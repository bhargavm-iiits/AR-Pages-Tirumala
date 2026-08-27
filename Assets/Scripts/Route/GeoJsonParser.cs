using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace AlipiriAR.Route
{
    /// <summary>One OSM way (or KML LineString) before chaining — PLAN.md §01 calls these
    /// "ways". Coordinates are stored (lon, lat) to match GeoJSON's own axis order.
    /// Elevation is double.NaN when the source has none — true for the current GeoJSON,
    /// populated when a KML import carries altitude (PLAN.md §03's "one ask for the KML").</summary>
    public class GeoWay
    {
        public string Id;
        public readonly List<(double lon, double lat, double elevation)> Coordinates = new();
    }

    /// <summary>Parses a GeoJSON FeatureCollection into the LineString ways RouteBuilder chains.
    /// Point features (landmark-style nodes sometimes embedded in the same file) are ignored here —
    /// landmark data comes from landmarks.json, not the route file.</summary>
    public static class GeoJsonParser
    {
        public static List<GeoWay> ParseWays(string json)
        {
            var ways = new List<GeoWay>();
            if (string.IsNullOrEmpty(json)) return ways;

            JObject root;
            try { root = JObject.Parse(json); }
            catch (System.Exception e)
            {
                Debug.LogError($"[GeoJsonParser] Failed to parse GeoJSON: {e.Message}");
                return ways;
            }

            var features = root["features"] as JArray;
            if (features == null) return ways;

            foreach (var feature in features)
            {
                var geometry = feature["geometry"];
                if (geometry == null || (string)geometry["type"] != "LineString") continue;

                var coordsToken = geometry["coordinates"] as JArray;
                if (coordsToken == null || coordsToken.Count < 2) continue;

                var way = new GeoWay { Id = ResolveId(feature) };
                foreach (var coord in coordsToken)
                {
                    if (coord is not JArray pair || pair.Count < 2) continue;
                    double lon = pair[0].Value<double>();
                    double lat = pair[1].Value<double>();
                    double elevation = pair.Count >= 3 ? pair[2].Value<double>() : double.NaN;
                    way.Coordinates.Add((lon, lat, elevation));
                }

                if (way.Coordinates.Count >= 2) ways.Add(way);
            }

            return ways;
        }

        private static string ResolveId(JToken feature)
        {
            var id = feature["id"]?.ToString();
            if (!string.IsNullOrEmpty(id)) return id;

            var propId = feature["properties"]?["@id"]?.ToString();
            return string.IsNullOrEmpty(propId) ? "way/unknown" : propId;
        }
    }
}
