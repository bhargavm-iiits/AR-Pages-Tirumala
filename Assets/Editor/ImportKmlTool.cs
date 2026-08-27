using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AlipiriAR.Route;
using AlipiriAR.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace AlipiriAR.EditorTools
{
    /// <summary>
    /// The KML import path NewPlan.md §04 Phase F called for and that never existed — KmlImporter
    /// itself was written long ago but had no caller, so a KML could never actually reach the app.
    ///
    /// Writes into Assets/StreamingAssets/Route/alipiri_mettu.geojson, the file JsonDatabase loads
    /// at runtime. Everything downstream (RouteBuilder chaining, the Map overlay, AR chevrons,
    /// distances/ETA) reads from that one file, so a correct import updates all of them at once
    /// with no other code change.
    ///
    /// Always writes a timestamped backup next to the original before touching it — an import that
    /// turns out to be wrong (points not in walk order being the likely way that happens) must be
    /// undoable without needing version control to have been clean at that moment.
    /// </summary>
    public static class ImportKmlTool
    {
        private const string RouteAssetPath = "Assets/StreamingAssets/Route/alipiri_mettu.geojson";

        [MenuItem("Tools/Alipiri AR/Import KML Route…", false, 20)]
        public static void ImportKmlRoute()
        {
            string kmlPath = EditorUtility.OpenFilePanel("Select KML route file", "", "kml");
            if (string.IsNullOrEmpty(kmlPath)) return;

            string kmlText;
            try { kmlText = File.ReadAllText(kmlPath); }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Import KML", $"Could not read the file:\n{e.Message}", "OK");
                return;
            }

            // LineStrings are unambiguous geometry, so they win when present. Only when the file
            // has none do we fall back to treating loose Points/gx:Track as an ordered path, which
            // is the interpretation that can be wrong if the file isn't in walk order.
            var ways = KmlImporter.ParseWays(kmlText);
            string orderingWarning = null;
            string sourceKind;

            if (ways.Count > 0)
            {
                sourceKind = $"{ways.Count} LineString placemark(s)";
            }
            else
            {
                var pointWay = KmlImporter.ParsePointPath(kmlText, out orderingWarning);
                if (pointWay.Coordinates.Count < 2)
                {
                    EditorUtility.DisplayDialog("Import KML",
                        "No usable geometry found — the file has no LineString placemarks, no gx:Track, " +
                        "and fewer than two Point placemarks.", "OK");
                    return;
                }
                ways = new List<GeoWay> { pointWay };
                sourceKind = $"{pointWay.Coordinates.Count} points, treated as one path in document order";
            }

            var stats = Summarize(ways);
            var existing = ReadExistingStats();

            string message =
                $"Read from KML:\n" +
                $"  {sourceKind}\n" +
                $"  {stats.vertices} vertices, {stats.lengthMeters:F0} m end-to-end\n" +
                $"  Elevation: {(stats.hasAltitude ? "present — unlocks the elevation profile and real step count" : "absent")}\n\n" +
                $"Current route file:\n" +
                $"  {existing.features} features, {existing.vertices} vertices\n\n" +
                (orderingWarning != null ? $"⚠ {orderingWarning}\n\n" : string.Empty) +
                $"Replace overwrites the route with the KML alone.\n" +
                $"Merge appends it, letting RouteBuilder chain it with the existing ways.\n\n" +
                $"A timestamped backup is written either way.";

            int choice = EditorUtility.DisplayDialogComplex("Import KML", message, "Replace", "Cancel", "Merge");
            if (choice == 1) return;

            bool merge = choice == 2;

            try
            {
                string backupPath = WriteBackup();
                WriteGeoJson(ways, merge);
                AssetDatabase.ImportAsset(RouteAssetPath, ImportAssetOptions.ForceUpdate);

                var after = ReadExistingStats();
                Debug.Log($"[ImportKmlTool] Imported {stats.vertices} vertices from \"{Path.GetFileName(kmlPath)}\" " +
                          $"({(merge ? "merged" : "replaced")}). Route file now has {after.features} features / " +
                          $"{after.vertices} vertices. Backup: {backupPath}");
                EditorUtility.DisplayDialog("Import KML",
                    $"Done — route file now has {after.features} features / {after.vertices} vertices.\n\n" +
                    $"Backup written to:\n{backupPath}\n\n" +
                    $"Run Tools ▸ Alipiri AR ▸ Validate Data to check the result, then rebuild.", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ImportKmlTool] Import failed: {e}");
                EditorUtility.DisplayDialog("Import KML", $"Import failed:\n{e.Message}", "OK");
            }
        }

        private static (int vertices, double lengthMeters, bool hasAltitude) Summarize(List<GeoWay> ways)
        {
            int vertices = 0;
            double length = 0.0;
            foreach (var way in ways)
            {
                vertices += way.Coordinates.Count;
                for (int i = 1; i < way.Coordinates.Count; i++)
                {
                    var (pLon, pLat, _) = way.Coordinates[i - 1];
                    var (cLon, cLat, _) = way.Coordinates[i];
                    length += GeoMath.HaversineMeters(pLat, pLon, cLat, cLon);
                }
            }
            return (vertices, length, KmlImporter.HasAltitude(ways));
        }

        private static (int features, int vertices) ReadExistingStats()
        {
            string full = FullRoutePath();
            if (!File.Exists(full)) return (0, 0);

            try
            {
                var root = JObject.Parse(File.ReadAllText(full));
                var features = root["features"] as JArray;
                if (features == null) return (0, 0);

                int vertices = 0;
                foreach (var feature in features)
                {
                    if ((string)feature["geometry"]?["type"] != "LineString") continue;
                    vertices += (feature["geometry"]["coordinates"] as JArray)?.Count ?? 0;
                }
                return (features.Count, vertices);
            }
            catch
            {
                return (0, 0);
            }
        }

        /// <summary>Backups go to &lt;project&gt;/Backups/Route/, deliberately OUTSIDE Assets/ —
        /// everything under StreamingAssets is copied verbatim into the APK, so keeping backups
        /// beside the original would ship every one of them to users and grow the build with each
        /// import. Outside Assets/ they're also invisible to the AssetDatabase, so they can't
        /// become stray .meta churn either.</summary>
        private static string WriteBackup()
        {
            string full = FullRoutePath();
            if (!File.Exists(full)) return "(no existing file to back up)";

            string projectRoot = Path.GetDirectoryName(Application.dataPath)!;
            string backupDir = Path.Combine(projectRoot, "Backups", "Route");
            Directory.CreateDirectory(backupDir);

            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string backup = Path.Combine(backupDir, $"alipiri_mettu.backup-{stamp}.geojson");
            File.Copy(full, backup, overwrite: true);
            return backup;
        }

        private static void WriteGeoJson(List<GeoWay> ways, bool merge)
        {
            string full = FullRoutePath();
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);

            JArray features;
            if (merge && File.Exists(full))
            {
                var existingRoot = JObject.Parse(File.ReadAllText(full));
                features = existingRoot["features"] as JArray ?? new JArray();
            }
            else
            {
                features = new JArray();
            }

            foreach (var way in ways)
            {
                var coords = new JArray();
                foreach (var (lon, lat, elevation) in way.Coordinates)
                {
                    // Third element only when there's real altitude — GeoJsonParser reads a 3rd
                    // element as elevation, so emitting a placeholder would fabricate flat terrain
                    // and silently defeat the "no elevation yet" empty state the app relies on.
                    coords.Add(double.IsNaN(elevation)
                        ? new JArray(lon, lat)
                        : new JArray(lon, lat, elevation));
                }

                features.Add(new JObject
                {
                    ["type"] = "Feature",
                    ["id"] = way.Id,
                    ["properties"] = new JObject { ["@id"] = way.Id, ["source"] = "kml-import" },
                    ["geometry"] = new JObject { ["type"] = "LineString", ["coordinates"] = coords },
                });
            }

            var root = new JObject
            {
                ["type"] = "FeatureCollection",
                ["features"] = features,
            };

            File.WriteAllText(full, root.ToString(Formatting.Indented));
        }

        private static string FullRoutePath() =>
            Path.Combine(Application.dataPath, "StreamingAssets/Route/alipiri_mettu.geojson");
    }
}
