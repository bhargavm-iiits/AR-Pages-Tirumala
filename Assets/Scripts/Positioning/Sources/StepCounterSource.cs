using System;
using UnityEngine;

namespace AlipiriAR.Positioning
{
#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>Same AndroidJavaProxy pattern as Audio/AndroidTextToSpeech.cs's TtsInitListener —
    /// android.hardware.SensorEventListener genuinely is a Java interface (unlike
    /// UtteranceProgressListener, see that file's class doc), so a proxy is safe here. Method
    /// names are public and exactly match the Java interface — AndroidJavaProxy dispatches by
    /// exact name via reflection, which only finds public methods.</summary>
    internal class StepSensorListener : AndroidJavaProxy
    {
        private readonly Action<AndroidJavaObject> _onSensorChanged;

        public StepSensorListener(Action<AndroidJavaObject> onSensorChanged)
            : base("android.hardware.SensorEventListener")
        {
            _onSensorChanged = onSensorChanged;
        }

        public void onSensorChanged(AndroidJavaObject sensorEvent) => _onSensorChanged?.Invoke(sensorEvent);

        public void onAccuracyChanged(AndroidJavaObject sensor, int accuracy)
        {
        }
    }
#endif

    /// <summary>
    /// Wraps Android's TYPE_STEP_COUNTER (cumulative steps since last boot) as an
    /// IPositionSource — Docs/update1.md §05/Phase 3 item 3. Converts each delta to metres via
    /// the estimator's per-grade stride constants (the grade lookup is supplied by
    /// PositionFusionService, since this class has no route knowledge of its own) and emits
    /// DeltaChainage-flavoured measurements — S here is a delta the caller adds to the current
    /// estimate, not an absolute chainage; see PositionFusionService.HandleStepCounterMeasurement.
    ///
    /// Requires android.permission.ACTIVITY_RECOGNITION on API 29+ (declared in
    /// Assets/Plugins/Android/AndroidManifest.xml) — requested at runtime here, same pattern
    /// LocationProvider already uses for FineLocation.
    /// </summary>
    public class StepCounterSource : IPositionSource
    {
        private const string ActivityRecognitionPermission = "android.permission.ACTIVITY_RECOGNITION";

        public SourceKind Kind => SourceKind.StepCounter;

        public bool IsAvailable { get; private set; }

        public event Action<PositionMeasurement> OnMeasurement;

        private double? _baselineCumulativeSteps;

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _sensorManager;
        private AndroidJavaObject _stepSensor;
        private StepSensorListener _listener;
#endif

        public void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(ActivityRecognitionPermission))
                UnityEngine.Android.Permission.RequestUserPermission(ActivityRecognitionPermission);
            // Not gated on the grant here (unlike LocationProvider's blocking wait) — a denial
            // just means the sensor registration below never fires and IsAvailable stays false,
            // which is exactly "sensor absent" as far as PositionFusionService is concerned
            // (§04: "Sensor absent — sources register conditionally... never assume a sensor
            // exists"). No reason to block Start() on a permission prompt for one of several
            // sources.

            using var activity = GetCurrentActivity();
            _sensorManager = activity.Call<AndroidJavaObject>("getSystemService", "sensor");
            using (var sensorClass = new AndroidJavaClass("android.hardware.Sensor"))
            {
                int typeStepCounter = sensorClass.GetStatic<int>("TYPE_STEP_COUNTER");
                _stepSensor = _sensorManager.Call<AndroidJavaObject>("getDefaultSensor", typeStepCounter);
            }

            IsAvailable = _stepSensor != null
                && UnityEngine.Android.Permission.HasUserAuthorizedPermission(ActivityRecognitionPermission);
            if (!IsAvailable) return;

            _listener = new StepSensorListener(OnSensorEvent);
            using var sensorManagerClass = new AndroidJavaClass("android.hardware.SensorManager");
            int delayNormal = sensorManagerClass.GetStatic<int>("SENSOR_DELAY_NORMAL");
            _sensorManager.Call<bool>("registerListener", _listener, _stepSensor, delayNormal);
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

#if UNITY_ANDROID && !UNITY_EDITOR
        private void OnSensorEvent(AndroidJavaObject sensorEvent)
        {
            float[] values = sensorEvent.Get<float[]>("values");
            if (values == null || values.Length == 0) return;

            double cumulativeSteps = values[0];
            if (_baselineCumulativeSteps == null)
            {
                _baselineCumulativeSteps = cumulativeSteps;
                return;
            }

            double deltaSteps = cumulativeSteps - _baselineCumulativeSteps.Value;
            if (deltaSteps <= 0) return; // TYPE_STEP_COUNTER is monotonic; a non-positive delta is a re-baseline artefact, not backward walking

            _baselineCumulativeSteps = cumulativeSteps;

            // S carries the raw step delta, not a metre delta — PositionFusionService owns the
            // per-grade stride conversion (it has the route; this class deliberately doesn't).
            // Sigma is meaningless here for the same reason and left at 0; the fusion service
            // computes the real sigma from AlongTrackEstimator.PredictSteps's own miscount model.
            OnMeasurement?.Invoke(new PositionMeasurement(
                deltaSteps, 0f, null, null, Time.unscaledTimeAsDouble, SourceKind.StepCounter));
        }

        private static AndroidJavaObject GetCurrentActivity()
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        }
#endif
    }
}
