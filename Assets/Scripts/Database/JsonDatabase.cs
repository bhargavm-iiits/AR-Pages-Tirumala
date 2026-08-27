using System.Collections;
using System.Collections.Generic;
using AlipiriAR.Data;
using AlipiriAR.Route;
using AlipiriAR.Utilities;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace AlipiriAR.Database
{
    /// <summary>Loads and cross-validates the two StreamingAssets data files this app runs on:
    /// landmarks.json and the route GeoJSON (or an imported KML once one exists — PLAN.md §01/§05).
    /// Builds the route once via RouteBuilder and resolves every landmark's position along it,
    /// so the Landmarks list, Progress screen and Map screen all read the same numbers.</summary>
    public class JsonDatabase
    {
        /// <summary>Known reference points bounding the corridor — used only to seed
        /// RouteBuilder's goal-directed chaining, not as authoritative landmark data.</summary>
        private static readonly (double lat, double lon) AlipiriStart = (13.646192, 79.405825);
        private static readonly (double lat, double lon) TirumalaEnd = (13.672393, 79.351832);

        public IReadOnlyList<LandmarkData> Landmarks { get; private set; } = new List<LandmarkData>();
        public RouteResult Route { get; private set; } = new();
        public bool IsLoaded { get; private set; }

        public IEnumerator LoadAll()
        {
            string landmarksJson = null;
            yield return StreamingAssetsLoader.LoadText("Database/landmarks.json", t => landmarksJson = t);
            Landmarks = ParseLandmarks(landmarksJson);

            string geoJson = null;
            yield return StreamingAssetsLoader.LoadText("Route/alipiri_mettu.geojson", t => geoJson = t);
            var ways = GeoJsonParser.ParseWays(geoJson);
            Route = RouteBuilder.Build(ways, AlipiriStart, TirumalaEnd);

            if (Route.RejectedWayIds.Count > 0)
                Debug.Log($"[JsonDatabase] Route builder rejected {Route.RejectedWayIds.Count} way(s) as spurs: {string.Join(", ", Route.RejectedWayIds)}");
            if (Route.BridgedDistanceMeters > 0)
                Debug.LogWarning($"[JsonDatabase] Route has {Route.BridgedDistanceMeters:F1} m of straight-line bridge (no surveyed geometry). See PLAN.md §01/§12.");

            ResolveLandmarkDistances();
            IsLoaded = true;
        }

        private void ResolveLandmarkDistances()
        {
            if (Route.Waypoints.Count == 0) return;

            foreach (var lm in Landmarks)
            {
                var projection = PolylineUtility.ClosestPoint(Route.Waypoints, lm.Latitude, lm.Longitude);
                lm.CumulativeDistanceMeters = projection.CumulativeDistanceMeters;
                lm.SnappedLatitude = projection.Latitude;
                lm.SnappedLongitude = projection.Longitude;
            }
        }

        private static List<LandmarkData> ParseLandmarks(string json)
        {
            var result = new List<LandmarkData>();
            if (string.IsNullOrEmpty(json)) return result;

            JObject root;
            try { root = JObject.Parse(json); }
            catch (System.Exception e)
            {
                Debug.LogError($"[JsonDatabase] Failed to parse landmarks.json: {e.Message}");
                return result;
            }

            var array = root["landmarks"] as JArray;
            if (array == null) return result;

            foreach (var token in array)
            {
                string voiceText = token.Value<string>("voiceText") ?? string.Empty;
                string description = token.Value<string>("description");

                var lm = new LandmarkData
                {
                    Id = token.Value<int?>("id") ?? 0,
                    Name = token.Value<string>("name") ?? string.Empty,
                    Type = LandmarkTypeExtensions.Parse(token.Value<string>("type")),
                    Latitude = token.Value<double?>("latitude") ?? 0.0,
                    Longitude = token.Value<double?>("longitude") ?? 0.0,
                    TriggerRadiusMeters = token.Value<float?>("triggerRadius") ?? 20f,
                    VoiceText = voiceText,
                    // No "description" field exists in the source data yet (NewPlan.md §03 D6) —
                    // fall back to VoiceText so display copy still reads correctly until a
                    // content pass gives the two a chance to diverge.
                    Description = string.IsNullOrEmpty(description) ? voiceText : description,
                    Priority = token.Value<int?>("priority") ?? 1,
                    // Rev 4 heritage layer fields (Docs/GeospatialPlan.md §07) — none exist in the
                    // source data yet, so every entry parses to its documented default and
                    // behaves exactly as it did before this field existed.
                    Zone = RouteZoneExtensions.Parse(token.Value<string>("zone")),
                    GeospatialEnabled = token.Value<bool?>("geospatialEnabled") ?? false,
                    AnchorType = GeospatialAnchorTypeExtensions.Parse(token.Value<string>("anchorType")),
                    ExperienceId = token.Value<string>("experienceId"),
                };
                result.Add(lm);
            }

            return result;
        }
    }
}
