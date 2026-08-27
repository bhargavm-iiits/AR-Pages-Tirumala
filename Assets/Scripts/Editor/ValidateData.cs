using System.IO;
using AlipiriAR.Data;
using AlipiriAR.Route;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace AlipiriAR.EditorTools
{
    /// <summary>Editor-only sanity check over the two StreamingAssets data files, run via
    /// Tools ▸ Alipiri AR ▸ Validate Data. Catches the §04 defect class (invalid JSON, an
    /// out-of-range coordinate, duplicate/ambiguous types) before it ships, and asserts the
    /// route still totals close to the surveyed 7,288.6 m so a bad edit to the GeoJSON or a
    /// RouteBuilder regression is caught here instead of on the mountain.</summary>
    public static class ValidateData
    {
        private const double ExpectedTotalMeters = 7288.6;
        private const double TotalToleranceMeters = 25.0;

        [MenuItem("Alipiri AR/Validate Data", false, 3)]
        [MenuItem("Tools/Alipiri AR/Validate Data", false, 3)]
        public static void Run()
        {
            int errors = 0;
            int warnings = 0;

            errors += ValidateLandmarks(out int landmarkCount);
            var (routeErrors, routeWarnings) = ValidateRoute();
            errors += routeErrors;
            warnings += routeWarnings;

            if (errors == 0 && warnings == 0)
                Debug.Log($"[ValidateData] OK — {landmarkCount} landmarks, route within tolerance. No issues.");
            else
                Debug.Log($"[ValidateData] Finished with {errors} error(s), {warnings} warning(s). See messages above.");
        }

        private static int ValidateLandmarks(out int count)
        {
            count = 0;
            string path = Path.Combine(Application.streamingAssetsPath, "Database/landmarks.json");
            if (!File.Exists(path))
            {
                Debug.LogError($"[ValidateData] Missing {path}");
                return 1;
            }

            JObject root;
            try
            {
                root = JObject.Parse(File.ReadAllText(path));
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ValidateData] landmarks.json is not valid JSON: {e.Message}");
                return 1;
            }

            var array = root["landmarks"] as JArray;
            if (array == null)
            {
                Debug.LogError("[ValidateData] landmarks.json has no \"landmarks\" array.");
                return 1;
            }

            int errors = 0;
            var seenIds = new System.Collections.Generic.HashSet<int>();

            foreach (var token in array)
            {
                count++;
                int id = token.Value<int?>("id") ?? -1;
                double lat = token.Value<double?>("latitude") ?? double.NaN;
                double lon = token.Value<double?>("longitude") ?? double.NaN;
                string name = token.Value<string>("name") ?? "(unnamed)";

                if (!seenIds.Add(id))
                {
                    Debug.LogError($"[ValidateData] Duplicate landmark id {id} (\"{name}\").");
                    errors++;
                }

                if (lat is double.NaN || lon is double.NaN)
                {
                    Debug.LogError($"[ValidateData] Landmark {id} (\"{name}\") missing lat/lon.");
                    errors++;
                    continue;
                }

                // Alipiri–Tirumala bounding box with margin, per PLAN.md §01.
                if (lat < 13.5 || lat > 13.8 || lon < 79.2 || lon > 79.5)
                {
                    Debug.LogError($"[ValidateData] Landmark {id} (\"{name}\") is outside the expected bbox: {lat}, {lon}");
                    errors++;
                }
            }

            if (count != 41)
                Debug.LogWarning($"[ValidateData] Expected 41 landmarks, found {count}. Not necessarily wrong — just confirm it's intentional.");

            return errors;
        }

        private static (int errors, int warnings) ValidateRoute()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Route/alipiri_mettu.geojson");
            if (!File.Exists(path))
            {
                Debug.LogError($"[ValidateData] Missing {path}");
                return (1, 0);
            }

            var ways = GeoJsonParser.ParseWays(File.ReadAllText(path));
            if (ways.Count == 0)
            {
                Debug.LogError("[ValidateData] Route GeoJSON parsed to zero ways.");
                return (1, 0);
            }

            var result = RouteBuilder.Build(ways, (13.646192, 79.405825), (13.672393, 79.351832));
            int warnings = 0;

            if (result.Waypoints.Count < 2)
            {
                Debug.LogError("[ValidateData] RouteBuilder produced fewer than 2 waypoints.");
                return (1, warnings);
            }

            double total = result.TotalDistanceMeters;
            if (System.Math.Abs(total - ExpectedTotalMeters) > TotalToleranceMeters)
            {
                Debug.LogWarning($"[ValidateData] Route total is {total:F1} m, expected ~{ExpectedTotalMeters:F1} m " +
                                  $"(±{TotalToleranceMeters:F0} m). Check the source data or update this expectation if the change is intentional.");
                warnings++;
            }

            if (result.BridgedDistanceMeters > 0)
                Debug.LogWarning($"[ValidateData] Route still has {result.BridgedDistanceMeters:F1} m of straight-line bridge — no KML import yet (PLAN.md §12).");

            if (result.RejectedWayIds.Count > 0)
                Debug.Log($"[ValidateData] Rejected {result.RejectedWayIds.Count} way(s) as spurs: {string.Join(", ", result.RejectedWayIds)}");

            return (0, warnings);
        }
    }
}
