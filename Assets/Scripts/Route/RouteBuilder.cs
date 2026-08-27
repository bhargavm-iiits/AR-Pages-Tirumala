using System.Collections.Generic;
using AlipiriAR.Data;
using AlipiriAR.Utilities;

namespace AlipiriAR.Route
{
    public class RouteResult
    {
        public readonly List<Waypoint> Waypoints = new();
        public readonly List<string> RejectedWayIds = new();
        public double TotalDistanceMeters;
        public double BridgedDistanceMeters;
    }

    /// <summary>
    /// Chains GeoWays into one walkable polyline by endpoint proximity, goal-directed so it
    /// naturally rejects spurs and bridges real survey gaps with a straight line — reproduces
    /// the exact topology verified in PLAN.md §01 (6 ways join at 0.0 m, way/365041854 is
    /// rejected as a 116.6 m spur, the 1,171.7 m gap before way/30434846 is bridged) without
    /// hardcoding any way ID, so it keeps working once a KML import changes the geometry.
    /// </summary>
    public static class RouteBuilder
    {
        /// <summary>Below this, two endpoints are the same physical point (a real OSM join).</summary>
        private const double JoinThresholdMeters = 15.0;

        /// <summary>Above this, a way is not a plausible next segment at all — reachable ways
        /// only. Below it but above JoinThreshold, a way is accepted as a bridged gap.</summary>
        private const double MaxBridgeThresholdMeters = 1500.0;

        private const double ArrivalThresholdMeters = 15.0;

        public static RouteResult Build(List<GeoWay> ways, (double lat, double lon) start, (double lat, double lon) end)
        {
            var result = new RouteResult();
            if (ways == null || ways.Count == 0) return result;

            var remaining = new List<GeoWay>(ways);

            GeoWay firstWay = null;
            bool firstReversed = false;
            double bestFirstDist = double.MaxValue;

            foreach (var way in remaining)
            {
                var (sLon, sLat, _) = way.Coordinates[0];
                var (eLon, eLat, _) = way.Coordinates[^1];
                double dStart = GeoMath.HaversineMeters(start.lat, start.lon, sLat, sLon);
                double dEnd = GeoMath.HaversineMeters(start.lat, start.lon, eLat, eLon);

                if (dStart < bestFirstDist) { bestFirstDist = dStart; firstWay = way; firstReversed = false; }
                if (dEnd < bestFirstDist) { bestFirstDist = dEnd; firstWay = way; firstReversed = true; }
            }

            if (firstWay == null) return result;

            remaining.Remove(firstWay);
            var firstCoords = firstReversed ? Reversed(firstWay.Coordinates) : firstWay.Coordinates;
            AppendWaypoints(result.Waypoints, firstCoords, isBridged: false);
            var (curLon, curLat, _) = firstCoords[^1];

            while (remaining.Count > 0)
            {
                double curToGoal = GeoMath.HaversineMeters(curLat, curLon, end.lat, end.lon);
                if (curToGoal <= ArrivalThresholdMeters) break;

                // Nearest endpoint wins outright. A small near-distance (ideally 0 m, a shared
                // OSM node) is a topological certainty that this way continues the path; "which
                // candidate makes the most progress toward the goal" is not — a farther-but-more-
                // direct way can look better on that metric while actually skipping a real
                // connector (verified against PLAN.md §01's ground truth: this is what makes the
                // algorithm walk 30434845→367434743→367434740→367434742→367434744 in order
                // instead of jumping straight to 367434744 and stranding 367434743 as a spur).
                GeoWay bestWay = null;
                bool bestReversed = false;
                double bestNearDist = double.MaxValue;
                double bestFarToGoal = double.MaxValue;

                foreach (var way in remaining)
                {
                    var (sLon, sLat, _) = way.Coordinates[0];
                    var (eLon, eLat, _) = way.Coordinates[^1];

                    double dToStart = GeoMath.HaversineMeters(curLat, curLon, sLat, sLon);
                    if (dToStart <= MaxBridgeThresholdMeters && dToStart < bestNearDist)
                    {
                        bestNearDist = dToStart;
                        bestFarToGoal = GeoMath.HaversineMeters(eLat, eLon, end.lat, end.lon);
                        bestWay = way;
                        bestReversed = false;
                    }

                    double dToEnd = GeoMath.HaversineMeters(curLat, curLon, eLat, eLon);
                    if (dToEnd <= MaxBridgeThresholdMeters && dToEnd < bestNearDist)
                    {
                        bestNearDist = dToEnd;
                        bestFarToGoal = GeoMath.HaversineMeters(sLat, sLon, end.lat, end.lon);
                        bestWay = way;
                        bestReversed = true;
                    }
                }

                // Nothing left is reachable, or attaching it wouldn't move us toward the
                // destination — stop rather than loop or wander onto an unrelated branch.
                if (bestWay == null || bestFarToGoal >= curToGoal) break;

                remaining.Remove(bestWay);
                var coords = bestReversed ? Reversed(bestWay.Coordinates) : bestWay.Coordinates;
                bool bridged = bestNearDist > JoinThresholdMeters;
                if (bridged) result.BridgedDistanceMeters += bestNearDist;

                // A near-zero-distance join means this way's first vertex is the same physical
                // point as the chain's current end — drop the duplicate. A bridged join keeps it,
                // since that vertex is genuinely new ground reached after crossing the gap.
                var toAppend = (!bridged && coords.Count > 1) ? coords.GetRange(1, coords.Count - 1) : coords;
                AppendWaypoints(result.Waypoints, toAppend, bridged);

                (curLon, curLat, _) = coords[^1];
            }

            foreach (var way in remaining) result.RejectedWayIds.Add(way.Id);
            result.TotalDistanceMeters = result.Waypoints.Count > 0 ? result.Waypoints[^1].CumulativeDistanceMeters : 0.0;
            return result;
        }

        private static List<(double lon, double lat, double elevation)> Reversed(List<(double lon, double lat, double elevation)> coords)
        {
            var copy = new List<(double lon, double lat, double elevation)>(coords);
            copy.Reverse();
            return copy;
        }

        private static void AppendWaypoints(List<Waypoint> waypoints, List<(double lon, double lat, double elevation)> coords, bool isBridged)
        {
            double cumulative = waypoints.Count > 0 ? waypoints[^1].CumulativeDistanceMeters : 0.0;
            double prevLat = waypoints.Count > 0 ? waypoints[^1].Latitude : coords[0].lat;
            double prevLon = waypoints.Count > 0 ? waypoints[^1].Longitude : coords[0].lon;
            bool isFirstOverall = waypoints.Count == 0;

            foreach (var (lon, lat, elevation) in coords)
            {
                if (!isFirstOverall)
                    cumulative += GeoMath.HaversineMeters(prevLat, prevLon, lat, lon);

                waypoints.Add(new Waypoint(lat, lon, (float)elevation, cumulative, isBridged));
                prevLat = lat;
                prevLon = lon;
                isFirstOverall = false;
            }
        }
    }
}
