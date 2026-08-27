using System;
using System.Collections;
using AlipiriAR.Core;
using AlipiriAR.Data;
using AlipiriAR.Localization;
using UnityEngine;

namespace AlipiriAR.Audio
{
    /// <summary>Clip → TTS → caption fallback chain (PLAN.md §11). No audio clips exist in
    /// Docs — that tier is a documented no-op in v1, not a stub pretending to work — so every
    /// Speak() call either reaches a real Android voice or falls back to the on-screen caption,
    /// both fully functional against the voiceText already in landmarks.json. Muted by
    /// SettingsStore.VoiceGuidanceEnabled, the same gating pattern HapticService uses.
    ///
    /// "Finished" is a time estimate from text length, not a real callback — Android's
    /// UtteranceProgressListener is an abstract class, not an interface, so AndroidJavaProxy
    /// can't implement it (see AndroidTextToSpeech's doc comment; this was a real crash on a
    /// real device before being found and fixed).</summary>
    public class VoiceNavigationManager
    {
        private const float CharsPerSecond = 14f;
        private const float MinDurationSeconds = 1.2f;
        private const float MaxDurationSeconds = 30f;

        private readonly AndroidTextToSpeech _tts = new();
        private bool _ttsReady;
        private int _generation;
        private VoiceDriver _driver;

        public event Action<string> OnCaption; // text to show on screen, regardless of audio outcome
        public event Action OnPlaybackStarted;
        public event Action OnPlaybackFinished;

        private VoiceNavigationManager()
        {
            _tts.Initialize(() => _ttsReady = true);
        }

        /// <summary>Speaks (or captions) one piece of voiceText. audioClipPath is checked first —
        /// always null in v1 since Assets/Docs has no audio files (PLAN.md §04/§11) — the
        /// parameter exists so a future clip library slots in here without changing call sites.</summary>
        public void Speak(string text, string audioClipPath = null)
        {
            if (string.IsNullOrEmpty(text)) return;

            OnCaption?.Invoke(text);
            _generation++; // invalidates any pending auto-finish from a previous Speak()/Stop()

            if (!SettingsStore.Resolve().VoiceGuidanceEnabled) return;

            if (!string.IsNullOrEmpty(audioClipPath))
            {
                // Clip tier — left for a future clip library; nothing to check against today.
            }

            if (_ttsReady)
            {
                // "Degrades to English + caption" (PLAN.md §11) when the current UI locale has
                // no installed voice data on-device.
                string locale = Loc.CurrentLocale;
                string speakLocale = _tts.IsLanguageAvailable(locale) ? locale : "en";
                string utteranceId = Guid.NewGuid().ToString();

                if (_tts.Speak(text, speakLocale, utteranceId))
                {
                    OnPlaybackStarted?.Invoke();
                    float duration = Mathf.Clamp(text.Length / CharsPerSecond, MinDurationSeconds, MaxDurationSeconds);
                    EnsureDriver().StartCoroutine(AutoFinishAfter(duration, _generation));
                    return;
                }
            }

            // No TTS engine at all (Editor, or a device with none installed) — the caption
            // above already fired, so the text is still shown either way.
        }

        public void Stop()
        {
            _generation++; // invalidates any pending auto-finish
            _tts.Stop();
            OnPlaybackFinished?.Invoke();
        }

        private IEnumerator AutoFinishAfter(float seconds, int generation)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (generation == _generation) OnPlaybackFinished?.Invoke();
        }

        private VoiceDriver EnsureDriver()
        {
            if (_driver != null) return _driver;
            var go = new GameObject("~VoiceNavigationManager");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _driver = go.AddComponent<VoiceDriver>();
            return _driver;
        }

        private class VoiceDriver : MonoBehaviour { }

        public static VoiceNavigationManager Resolve()
        {
            if (!ServiceLocator.TryGet<VoiceNavigationManager>(out var manager))
            {
                manager = new VoiceNavigationManager();
                ServiceLocator.Register(manager);
            }
            return manager;
        }
    }
}
