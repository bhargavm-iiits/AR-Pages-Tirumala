using System;
using System.Collections;
using System.Collections.Generic;
using AlipiriAR.Data;
using UnityEngine;

namespace AlipiriAR.Positioning
{
    public enum LocationAccuracyLevel { Good, Degraded, Poor }

    /// <summary>Which position source is actually driving the app. Exposed (rather than kept
    /// private) because the two are indistinguishable downstream by design, and that turned out
    /// to be a liability: with the trace harness wired in as the only source, the whole app
    /// advanced along the route on its own while the phone sat still, and nothing on screen said
    /// why. See Docs/Draft1.md D11.</summary>
    public enum LocationSourceMode { TraceReplay, DeviceGps }

    /// <summary>Lifecycle of the active source. Only <see cref="Live"/> means fixes are actually
    /// arriving — everything else is a state the UI has to say out loud, since a stalled position
    /// source is otherwise indistinguishable from standing perfectly still.</summary>
    public enum LocationSourceStatus { Idle, Acquiring, Live, PermissionDenied, ServicesDisabled, Failed }

    /// <summary>
    /// The one position source every screen consumes (PLAN.md §08/§09 — "all four data-driven
    /// screens now share one scalar and cannot disagree"). Two modes behind the same events:
    /// TraceReplay (default — the desk-testable harness PLAN.md calls the project's
    /// highest-leverage test) and real device GPS via Input.location/Input.compass.
    ///
    /// Input.location (LocationService) and Input.compass are NOT part of the legacy polling
    /// surface (GetKey/GetAxis/mousePosition/touches) that throws when Active Input Handling is
    /// set to "Input System Package (New)" — Input System has no GPS/compass replacement, so
    /// Unity exempts them. Reviewed against Unity's own documentation; not exercised on real
    /// Android hardware in this environment, so treat the device-GPS path (below the trace-replay
    /// section) as reviewed-but-device-unverified until tested on a phone.
    /// </summary>
    public class LocationProvider : MonoBehaviour
    {
        public const float DesiredAccuracyMeters = 5f;
        public const float UpdateDistanceMeters = 1f;
        public const float LowAccuracyThresholdMeters = 25f;

        private readonly GpsKalmanFilter _filter = new();
        private TraceReplaySource _trace;
        private bool _useTrace = true;
        private Coroutine _gpsRoutine;
        private double _lastHeading;
        private LocationAccuracyLevel _lastLevel = LocationAccuracyLevel.Good;
        private LocationSourceStatus _status = LocationSourceStatus.Idle;

        public LocationSourceMode Mode => _useTrace ? LocationSourceMode.TraceReplay : LocationSourceMode.DeviceGps;
        public LocationSourceStatus Status => _status;

        /// <summary>lat, lon (Kalman-filtered), headingDeg, accuracyMeters (raw, unfiltered).</summary>
        public event Action<double, double, float, float> OnFixFiltered;
        public event Action<LocationAccuracyLevel> OnAccuracyLevelChanged;
        public event Action<LocationSourceStatus> OnStatusChanged;

        public static LocationProvider CreateTraceReplay(IReadOnlyList<Waypoint> waypoints)
        {
            var go = new GameObject("~LocationProvider");
            DontDestroyOnLoad(go);
            var provider = go.AddComponent<LocationProvider>();
            provider._useTrace = true;
            provider._trace = TraceReplaySource.Create(waypoints);
            provider._trace.OnPositionChanged += provider.OnTraceFix;
            return provider;
        }

        /// <summary>Real Android GPS/compass, via <see cref="Input.location"/>/<see
        /// cref="Input.compass"/>. Was previously written but never constructable — <see
        /// cref="NavigationSession.Resolve"/> only ever called <see cref="CreateTraceReplay"/>, so
        /// the whole app ran on the simulated walk unconditionally, on-device included. See
        /// Docs/Draft1.md D11.</summary>
        public static LocationProvider CreateDeviceGps()
        {
            var go = new GameObject("~LocationProvider");
            DontDestroyOnLoad(go);
            var provider = go.AddComponent<LocationProvider>();
            provider._useTrace = false;
            return provider;
        }

        public void Play()
        {
            if (_useTrace) { SetStatus(LocationSourceStatus.Acquiring); _trace.Play(); }
            else StartRealGps();
        }

        public void Pause()
        {
            if (_useTrace) _trace.Pause();
            else StopRealGps();
        }

        public bool IsPlaying => _useTrace ? _trace.IsPlaying : _gpsRoutine != null;

        private void SetStatus(LocationSourceStatus status)
        {
            if (_status == status) return;
            _status = status;
            OnStatusChanged?.Invoke(status);
        }

        private void OnTraceFix(double lat, double lon, float headingDeg, double cumulativeDistanceMeters)
        {
            // TraceReplaySource is exact/noise-free; still run it through the same Kalman path
            // real fixes take so downstream code can't tell which source is driving it — that
            // parity is the whole point of the harness.
            const float syntheticAccuracy = 6f;
            _filter.Update(lat, lon, syntheticAccuracy, Time.unscaledTimeAsDouble);
            _lastHeading = headingDeg;
            SetStatus(LocationSourceStatus.Live);
            EmitFix(syntheticAccuracy);
        }

        private void EmitFix(float accuracyMeters)
        {
            OnFixFiltered?.Invoke(_filter.Latitude, _filter.Longitude, (float)_lastHeading, accuracyMeters);

            var level = accuracyMeters <= DesiredAccuracyMeters * 2f ? LocationAccuracyLevel.Good
                : accuracyMeters <= LowAccuracyThresholdMeters ? LocationAccuracyLevel.Degraded
                : LocationAccuracyLevel.Poor;
            if (level != _lastLevel)
            {
                _lastLevel = level;
                OnAccuracyLevelChanged?.Invoke(level);
            }
        }

        // ---- Real device GPS path — device-unverified, see class doc ----

        private void StartRealGps()
        {
            if (_gpsRoutine != null) return;
            SetStatus(LocationSourceStatus.Acquiring);
            _gpsRoutine = StartCoroutine(RealGpsRoutine());
        }

        private void StopRealGps()
        {
            if (_gpsRoutine == null) return;
            StopCoroutine(_gpsRoutine);
            _gpsRoutine = null;
            Input.location.Stop();
            Input.compass.enabled = false;
            SetStatus(LocationSourceStatus.Idle);
        }

        private IEnumerator RealGpsRoutine()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.FineLocation))
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.FineLocation);

            float waited = 0f;
            while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.FineLocation) && waited < 15f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.FineLocation))
            {
                // Previously just a Debug.LogWarning + yield break — invisible to the app, which
                // meant a denied permission looked identical to "standing still": no fix ever
                // arrives, and nothing downstream (nav board, map, debug overlay) could tell the
                // difference. SetStatus makes this a state the UI can show. See Docs/Draft1.md D11.
                Debug.LogWarning("[LocationProvider] Location permission denied.");
                _gpsRoutine = null;
                SetStatus(LocationSourceStatus.PermissionDenied);
                yield break;
            }
#endif

            if (!Input.location.isEnabledByUser)
            {
                Debug.LogWarning("[LocationProvider] Location services disabled by user.");
                _gpsRoutine = null;
                SetStatus(LocationSourceStatus.ServicesDisabled);
                yield break;
            }

            Input.location.Start(DesiredAccuracyMeters, UpdateDistanceMeters);
            Input.compass.enabled = true;

            int maxWait = 20;
            while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
            {
                yield return new WaitForSeconds(1f);
                maxWait--;
            }

            if (Input.location.status != LocationServiceStatus.Running)
            {
                Debug.LogWarning($"[LocationProvider] Location service failed to start: {Input.location.status}");
                _gpsRoutine = null;
                SetStatus(LocationSourceStatus.Failed);
                yield break;
            }

            double lastTimestamp = -1;
            while (true)
            {
                var data = Input.location.lastData;
                if (data.timestamp != lastTimestamp)
                {
                    lastTimestamp = data.timestamp;
                    _lastHeading = Input.compass.enabled ? Input.compass.trueHeading : 0f;
                    _filter.Update(data.latitude, data.longitude, data.horizontalAccuracy, data.timestamp);
                    SetStatus(LocationSourceStatus.Live);
                    EmitFix(data.horizontalAccuracy);
                }
                yield return null;
            }
        }
    }
}
