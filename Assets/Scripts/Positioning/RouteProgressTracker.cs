using System;
using AlipiriAR.Route;
using AlipiriAR.Utilities;

namespace AlipiriAR.Positioning
{
    /// <summary>Snaps LocationProvider's filtered fix onto the route polyline and derives every
    /// number the four data screens show — completed/remaining distance, %, ETA — from that one
    /// scalar (PLAN.md §08 step 3/4, §09). Nothing computes its own version of "how far along
    /// the route am I" anywhere else. Snapping is safe here specifically because the stairway is
    /// a single corridor with no alternative path (PLAN.md §08 step 3).</summary>
    public class RouteProgressTracker
    {
        /// <summary>Below this, a fix that snaps behind the tracked high-water mark is ordinary
        /// GPS jitter and gets absorbed, not treated as the walker turning around. Found on a real
        /// device: closest-point snapping has no floor, so routine noise made CumulativeDistanceMeters
        /// visibly dip backward frame to frame — and since DynamicArrowManager anchors the whole
        /// chevron trail to this value, that dip showed up as the arrows themselves jumping
        /// backward. Matches the single-corridor assumption this class already documents: real
        /// reversals happen (a walker can turn back), but only past ordinary noise.</summary>
        private const double BackwardJitterToleranceMeters = 5.0;

        private readonly RouteResult _route;
        private readonly EtaEstimator _eta = new();
        private double _highWaterMarkMeters;

        public double CumulativeDistanceMeters { get; private set; }
        public double LateralDistanceMeters { get; private set; }
        public double Latitude { get; private set; }
        public double Longitude { get; private set; }

        public double TotalDistanceMeters => _route.TotalDistanceMeters;
        public double RemainingDistanceMeters => Math.Max(TotalDistanceMeters - CumulativeDistanceMeters, 0.0);
        public float FractionComplete => TotalDistanceMeters > 0 ? (float)(CumulativeDistanceMeters / TotalDistanceMeters) : 0f;

        public event Action OnUpdated;

        public RouteProgressTracker(RouteResult route)
        {
            _route = route;
        }

        public void Feed(double rawLat, double rawLon, double timestamp)
        {
            var projection = PolylineUtility.ClosestPoint(_route.Waypoints, rawLat, rawLon);

            _highWaterMarkMeters = projection.CumulativeDistanceMeters < _highWaterMarkMeters - BackwardJitterToleranceMeters
                ? projection.CumulativeDistanceMeters
                : Math.Max(_highWaterMarkMeters, projection.CumulativeDistanceMeters);
            CumulativeDistanceMeters = _highWaterMarkMeters;

            LateralDistanceMeters = projection.LateralDistanceMeters;
            Latitude = projection.Latitude;
            Longitude = projection.Longitude;

            _eta.Feed(CumulativeDistanceMeters, timestamp);
            OnUpdated?.Invoke();
        }

        public int EtaMinutes() => _eta.EstimateMinutes(RemainingDistanceMeters);
    }
}
