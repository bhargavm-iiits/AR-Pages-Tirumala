using System.Collections.Generic;
using AlipiriAR.Data;
using UnityEngine;

namespace AlipiriAR.Utilities
{
    /// <summary>Route-relative queries used by the positioning pipeline (Phase 9) and AR arrow
    /// placement (Phase 10): projecting a raw GPS fix onto the route corridor, and densifying
    /// the ~44 m-average-spaced source vertices into ~5 m steps for smooth arrow trails.
    /// PLAN.md §08 step 3, §09 "Arrow rendering".</summary>
    public static class PolylineUtility
    {
        public readonly struct Projection
        {
            public readonly int SegmentIndex;
            public readonly double T;
            public readonly double Latitude;
            public readonly double Longitude;
            public readonly double CumulativeDistanceMeters;
            public readonly double LateralDistanceMeters;

            public Projection(int segmentIndex, double t, double lat, double lon, double cumulative, double lateral)
            {
                SegmentIndex = segmentIndex;
                T = t;
                Latitude = lat;
                Longitude = lon;
                CumulativeDistanceMeters = cumulative;
                LateralDistanceMeters = lateral;
            }
        }

        /// <summary>Projects (lat, lon) onto the nearest point on the route polyline.
        /// Segments are short enough (tens to a few hundred metres) that a local planar
        /// approximation centred on each segment is accurate to well under a metre.</summary>
        public static Projection ClosestPoint(IReadOnlyList<Waypoint> waypoints, double lat, double lon)
        {
            int bestSeg = 0;
            double bestT = 0.0;
            double bestLateral = double.MaxValue;
            double bestLat = waypoints.Count > 0 ? waypoints[0].Latitude : lat;
            double bestLon = waypoints.Count > 0 ? waypoints[0].Longitude : lon;

            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                var a = waypoints[i];
                var b = waypoints[i + 1];

                double refLat = a.Latitude;
                double mPerDegLat = 111320.0;
                double mPerDegLon = 111320.0 * System.Math.Cos(refLat * Mathf.Deg2Rad);

                var p2 = new Vector2d((b.Longitude - a.Longitude) * mPerDegLon, (b.Latitude - a.Latitude) * mPerDegLat);
                var q = new Vector2d((lon - a.Longitude) * mPerDegLon, (lat - a.Latitude) * mPerDegLat);

                double segLenSq = p2.x * p2.x + p2.y * p2.y;
                double t = segLenSq > 1e-9 ? Clamp01((q.x * p2.x + q.y * p2.y) / segLenSq) : 0.0;

                double closestX = p2.x * t;
                double closestY = p2.y * t;
                double dx = q.x - closestX, dy = q.y - closestY;
                double lateral = System.Math.Sqrt(dx * dx + dy * dy);

                if (lateral < bestLateral)
                {
                    bestLateral = lateral;
                    bestSeg = i;
                    bestT = t;
                    bestLat = a.Latitude + closestY / mPerDegLat;
                    bestLon = a.Longitude + closestX / mPerDegLon;
                }
            }

            double cumulative = waypoints.Count > 0
                ? Lerp(waypoints[bestSeg].CumulativeDistanceMeters,
                       waypoints[System.Math.Min(bestSeg + 1, waypoints.Count - 1)].CumulativeDistanceMeters, bestT)
                : 0.0;

            return new Projection(bestSeg, bestT, bestLat, bestLon, cumulative, bestLateral);
        }

        /// <summary>Inverse of ClosestPoint: the lat/lon at a given along-track distance. Used by
        /// RouteProgressTracker.Feed once positioning is driven by a fused s rather than a raw
        /// fix (Docs/update1.md §03/Phase 3 item 7) — the tracker needs to turn "you are at
        /// 4,180 m" back into a map coordinate. Distance is clamped to the route's own range, so
        /// a fused s slightly outside [0, total] (Kalman overshoot right at the endpoints) still
        /// resolves to the nearest real endpoint rather than extrapolating off the polyline.</summary>
        public static (double lat, double lon) PointAtDistance(IReadOnlyList<Waypoint> waypoints, double distanceMeters)
        {
            if (waypoints.Count == 0) return (0.0, 0.0);
            if (waypoints.Count == 1) return (waypoints[0].Latitude, waypoints[0].Longitude);

            double clamped = Clamp(distanceMeters, waypoints[0].CumulativeDistanceMeters, waypoints[^1].CumulativeDistanceMeters);

            int i = 0;
            while (i < waypoints.Count - 2 && waypoints[i + 1].CumulativeDistanceMeters < clamped) i++;

            var a = waypoints[i];
            var b = waypoints[i + 1];
            double segLen = b.CumulativeDistanceMeters - a.CumulativeDistanceMeters;
            double t = segLen > 1e-6 ? Clamp01((clamped - a.CumulativeDistanceMeters) / segLen) : 0.0;

            return (Lerp(a.Latitude, b.Latitude, t), Lerp(a.Longitude, b.Longitude, t));
        }

        /// <summary>Local gradient at a given along-track distance — rise over run between the
        /// bracketing waypoints, signed positive uphill. NaN if either waypoint lacks elevation
        /// (true for every waypoint until Phase 1's survey — see Waypoint.HasElevation), which
        /// callers must check before using this to weight a barometer update (§05: "gate: skip
        /// when grade &lt; 0.05" already implies "and skip entirely when grade is unknown").</summary>
        public static double GradeAtDistance(IReadOnlyList<Waypoint> waypoints, double distanceMeters)
        {
            if (waypoints.Count < 2) return double.NaN;

            double clamped = Clamp(distanceMeters, waypoints[0].CumulativeDistanceMeters, waypoints[^1].CumulativeDistanceMeters);
            int i = 0;
            while (i < waypoints.Count - 2 && waypoints[i + 1].CumulativeDistanceMeters < clamped) i++;

            var a = waypoints[i];
            var b = waypoints[i + 1];
            if (!a.HasElevation || !b.HasElevation) return double.NaN;

            double run = b.CumulativeDistanceMeters - a.CumulativeDistanceMeters;
            if (run < 1e-6) return 0.0;
            return (b.Elevation - a.Elevation) / run;
        }

        /// <summary>Elevation at a given along-track distance, inverted for the barometer update
        /// direction (altitude → s uses ElevationProfile⁻¹ in §05; this is the forward direction,
        /// s → altitude, which is what the inversion is checked against). NaN under the same
        /// condition as GradeAtDistance.</summary>
        public static double ElevationAtDistance(IReadOnlyList<Waypoint> waypoints, double distanceMeters)
        {
            if (waypoints.Count == 0) return double.NaN;
            if (waypoints.Count == 1) return waypoints[0].HasElevation ? waypoints[0].Elevation : double.NaN;

            double clamped = Clamp(distanceMeters, waypoints[0].CumulativeDistanceMeters, waypoints[^1].CumulativeDistanceMeters);
            int i = 0;
            while (i < waypoints.Count - 2 && waypoints[i + 1].CumulativeDistanceMeters < clamped) i++;

            var a = waypoints[i];
            var b = waypoints[i + 1];
            if (!a.HasElevation || !b.HasElevation) return double.NaN;

            double segLen = b.CumulativeDistanceMeters - a.CumulativeDistanceMeters;
            double t = segLen > 1e-6 ? Clamp01((clamped - a.CumulativeDistanceMeters) / segLen) : 0.0;
            return Lerp(a.Elevation, b.Elevation, t);
        }

        /// <summary>Inverts the elevation profile: the along-track distance whose modelled
        /// elevation is closest to observedElevationMeters, searched only in [searchFromMeters -
        /// windowMeters, searchFromMeters + windowMeters] — §05's barometer update needs "the s
        /// near where we already think we are with this altitude", not a global search, since
        /// 975 m of monotonic ascent still has flat/undulating sub-sections where one altitude
        /// value could otherwise match several distant points on the route. NaN if the window
        /// contains no elevation data.</summary>
        public static double InvertElevationNear(IReadOnlyList<Waypoint> waypoints, double observedElevationMeters, double searchFromMeters, double windowMeters)
        {
            double bestS = double.NaN;
            double bestDiff = double.MaxValue;

            double lo = searchFromMeters - windowMeters;
            double hi = searchFromMeters + windowMeters;

            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                var a = waypoints[i];
                var b = waypoints[i + 1];
                if (b.CumulativeDistanceMeters < lo || a.CumulativeDistanceMeters > hi) continue;
                if (!a.HasElevation || !b.HasElevation) continue;

                const int samples = 4;
                for (int s = 0; s <= samples; s++)
                {
                    double t = s / (double)samples;
                    double sampleS = Lerp(a.CumulativeDistanceMeters, b.CumulativeDistanceMeters, t);
                    if (sampleS < lo || sampleS > hi) continue;
                    double sampleElevation = Lerp(a.Elevation, b.Elevation, t);
                    double diff = System.Math.Abs(sampleElevation - observedElevationMeters);
                    if (diff < bestDiff)
                    {
                        bestDiff = diff;
                        bestS = sampleS;
                    }
                }
            }

            return bestS;
        }

        /// <summary>Inserts linearly-interpolated points so no gap between consecutive vertices
        /// exceeds maxSpacingMeters — the source GeoJSON averages ~44 m between vertices, far
        /// too coarse for a convincing AR chevron trail (PLAN.md §09).</summary>
        public static List<Waypoint> Densify(IReadOnlyList<Waypoint> waypoints, double maxSpacingMeters)
        {
            var result = new List<Waypoint>();
            if (waypoints.Count == 0) return result;

            result.Add(waypoints[0]);
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                var a = waypoints[i];
                var b = waypoints[i + 1];
                double segLen = b.CumulativeDistanceMeters - a.CumulativeDistanceMeters;
                int steps = segLen > maxSpacingMeters ? (int)System.Math.Ceiling(segLen / maxSpacingMeters) : 1;

                for (int s = 1; s <= steps; s++)
                {
                    double t = (double)s / steps;
                    double lat = Lerp(a.Latitude, b.Latitude, t);
                    double lon = Lerp(a.Longitude, b.Longitude, t);
                    double cumulative = Lerp(a.CumulativeDistanceMeters, b.CumulativeDistanceMeters, t);
                    float elevation = a.HasElevation && b.HasElevation
                        ? (float)Lerp(a.Elevation, b.Elevation, t)
                        : float.NaN;
                    result.Add(new Waypoint(lat, lon, elevation, cumulative, a.IsBridged || b.IsBridged));
                }
            }

            return result;
        }

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
        private static double Clamp01(double v) => v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);
        private static double Clamp(double v, double min, double max) => v < min ? min : (v > max ? max : v);

        private readonly struct Vector2d
        {
            public readonly double x, y;
            public Vector2d(double x, double y) { this.x = x; this.y = y; }
        }
    }
}
