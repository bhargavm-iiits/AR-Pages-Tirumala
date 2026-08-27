using System.Collections.Generic;
using AlipiriAR.Data;
using AlipiriAR.Utilities;
using UnityEngine;

namespace AlipiriAR.AR
{
    /// <summary>Pooled chevrons along the next ~100 m of route, spaced ~2.5 m, re-pooled as the
    /// user advances (PLAN.md §09) — "along the path" reads as a continuous trail rather than a
    /// short stub, and stays within ARSessionBootstrapper's 150m camera far clip so nothing in the
    /// trail is invisibly clipped. Route is Densify()'d to 5 m spacing first — the raw ~44 m mean
    /// vertex spacing is far too coarse to look like a path. Device-unverified — see
    /// ARSessionBootstrapper's class doc.</summary>
    public class DynamicArrowManager
    {
        private const float TrailLengthMeters = 100f;
        private const float ArrowSpacingMeters = 2.5f;
        private const float FadeStartMeters = 80f;

        private readonly List<Waypoint> _densified;
        private readonly GroundPlacementService _placement;
        private readonly HybridLocalizationEngine _localization;
        private readonly List<NavigationArrow> _pool = new();

        public DynamicArrowManager(IReadOnlyList<Waypoint> routeWaypoints, GroundPlacementService placement, HybridLocalizationEngine localization, Transform poolRoot)
        {
            _densified = PolylineUtility.Densify(routeWaypoints, 5.0);
            _placement = placement;
            _localization = localization;

            int poolSize = Mathf.CeilToInt(TrailLengthMeters / ArrowSpacingMeters) + 2;
            for (int i = 0; i < poolSize; i++)
                _pool.Add(NavigationArrow.Create(poolRoot));
        }

        /// <summary>Repositions the pool for the current route position — call every frame (or
        /// every few) while AR is active.</summary>
        public void Refresh(double currentCumulativeDistance, Vector3 cameraForward)
        {
            if (!_localization.Frame.IsEstablished || _densified.Count == 0)
            {
                foreach (var arrow in _pool) arrow.SetActive(false);
                return;
            }

            int used = 0;
            for (float d = ArrowSpacingMeters; d <= TrailLengthMeters && used < _pool.Count; d += ArrowSpacingMeters, used++)
            {
                double targetDistance = currentCumulativeDistance + d;
                var wp = SampleAt(targetDistance);
                if (wp == null) break;

                Vector3 approxWorld = _localization.GeoToWorld(wp.Value.Latitude, wp.Value.Longitude);

                // Route bearing at this point: direction toward a sample a little further along
                // the path, not the camera's current facing — a walker doesn't always face the
                // way they're walking (checking a landmark, looking around), so cameraForward is
                // only a last-resort guess now, used solely when the route itself gives no
                // direction (e.g. right at the very end of it).
                var wpAhead = SampleAt(targetDistance + 1.0) ?? wp;
                Vector3 aheadWorld = _localization.GeoToWorld(wpAhead.Value.Latitude, wpAhead.Value.Longitude);
                Vector3 routeForward = aheadWorld - approxWorld;
                routeForward.y = 0f;
                if (routeForward.sqrMagnitude < 0.0001f) routeForward = cameraForward;

                Pose pose = _placement.PlaceAtGroundXZ(approxWorld, routeForward);

                var arrow = _pool[used];
                arrow.SetActive(true);
                arrow.SetPose(pose);

                float alpha = d <= FadeStartMeters ? 1f : Mathf.Clamp01(1f - (d - FadeStartMeters) / (TrailLengthMeters - FadeStartMeters));
                arrow.SetVisual(alpha, wp.Value.IsBridged);
            }

            for (int i = used; i < _pool.Count; i++) _pool[i].SetActive(false);
        }

        private Waypoint? SampleAt(double distance)
        {
            if (distance > _densified[^1].CumulativeDistanceMeters) return null;

            for (int i = 0; i < _densified.Count - 1; i++)
            {
                if (_densified[i + 1].CumulativeDistanceMeters >= distance) return _densified[i];
            }
            return _densified[^1];
        }

        public void Dispose()
        {
            foreach (var arrow in _pool)
                if (arrow != null) Object.Destroy(arrow.gameObject);
        }
    }
}
