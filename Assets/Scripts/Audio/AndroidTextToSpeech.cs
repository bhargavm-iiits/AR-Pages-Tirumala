using System;
using UnityEngine;

namespace AlipiriAR.Audio
{
    /// <summary>JNI wrapper around android.speech.tts.TextToSpeech (PLAN.md §11) — checks
    /// isLanguageAvailable() BEFORE committing to a locale, since setLanguage() silently keeps
    /// the previous voice if the requested one has no installed voice data on-device. Written
    /// against the stable, decade-old TextToSpeech API (SUCCESS/QUEUE_FLUSH/LANG_AVAILABLE
    /// constant values below are guaranteed by Android's API stability policy).
    ///
    /// No utterance-progress callback: android.speech.tts.UtteranceProgressListener is an
    /// ABSTRACT CLASS, not an interface — confirmed on a real device via a genuine crash
    /// (IllegalArgumentException: "... is not an interface") from an earlier version of this
    /// file that used AndroidJavaProxy against it, which only works for real Java interfaces.
    /// Subclassing an abstract Java class from C# needs a purpose-built .aar/.jar shim, which
    /// is real native-build work, not a one-line fix — so VoiceNavigationManager estimates
    /// playback duration from text length instead of listening for real start/done events.</summary>
    public class AndroidTextToSpeech
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private const int TtsSuccess = 0;
        private const int QueueFlush = 0;

        private AndroidJavaObject _tts;
        private bool _ready;
        private Action _onReady;

        public void Initialize(Action onReady)
        {
            _onReady = onReady;
            var listener = new TtsInitListener(OnInit);
            var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            _tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, listener);
        }

        private void OnInit(int status)
        {
            _ready = status == TtsSuccess;
            _onReady?.Invoke();
        }

        public bool IsLanguageAvailable(string localeCode)
        {
            if (!_ready) return false;
            var locale = new AndroidJavaObject("java.util.Locale", localeCode);
            int result = _tts.Call<int>("isLanguageAvailable", locale);
            return result >= 0; // LANG_AVAILABLE=0, LANG_COUNTRY_AVAILABLE=1, LANG_COUNTRY_VAR_AVAILABLE=2
        }

        public bool Speak(string text, string localeCode, string utteranceId)
        {
            if (!_ready || string.IsNullOrEmpty(text)) return false;

            var locale = new AndroidJavaObject("java.util.Locale", localeCode);
            _tts.Call<int>("setLanguage", locale);

            var bundle = new AndroidJavaObject("android.os.Bundle");
            int result = _tts.Call<int>("speak", text, QueueFlush, bundle, utteranceId);
            return result == TtsSuccess;
        }

        public void Stop() => _tts?.Call<int>("stop");

        public void Shutdown()
        {
            _tts?.Call("shutdown");
            _tts = null;
        }
#else
        public void Initialize(Action onReady) => onReady?.Invoke();
        public bool IsLanguageAvailable(string localeCode) => false;
        public bool Speak(string text, string localeCode, string utteranceId) => false;
        public void Stop() { }
        public void Shutdown() { }
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>Method name below is lowercase because AndroidJavaProxy dispatches by exact
    /// Java interface method name (onInit) — not a style slip. TextToSpeech.OnInitListener,
    /// unlike UtteranceProgressListener, genuinely is a Java interface, so this one is safe.</summary>
    internal class TtsInitListener : AndroidJavaProxy
    {
        private readonly Action<int> _onInit;

        public TtsInitListener(Action<int> onInit) : base("android.speech.tts.TextToSpeech$OnInitListener")
        {
            _onInit = onInit;
        }

        public void onInit(int status) => _onInit(status);
    }
#endif
}
