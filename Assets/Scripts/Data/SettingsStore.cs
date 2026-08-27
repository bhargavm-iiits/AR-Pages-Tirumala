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
