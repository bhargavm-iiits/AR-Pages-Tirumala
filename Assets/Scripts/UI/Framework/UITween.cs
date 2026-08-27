using System;
using System.Collections;
using UnityEngine;

namespace AlipiriAR.UI
{
    /// <summary>Lightweight fade/scale tweens for overlays and card transitions.
    /// No external dependency — a handful of coroutines is all 12 screens need.</summary>
    public static class UITween
    {
        /// <summary>Set true to make every tween instant (accessibility / low-power mode).</summary>
        public static bool ReducedMotion = false;

        private static Runner _runner;

        private static Runner EnsureRunner()
        {
            if (_runner != null) return _runner;
            var go = new GameObject("~UITweenRunner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<Runner>();
            return _runner;
        }

        public static void FadeIn(CanvasGroup group, float duration = 0.18f, Action onComplete = null)
        {
            group.alpha = 0f;
            group.gameObject.SetActive(true);
            EnsureRunner().StartCoroutine(FadeRoutine(group, 0f, 1f, duration, onComplete));
        }

        public static void FadeOut(CanvasGroup group, float duration = 0.15f, Action onComplete = null)
        {
            EnsureRunner().StartCoroutine(FadeRoutine(group, group.alpha, 0f, duration, () =>
            {
                group.gameObject.SetActive(false);
                onComplete?.Invoke();
            }));
        }

        /// <summary>Scale-and-fade entrance used by modal overlays (landmark popup, arrival card, etc).</summary>
        public static void PopIn(RectTransform rt, CanvasGroup group, float duration = 0.22f)
        {
            rt.localScale = Vector3.one * 0.92f;
            group.alpha = 0f;
            group.gameObject.SetActive(true);
            EnsureRunner().StartCoroutine(PopRoutine(rt, group, duration));
        }

        public static void SlideProgress(Action<float> setFillAmount, float from, float to, float duration = 0.4f)
        {
            EnsureRunner().StartCoroutine(FloatRoutine(setFillAmount, from, to, duration));
        }

        private static IEnumerator FadeRoutine(CanvasGroup group, float from, float to, float duration, Action onComplete)
        {
            if (ReducedMotion || duration <= 0f)
            {
                group.alpha = to;
                onComplete?.Invoke();
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                // The runner is DontDestroyOnLoad, so this outlives whatever it's animating —
                // closing the popup/card mid-tween destroys group's GameObject while this
                // coroutine keeps ticking, and the next write below throws MissingReferenceException.
                if (group == null) yield break;
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, EaseOutCubic(Mathf.Clamp01(t / duration)));
                yield return null;
            }
            if (group == null) yield break;
            group.alpha = to;
            onComplete?.Invoke();
        }

        private static IEnumerator PopRoutine(RectTransform rt, CanvasGroup group, float duration)
        {
            if (ReducedMotion || duration <= 0f)
            {
                rt.localScale = Vector3.one;
                group.alpha = 1f;
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                if (rt == null || group == null) yield break;
                t += Time.unscaledDeltaTime;
                float e = EaseOutCubic(Mathf.Clamp01(t / duration));
                rt.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, e);
                group.alpha = e;
                yield return null;
            }
            if (rt == null || group == null) yield break;
            rt.localScale = Vector3.one;
            group.alpha = 1f;
        }

        private static IEnumerator FloatRoutine(Action<float> setter, float from, float to, float duration)
        {
            if (ReducedMotion || duration <= 0f)
            {
                setter(to);
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                setter(Mathf.Lerp(from, to, EaseOutCubic(Mathf.Clamp01(t / duration))));
                yield return null;
            }
            setter(to);
        }

        // FloatRoutine's setter is a plain Action<float>, not a UnityEngine.Object reference —
        // there's no destroyed-object handle to guard against here the way Fade/PopRoutine need.

        private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

        private class Runner : MonoBehaviour { }
    }
}
