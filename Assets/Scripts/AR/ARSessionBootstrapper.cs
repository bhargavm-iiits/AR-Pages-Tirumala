using System;
using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace AlipiriAR.AR
{
    public enum ArAvailabilityState { Checking, Ready, Unsupported, NeedsInstall, Installing, Failed }

    /// <summary>
    /// Builds the AR session hierarchy in code — no prefab, matching this project's whole
    /// procedural-construction approach — replicating exactly the structure Unity's own
    /// "GameObject ▸ XR ▸ XR Origin (Mobile AR)" editor command creates. Verified against this
    /// project's actual installed package source
    /// (Library/PackageCache/com.unity.xr.arfoundation.../Editor/ARFoundation/XROriginCreateUtil.cs)
    /// rather than written from memory, since a wrong hierarchy here (missing TrackedPoseDriver
    /// bindings, wrong component placement) is exactly the kind of mistake a compile-check can't
    /// catch — everything compiles either way, only a real device tells you if tracking works.
    ///
    /// Deliberately NOT parented under the UI Canvas — the AR camera renders real 3D content
    /// and must live as its own object graph; the Screen Space Overlay canvas (all of this app's
    /// UI) draws on top of whatever the camera sees, which is exactly the layering the mockups
    /// show (full-bleed camera behind, glass chrome in front) and needs no coordination between
    /// the two beyond that draw order.
    ///
    /// This is the one subsystem in the whole app that cannot be exercised at all outside a real
    /// ARCore-capable Android device — no AR simulation package is installed, so in Unity Editor
    /// Play Mode ARSession.state can only ever resolve to Unsupported here. Written against AR
    /// Foundation 6.5's verified API surface and compiles cleanly; the AR-session-running path
    /// itself is device-unverified.
    /// </summary>
    public class ARSessionBootstrapper : MonoBehaviour
    {
        public ArAvailabilityState State { get; private set; } = ArAvailabilityState.Checking;
        public XROrigin Origin { get; private set; }
        public Camera ArCamera { get; private set; }
        public ARSession Session { get; private set; }
        public ARPlaneManager PlaneManager { get; private set; }
        public ARRaycastManager RaycastManager { get; private set; }

        public event Action<ArAvailabilityState> OnStateChanged;

        public static ARSessionBootstrapper Create()
        {
            var go = new GameObject("~ARSessionBootstrapper");
            DontDestroyOnLoad(go);
            var bootstrapper = go.AddComponent<ARSessionBootstrapper>();
            bootstrapper.StartCoroutine(bootstrapper.InitializeRoutine());
            return bootstrapper;
        }

        private IEnumerator InitializeRoutine()
        {
            SetState(ArAvailabilityState.Checking);

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
                float waited = 0f;
                while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera) && waited < 15f)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }
                if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
                {
                    SetState(ArAvailabilityState.Failed);
                    yield break;
                }
            }
#endif

            yield return ARSession.CheckAvailability();

            if (ARSession.state == ARSessionState.Unsupported)
            {
                SetState(ArAvailabilityState.Unsupported);
                yield break;
            }

            if (ARSession.state == ARSessionState.NeedsInstall)
            {
                SetState(ArAvailabilityState.Installing);
                yield return ARSession.Install();
                if (ARSession.state != ARSessionState.Ready)
                {
                    SetState(ArAvailabilityState.Failed);
                    yield break;
                }
            }

            BuildHierarchy();
            SetState(ArAvailabilityState.Ready);
        }

        private void BuildHierarchy()
        {
            var sessionGo = new GameObject("AR Session", typeof(ARSession), typeof(ARInputManager));
            sessionGo.transform.SetParent(transform, false);
            Session = sessionGo.GetComponent<ARSession>();

            var originGo = new GameObject("XR Origin", typeof(XROrigin));
            originGo.transform.SetParent(transform, false);
            Origin = originGo.GetComponent<XROrigin>();

            var offsetGo = new GameObject("Camera Offset");
            offsetGo.transform.SetParent(originGo.transform, false);

            var cameraGo = new GameObject("AR Camera", typeof(Camera), typeof(ARCameraManager), typeof(ARCameraBackground), typeof(TrackedPoseDriver));
            cameraGo.transform.SetParent(offsetGo.transform, false);

            ArCamera = cameraGo.GetComponent<Camera>();
            ArCamera.clearFlags = CameraClearFlags.Color;
            ArCamera.backgroundColor = Color.black;
            ArCamera.nearClipPlane = 0.1f;
            // 20m used to clip the chevron trail's own far end (DynamicArrowManager.TrailLengthMeters
            // reaches further than that), invisibly cutting the "arrows all along the path" the
            // walker actually sees short of where the trail logic thought it went.
            ArCamera.farClipPlane = 150f;

            var trackedPoseDriver = cameraGo.GetComponent<TrackedPoseDriver>();
            var positionAction = new InputAction("Position", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
            positionAction.AddBinding("<HandheldARInputDevice>/devicePosition");
            var rotationAction = new InputAction("Rotation", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
            rotationAction.AddBinding("<HandheldARInputDevice>/deviceRotation");
            trackedPoseDriver.positionInput = new InputActionProperty(positionAction);
            trackedPoseDriver.rotationInput = new InputActionProperty(rotationAction);

            Origin.CameraFloorOffsetObject = offsetGo;
            Origin.Camera = ArCamera;

            PlaneManager = originGo.AddComponent<ARPlaneManager>();
            // Default requestedDetectionMode is every bit set (Horizontal | Vertical |
            // NotAxisAligned) — confirmed on a real device: a door got tracked as a plane, and
            // since GroundPlacementService's raycast doesn't care about a plane's own alignment,
            // arrows climbed straight up the door instead of sitting on the floor. This app only
            // ever wants walkable ground/steps, never walls, so vertical detection is pure risk.
            PlaneManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            RaycastManager = originGo.AddComponent<ARRaycastManager>();
        }

        private void SetState(ArAvailabilityState state)
        {
            State = state;
            OnStateChanged?.Invoke(state);
        }

        private void OnDestroy()
        {
            if (Session != null) Destroy(Session.gameObject);
            if (Origin != null) Destroy(Origin.gameObject);
        }
    }
}
