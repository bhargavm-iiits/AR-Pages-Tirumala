using AlipiriAR.Data;
using UnityEngine;

namespace AlipiriAR.Utilities
{
    /// <summary>Gates every haptic pulse behind the Settings switch (PLAN.md §06 — "Gate all
    /// Handheld.Vibrate calls"). Nothing in v1 triggers this yet; the AR turn/arrival overlays
    /// (Scene 6) will call Pulse() rather than Handheld.Vibrate directly.</summary>
    public static class HapticService
    {
        public static void Pulse()
        {
            if (!SettingsStore.Resolve().HapticFeedbackEnabled) return;
            Handheld.Vibrate();
        }
    }
}
