using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace AlipiriAR.AR
{
    public enum GroundPlacementTier { Plane = 1, FeaturePoint = 2, Fallback = 3 }

    /// <summary>Three tiers, so an arrow always exists (PLAN.md §09) — raycast straight down
    /// against detected planes first, then feature points, then fall back to a fixed offset
    /// below the camera, so deep shade, motion blur or a phone pitched down never leaves the
    /// walker arrow-less. Uses ARRaycastManager's world-space Ray overload (verified against
    /// this project's installed AR Foundation source) rather than the screen-point overload,
    /// since chevron positions come from known route coordinates, not a tapped screen location.
    /// Device-unverified — see ARSessionBootstrapper's class doc.</summary>
    public class GroundPlacementService
    {
        private const float FallbackHeightBelowCamera = 1.4f;
        private const float RayStartHeightAboveCamera = 2f;

        /// <summary>How far a hit's surface normal (pose.up) may lean from true vertical and
        /// still count as "ground" — steps have a horizontal tread even on a steep staircase, so
        /// this only needs to reject genuinely vertical surfaces (walls, doors), not gentle slopes.
        /// cos(35°) ≈ 0.82. Found on a real device: ARPlaneManager detects vertical planes too by
        /// default (now restricted to Horizontal in ARSessionBootstrapper), and a FeaturePoint hit
        /// near a wall/door edge can still land on that surface regardless of detection mode — this
        /// is the tier-agnostic backstop against both.</summary>
        private const float MinGroundUpDot = 0.82f;

        private readonly ARRaycastManager _raycastManager;
        private readonly Camera _arCamera;
        private readonly List<ARRaycastHit> _hitBuffer = new();

        public GroundPlacementTier LastTier { get; private set; } = GroundPlacementTier.Fallback;

        public GroundPlacementService(ARRaycastManager raycastManager, Camera arCamera)
        {
            _raycastManager = raycastManager;
            _arCamera = arCamera;
        }

        /// <summary>Finds the ground pose at a given world-space (x, _, z) — how
        /// DynamicArrowManager places each chevron in the trail. The y component of
        /// approximateWorldXZ is ignored; the ray starts above the camera and casts down.
        /// routeForward is the actual route bearing at this point (direction toward the next
        /// sample along the path) — used for EVERY tier's yaw now, not just Fallback's. Found on
        /// a real device: ARCore's Plane/FeaturePoint raycast hits carry their own in-plane
        /// rotation, which is arbitrary relative to the walking direction (it's whatever canonical
        /// orientation ARCore assigned that plane's polygon), not the route bearing — using it
        /// directly made chevrons point every which way instead of toward where to walk. Only the
        /// hit's "up" (its real surface normal) is trustworthy; the yaw always comes from the route.</summary>
        public Pose PlaceAtGroundXZ(Vector3 approximateWorldXZ, Vector3 routeForward)
        {
            Vector3 rayOrigin = new Vector3(approximateWorldXZ.x, _arCamera.transform.position.y + RayStartHeightAboveCamera, approximateWorldXZ.z);
            var ray = new Ray(rayOrigin, Vector3.down);

            Vector3 flatForward = routeForward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.0001f) flatForward = _arCamera.transform.forward;
            flatForward.Normalize();

            if (RaycastFirst(ray, TrackableType.PlaneWithinPolygon, out var planePose))
            {
                LastTier = GroundPlacementTier.Plane;
                return new Pose(planePose.position, Quaternion.LookRotation(flatForward, planePose.up));
            }
            if (RaycastFirst(ray, TrackableType.FeaturePoint, out var featurePose))
            {
                LastTier = GroundPlacementTier.FeaturePoint;
                return new Pose(featurePose.position, Quaternion.LookRotation(flatForward, featurePose.up));
            }

            LastTier = GroundPlacementTier.Fallback;
            Vector3 pos = new Vector3(approximateWorldXZ.x, _arCamera.transform.position.y - FallbackHeightBelowCamera, approximateWorldXZ.z);
            return new Pose(pos, Quaternion.LookRotation(flatForward, Vector3.up));
        }

        private bool RaycastFirst(Ray ray, TrackableType type, out Pose pose)
        {
            if (_raycastManager.Raycast(ray, _hitBuffer, type))
            {
                foreach (var hit in _hitBuffer)
                {
                    if (Vector3.Dot(hit.pose.up, Vector3.up) < MinGroundUpDot) continue;
                    pose = hit.pose;
                    return true;
                }
            }
            pose = default;
            return false;
        }
    }
}
