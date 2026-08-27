using System.Collections.Generic;
using AlipiriAR.Data;
using AlipiriAR.Utilities;

namespace AlipiriAR.Positioning
{
    public enum ManeuverType { Straight, TurnLeft, TurnRight }

    public readonly struct Maneuver
    {
        public readonly ManeuverType Type;
        public readonly double DistanceMeters;
        public readonly string TowardsName;

        public Maneuver(ManeuverType type, double distanceMeters, string towardsName)
        {
            Type = type;
            DistanceMeters = distanceMeters;
            TowardsName = towardsName;
        }
    }

    /// <summary>Bearing deltas between upcoming route segments → turn cards (PLAN.md §09,
    /// mockup frame 7). A stairway corridor has no branching junctions, so a "turn" here means
    /// the path's own bend, not an intersection choice — still useful context for the AR
    /// overlay (Scene 6) even though there is only ever one way to go.</summary>
    public static class ManeuverDetector
    {
        private const double StraightThresholdDegrees = 18.0;

        public static Maneuver? DetectNext(double cumulativeDistanceMeters, IReadOnlyList<Waypoint> waypoints, string towardsName)
        {
            int segIndex = FindSegmentIndex(waypoints, cumulativeDistanceMeters);
            if (segIndex < 0 || segIndex >= waypoints.Count - 2) return null;

            var a = waypoints[segIndex];
            var b = waypoints[segIndex + 1];
            var c = waypoints[segIndex + 2];

            double bearingIn = GeoMath.BearingDegrees(a.Latitude, a.Longitude, b.Latitude, b.Longitude);
            double bearingOut = GeoMath.BearingDegrees(b.Latitude, b.Longitude, c.Latitude, c.Longitude);
            double delta = NormalizeAngle(bearingOut - bearingIn);

            var type = System.Math.Abs(delta) < StraightThresholdDegrees
                ? ManeuverType.Straight
                : (delta > 0 ? ManeuverType.TurnRight : ManeuverType.TurnLeft);

            double distanceToTurn = System.Math.Max(b.CumulativeDistanceMeters - cumulativeDistanceMeters, 0.0);
            return new Maneuver(type, distanceToTurn, towardsName);
        }

        private static int FindSegmentIndex(IReadOnlyList<Waypoint> waypoints, double cumulativeDistance)
        {
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                if (waypoints[i + 1].CumulativeDistanceMeters >= cumulativeDistance) return i;
            }
            return waypoints.Count - 2;
        }

        private static double NormalizeAngle(double deg)
        {
            deg %= 360.0;
            if (deg > 180.0) deg -= 360.0;
            if (deg < -180.0) deg += 360.0;
            return deg;
        }
    }
}
