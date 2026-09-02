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

        /// <summary>Continuous breathing scale loop — the chevron trail's traveling pulse and any
        /// future "this is live/active" indicator use this rather than each hand-rolling their own
        /// sine timer. Runs on the shared runner (not the target), so it keeps going even if the
        /// caller doesn't hold a MonoBehaviour of its own; call StopPulse with the returned handle
        /// to end it (destroying the target GameObject also silently stops it via the null guard).</summary>
        public static Coroutine Pulse(Transform t, float minScale, float maxScale, float period)
        {
            return EnsureRunner().StartCoroutine(PulseRoutine(t, minScale, maxScale, period));
        }

        public static void StopPulse(Coroutine handle)
        {
            if (handle == null) return;
            EnsureRunner().StopCoroutine(handle);
        }

        /// <summary>One-shot horizontal shake — low-GPS warning and any other "something's wrong,
        /// look here" moment. Decaying sine, not a fixed back-and-forth, so it settles rather than
        /// stopping abruptly on the last beat.</summary>
        public static void ShakeX(RectTransform rt, float amplitude = 14f, float duration = 0.4f, int cycles = 4)
        {
            if (ReducedMotion) return;
            EnsureRunner().StartCoroutine(ShakeRoutine(rt, amplitude, duration, cycles));
        }

        /// <summary>Digit-by-digit count instead of a snap — the step badge and any other integer
        /// readout. Rounds the underlying float tween per frame so it always lands exactly on
        /// `to`.</summary>
        public static void CountInt(Action<int> setter, int from, int to, float duration = 0.5f)
        {
            EnsureRunner().StartCoroutine(FloatRoutine(v => setter(Mathf.RoundToInt(v)), from, to, duration));
        }

        private static IEnumerator PulseRoutine(Transform t, float minScale, float maxScale, float period)
        {
            float clock = 0f;
            while (true)
            {
                if (t == null) yield break;
                if (ReducedMotion)
                {
                    t.localScale = Vector3.one * ((minScale + maxScale) * 0.5f);
                    yield return null;
                    continue;
                }
                clock += Time.unscaledDeltaTime;
                float wave = (Mathf.Sin(clock * (Mathf.PI * 2f) / period) + 1f) * 0.5f;
                t.localScale = Vector3.one * Mathf.Lerp(minScale, maxScale, wave);
                yield return null;
            }
        }

        private static IEnumerator ShakeRoutine(RectTransform rt, float amplitude, float duration, int cycles)
        {
            Vector2 basePos = rt.anchoredPosition;
            float t = 0f;
            while (t < duration)
            {
                if (rt == null) yield break;
                t += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(t / duration);
                float decay = 1f - progress;
                float offset = Mathf.Sin(progress * cycles * Mathf.PI * 2f) * amplitude * decay;
                rt.anchoredPosition = basePos + new Vector2(offset, 0f);
                yield return null;
            }
            if (rt == null) yield break;
            rt.anchoredPosition = basePos;
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
