using UnityEngine;

namespace AlipiriAR.AR
{
    /// <summary>Holds the transform between the AR session's local space and geographic space
    /// (PLAN.md §09) — absorbs GPS/route-snap drift correction by nudging this frame rather than
    /// teleporting arrows, so corrections stay invisible to the walker. Device-unverified — see
    /// ARSessionBootstrapper's class doc.</summary>
    public class GeoAnchorFrame
    {
        /// <summary>Fraction of each frame's position discrepancy absorbed per correction —
        /// small enough that a nudge is imperceptible, large enough to converge in a few fixes.</summary>
        private const float NudgeFactor = 0.06f;

        /// <summary>Fraction of the compass/AR-yaw heading discrepancy absorbed per fix — much
        /// slower than NudgeFactor because a heading correction swings every chevron in the trail
        /// about the anchor at once, not just the anchor's own position.</summary>
        private const float HeadingNudgeFactor = 0.02f;

        private double _originLat, _originLon;
        private Vector3 _originArPos;
        private float _headingOffsetDeg; // AR-local yaw that corresponds to geographic north

        public bool IsEstablished { get; private set; }

        /// <summary>Clears IsEstablished without touching the stored origin/offset — the next
        /// HybridLocalizationEngine.FeedFix re-establishes from scratch exactly as it does on
        /// first launch. Call this whenever the AR session's own local space may have been torn
        /// down and rebuilt (ARSessionBootstrapper's OnApplicationPause) — backgrounding
        /// invalidates ARCore's tracking origin, but without this call IsEstablished stayed true
        /// through a pause/resume, so every arrow placed after resuming was positioned against a
        /// coordinate space that no longer existed (Docs/update1.md §02 F-08/Phase 0 item 4).</summary>
        public void Invalidate() => IsEstablished = false;

        /// <summary>Called once, the first time a confident GPS+compass fix and the AR camera's
        /// own tracked pose are both available — anchors the two coordinate spaces together.</summary>
        public void Establish(double lat, double lon, float compassHeadingDeg, Vector3 arCameraPosition, float arCameraYawDeg)
        {
            _originLat = lat;
            _originLon = lon;
            _originArPos = arCameraPosition;
            _headingOffsetDeg = compassHeadingDeg - arCameraYawDeg;
            IsEstablished = true;
        }

        /// <summary>Converts a lat/lon to AR-local world space using the current frame. Local
        /// planar approximation — fine at route scale, consistent with PolylineUtility's own.</summary>
        public Vector3 GeoToWorld(double lat, double lon)
        {
            double mPerDegLat = 111320.0;
            double mPerDegLon = 111320.0 * System.Math.Cos(_originLat * Mathf.Deg2Rad);
            float north = (float)((lat - _originLat) * mPerDegLat);
            float east = (float)((lon - _originLon) * mPerDegLon);

            float headingRad = _headingOffsetDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(headingRad), sin = Mathf.Sin(headingRad);
            float localX = east * cos - north * sin;
            float localZ = east * sin + north * cos;

            return _originArPos + new Vector3(localX, 0f, localZ);
        }

        /// <summary>Nudges the frame toward matching a fresh confident fix instead of snapping —
        /// corrections stay invisible to the walker (PLAN.md §09's whole point of this class).</summary>
        public void Nudge(double lat, double lon, Vector3 arCameraPosition)
        {
            if (!IsEstablished) return;

            Vector3 expectedWorldPos = GeoToWorld(lat, lon);
            Vector3 discrepancy = arCameraPosition - expectedWorldPos;
            _originArPos += discrepancy * NudgeFactor;
        }

        /// <summary>Slowly rotates the frame's heading offset toward what the compass and AR yaw
        /// currently imply, instead of Establish's one-time capture — so a magnetometer reading
        /// that happened to be off at seed time doesn't stay wrong for the rest of the session.
        /// DeltaAngle keeps the correction shortest-path across the 359°→0° wrap.</summary>
        public void NudgeHeading(float compassHeadingDeg, float arCameraYawDeg)
        {
            if (!IsEstablished) return;

            float targetOffsetDeg = compassHeadingDeg - arCameraYawDeg;
            float deltaDeg = Mathf.DeltaAngle(_headingOffsetDeg, targetOffsetDeg);
            _headingOffsetDeg += deltaDeg * HeadingNudgeFactor;
        }

        /// <summary>How far a fix's implied position sits from where the AR camera actually is
        /// right now — the disagreement HybridLocalizationEngine gates outlier rejection on.
        /// Horizontal only: GroundPlacementService's raycast tiers own vertical placement
        /// independently of this frame, so a Y discrepancy here isn't meaningful disagreement.</summary>
        public float ResidualMeters(double lat, double lon, Vector3 arCameraPosition)
        {
            Vector3 expectedWorldPos = GeoToWorld(lat, lon);
            Vector3 discrepancy = arCameraPosition - expectedWorldPos;
            discrepancy.y = 0f;
            return discrepancy.magnitude;
        }
    }
}
