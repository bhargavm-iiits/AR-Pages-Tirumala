using System;
using AlipiriAR.Core;
using UnityEngine;

namespace AlipiriAR.Data
{
    /// <summary>Persisted app preferences (PLAN.md §06 — Voice Guidance, Units, Auto Brightness,
    /// Haptic Feedback). PlayerPrefs-backed like ProfileService/VisitedStore.</summary>
    public class SettingsStore
    {
        private const string VoiceGuidanceKey = "settings.voice_guidance";
        private const string UnitsMetricKey = "settings.units_metric";
        private const string AutoBrightnessKey = "settings.auto_brightness";
        private const string HapticFeedbackKey = "settings.haptic_feedback";
        private const string SimulateGpsKey = "settings.simulate_gps";

        public event Action OnUnitsChanged;

        public bool VoiceGuidanceEnabled
        {
            get => PlayerPrefs.GetInt(VoiceGuidanceKey, 1) == 1;
            set { PlayerPrefs.SetInt(VoiceGuidanceKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public bool UnitsMetric
        {
            get => PlayerPrefs.GetInt(UnitsMetricKey, 1) == 1;
            set
            {
                if (UnitsMetric == value) return;
                PlayerPrefs.SetInt(UnitsMetricKey, value ? 1 : 0);
                PlayerPrefs.Save();
                OnUnitsChanged?.Invoke();
            }
        }

        public bool AutoBrightnessEnabled
        {
            get => PlayerPrefs.GetInt(AutoBrightnessKey, 1) == 1;
            set { PlayerPrefs.SetInt(AutoBrightnessKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public bool HapticFeedbackEnabled
        {
            get => PlayerPrefs.GetInt(HapticFeedbackKey, 1) == 1;
            set { PlayerPrefs.SetInt(HapticFeedbackKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Developer toggle — forces the Editor's simulated trace-replay walk on a real
        /// device build instead of real GPS hardware (NavigationSession.Resolve()'s only read of
        /// this). Off by default: a real pilgrim on the real hill should always get real GPS.
        /// Persisted (not just in-memory) since NavigationSession.Resolve() reads it once, early
        /// at next launch — flip it, then relaunch the app, then start navigation.</summary>
        public bool SimulateGps
        {
            get => PlayerPrefs.GetInt(SimulateGpsKey, 0) == 1;
            set { PlayerPrefs.SetInt(SimulateGpsKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static SettingsStore Resolve()
        {
            if (!ServiceLocator.TryGet<SettingsStore>(out var store))
            {
                store = new SettingsStore();
                ServiceLocator.Register(store);
            }
            return store;
        }
    }
}
