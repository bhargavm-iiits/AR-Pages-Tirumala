using System;
using UnityEngine;

namespace AlipiriAR.Positioning
{
    /// <summary>
    /// Wraps Android's TYPE_PRESSURE sensor as an IPositionSource — Docs/update1.md §01/§05's
    /// "co-primary" signal on this route's 975 m of monotonic ascent. Same JNI pattern as
    /// StepCounterSource (reusing its StepSensorListener proxy — SensorEventListener is generic
    /// over which sensor it's registered against, so one proxy class serves both).
    ///
    /// Emits raw altitude, not chainage — S here is metres above sea level via the standard
    /// barometric formula referenced to sea-level pressure, not an along-track distance. The
    /// elevation-profile inversion (altitude → s) needs route geometry this class has none of,
    /// so PositionFusionService does that step, exactly as it does the stride conversion for
    /// StepCounterSource.
    ///
    /// IsAvailable here reflects hardware presence only. §01 is explicit that this source is
    /// additionally blocked on data — every Waypoint.Elevation is NaN until the Phase 1 survey —
    /// so PositionFusionService gates on route elevation availability before ever starting this,
    /// which is the "conditional registration" §04's failure matrix calls for, just gated on
    /// data instead of hardware.
    /// </summary>
    public class BarometerSource : IPositionSource
    {
        public SourceKind Kind => SourceKind.Barometer;

        public bool IsAvailable { get; private set; }

        public event Action<PositionMeasurement> OnMeasurement;

        private double _seaLevelHpa = 1013.25;

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _sensorManager;
        private AndroidJavaObject _pressureSensor;
        private StepSensorListener _listener;
#endif

        public void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using var activity = GetCurrentActivity();
            _sensorManager = activity.Call<AndroidJavaObject>("getSystemService", "sensor");
            using (var sensorClass = new AndroidJavaClass("android.hardware.Sensor"))
            {
                int typePressure = sensorClass.GetStatic<int>("TYPE_PRESSURE");
                _pressureSensor = _sensorManager.Call<AndroidJavaObject>("getDefaultSensor", typePressure);
            }

            // §04 risk register: "Target devices lack a barometer — loses the strongest offline
            // signal on that device." Roughly a quarter of budget Android phones have none; this
            // is the conditional-registration branch for that, not an edge case worth a crash.
            IsAvailable = _pressureSensor != null;
            if (!IsAvailable) return;

            _listener = new StepSensorListener(OnSensorEvent);
            using var sensorManagerClass = new AndroidJavaClass("android.hardware.SensorManager");
            int delayGame = sensorManagerClass.GetStatic<int>("SENSOR_DELAY_GAME");
            _sensorManager.Call<bool>("registerListener", _listener, _pressureSensor, delayGame);
#else
            IsAvailable = false;
#endif
        }

        public void Stop()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_sensorManager != null && _listener != null)
                _sensorManager.Call("unregisterListener", _listener);
#endif
        }

        /// <summary>Re-zeroes the sea-level reference from a known altitude — call at every
        /// checkpoint fix (§05: "re-solve b from checkpoint.ele") to keep synoptic weather drift
        /// from accumulating across the climb.</summary>
        public void CalibrateAtKnownAltitude(double knownAltitudeMeters, double currentPressureHpa)
        {
            _seaLevelHpa = currentPressureHpa / Math.Pow(1.0 - knownAltitudeMeters / 44330.0, 5.255);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void OnSensorEvent(AndroidJavaObject sensorEvent)
        {
            float[] values = sensorEvent.Get<float[]>("values");
            if (values == null || values.Length == 0) return;

            double pressureHpa = values[0];
            double altitudeMeters = 44330.0 * (1.0 - Math.Pow(pressureHpa / _seaLevelHpa, 1.0 / 5.255));

            // Sigma left at 0 for the same reason as StepCounterSource's raw delta — the real
            // sigma (σ_alt / grade) needs the local grade, which PositionFusionService looks up.
            OnMeasurement?.Invoke(new PositionMeasurement(
                altitudeMeters, 0f, null, null, Time.unscaledTimeAsDouble, SourceKind.Barometer));
        }

        private static AndroidJavaObject GetCurrentActivity()
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        }
#endif
    }
}
