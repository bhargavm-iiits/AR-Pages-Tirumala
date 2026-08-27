using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

namespace AlipiriAR.Route
{
    /// <summary>
    /// Parses a KML file's route geometry into GeoWay objects, the same type GeoJsonParser
    /// produces — so RouteBuilder chains a KML import exactly like the existing GeoJSON, no
    /// separate code path. Handles all three shapes a route arrives in: LineString placemarks
    /// (ParseWays), and loose Point placemarks or a gx:Track (ParsePointPath). Reads altitude
    /// when present, which is what unlocks the real elevation profile and step estimate
    /// (PLAN.md §03's "one ask for the KML", §12). Edit-time only — ImportKmlTool (Editor) is
    /// the caller, writing the result into StreamingAssets/Route/alipiri_mettu.geojson.
    /// </summary>
    public static class KmlImporter
    {
        private static readonly XNamespace Kml = "http://www.opengis.net/kml/2.2";
        private static readonly XNamespace Gx = "http://www.google.com/kml/ext/2.2";

        /// <summary>Builds a single way from a KML whose path is expressed as a sequence of
        /// individual Point placemarks (or a gx:Track), rather than one LineString — the shape a
        /// hand-dropped or GPS-recorded walk usually exports as. Document order IS path order:
        /// KML has no other ordering signal for separate placemarks, so the file must list them
        /// walk-order start→end. OrderingWarning reports when the spacing pattern suggests it
        /// doesn't (see CheckOrdering) instead of silently producing a zigzag route.</summary>
        public static GeoWay ParsePointPath(string kmlText, out string orderingWarning)
        {
            orderingWarning = null;
            var way = new GeoWay { Id = "kml/point-path" };
            if (string.IsNullOrEmpty(kmlText)) return way;

            XDocument doc;
            try { doc = XDocument.Parse(kmlText); }
            catch (Exception e)
            {
                Debug.LogError($"[KmlImporter] Failed to parse KML: {e.Message}");
                return way;
            }

            // gx:Track first — when present it's an explicitly ordered recording, strictly better
            // evidence of path order than loose placemarks, so it wins outright.
            foreach (var coord in doc.Descendants(Gx + "coord"))
            {
                // gx:coord is space-separated "lon lat alt", unlike <coordinates>' comma form.
                var parts = coord.Value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double tLon)) continue;
                if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double tLat)) continue;
                double tAlt = parts.Length >= 3 && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double a)
                    ? a
                    : double.NaN;
                way.Coordinates.Add((tLon, tLat, tAlt));
            }

            if (way.Coordinates.Count == 0)
            {
                var points = doc.Descendants(Kml + "Point").Any()
                    ? doc.Descendants(Kml + "Point")
                    : doc.Descendants("Point");

                foreach (var point in points)
                {
                    var coordsElement = point.Descendants(Kml + "coordinates").FirstOrDefault()
                                         ?? point.Descendants("coordinates").FirstOrDefault();
                    if (coordsElement == null || string.IsNullOrWhiteSpace(coordsElement.Value)) continue;

                    var parts = coordsElement.Value.Trim().Split(',');
                    if (parts.Length < 2) continue;
                    if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double lon)) continue;
                    if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double lat)) continue;
                    double elevation = parts.Length >= 3 && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double alt)
                        ? alt
                        : double.NaN;
                    way.Coordinates.Add((lon, lat, elevation));
                }
            }

            orderingWarning = CheckOrdering(way);
            LogAltitude(new List<GeoWay> { way });
            return way;
        }

        /// <summary>A path's consecutive points are spaced fairly evenly; a file listing the same
        /// points in some non-walk order (alphabetical by name, say) produces wild jumps back and
        /// forth. Flags when any single hop is more than 8x the median hop — cheap, and the only
        /// automatic check possible, since KML itself carries no ordering metadata to verify
        /// against. A warning, never a rejection: a genuine route can legitimately have one long
        /// straight stretch, so the human has to make the call.</summary>
        private static string CheckOrdering(GeoWay way)
        {
            if (way.Coordinates.Count < 4) return null;

            var hops = new List<double>(way.Coordinates.Count - 1);
            for (int i = 1; i < way.Coordinates.Count; i++)
            {
                var (pLon, pLat, _) = way.Coordinates[i - 1];
                var (cLon, cLat, _) = way.Coordinates[i];
                hops.Add(Utilities.GeoMath.HaversineMeters(pLat, pLon, cLat, cLon));
            }

            var sorted = new List<double>(hops);
            sorted.Sort();
            double median = sorted[sorted.Count / 2];
            if (median <= 0.0) return null;

            double worst = sorted[^1];
            if (worst < median * 8.0) return null;

            int worstIndex = hops.IndexOf(worst);
            return $"Point spacing is uneven: the jump between point {worstIndex + 1} and {worstIndex + 2} " +
                   $"is {worst:F0} m, {worst / median:F0}x the median {median:F0} m hop. If the points aren't " +
                   $"listed in walking order start→end, the imported route will zigzag.";
        }

        public static List<GeoWay> ParseWays(string kmlText)
        {
            var ways = new List<GeoWay>();
            if (string.IsNullOrEmpty(kmlText)) return ways;

            XDocument doc;
            try { doc = XDocument.Parse(kmlText); }
            catch (Exception e)
            {
                Debug.LogError($"[KmlImporter] Failed to parse KML: {e.Message}");
                return ways;
            }

            // KML files vary in whether they declare the kml/2.2 namespace explicitly; search
            // both namespaced and un-namespaced so exports from different tools all work.
            var placemarks = doc.Descendants(Kml + "Placemark").Any()
                ? doc.Descendants(Kml + "Placemark")
                : doc.Descendants("Placemark");

            int index = 0;
            foreach (var placemark in placemarks)
            {
                var lineString = placemark.Descendants(Kml + "LineString").FirstOrDefault()
                                  ?? placemark.Descendants("LineString").FirstOrDefault();
                if (lineString == null) continue;

                var coordsElement = lineString.Descendants(Kml + "coordinates").FirstOrDefault()
                                     ?? lineString.Descendants("coordinates").FirstOrDefault();
                if (coordsElement == null || string.IsNullOrWhiteSpace(coordsElement.Value)) continue;

                var way = new GeoWay { Id = ResolveName(placemark, ++index) };
                foreach (var tuple in coordsElement.Value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = tuple.Split(',');
                    if (parts.Length < 2) continue;
                    if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double lon)) continue;
                    if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double lat)) continue;
                    double elevation = parts.Length >= 3 && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double alt)
                        ? alt
                        : double.NaN;
                    way.Coordinates.Add((lon, lat, elevation));
                }

                if (way.Coordinates.Count >= 2) ways.Add(way);
            }

            LogAltitude(ways);
            return ways;
        }

        /// <summary>Google My Maps (and some hand-exported KMLs) writes a placeholder elevation —
        /// usually 0 — on every single coordinate rather than omitting the third component. That's
        /// a valid, non-NaN number, so a naive "any elevation present" check would treat a flat
        /// placeholder column as real survey data and the Progress screen would draw a convincing
        /// but entirely fake flat elevation profile (NewPlan.md defect register, elevation guard).
        /// A real survey/GPS trace varies from point to point; a placeholder column does not — so
        /// this requires at least two distinct elevation values across the whole route before
        /// trusting any of them.</summary>
        public static bool HasAltitude(IEnumerable<GeoWay> ways)
        {
            double? first = null;
            foreach (var way in ways)
            {
                foreach (var (_, _, elevation) in way.Coordinates)
                {
                    if (double.IsNaN(elevation)) continue;
                    if (first == null) { first = elevation; continue; }
                    if (Math.Abs(elevation - first.Value) > 0.01) return true;
                }
            }
            return false;
        }

        private static void LogAltitude(List<GeoWay> ways)
        {
            Debug.Log(HasAltitude(ways)
                ? "[KmlImporter] Altitude present — elevation profile and step estimate will use real data."
                : "[KmlImporter] No altitude in this KML — elevation profile stays on its empty state.");
        }

        private static string ResolveName(XElement placemark, int fallbackIndex)
        {
            var name = (placemark.Element(Kml + "name") ?? placemark.Element("name"))?.Value;
            return string.IsNullOrWhiteSpace(name) ? $"kml/{fallbackIndex}" : name.Trim();
        }
    }
}
