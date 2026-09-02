using AlipiriAR.UI;
using UnityEngine;

namespace AlipiriAR.AR
{
    /// <summary>One chevron in the trail — fade by distance, amber tint across an unsurveyed
    /// bridge section (PLAN.md §09/§01). Flat on the ground, SpriteRenderer-based (no custom
    /// shader/material authoring, which would be an extra unverifiable risk here) — visually
    /// unverified beyond compiling, since it can only be seen through a running AR session.</summary>
    public class NavigationArrow : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private static Sprite _sprite;

        public static NavigationArrow Create(Transform parent)
        {
            var go = new GameObject("NavigationArrow", typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            var arrow = go.AddComponent<NavigationArrow>();
            arrow._renderer = go.GetComponent<SpriteRenderer>();
            arrow._renderer.sprite = GetSprite();
            arrow.transform.localScale = Vector3.one * 1.6f;
            return arrow;
        }

        private bool _hasPose;

        /// <summary>Lays the chevron flat on the ground, pointing along the pose's forward
        /// direction — pose.rotation comes from GroundPlacementService (a surface orientation),
        /// rotated flat into the ground plane instead of standing upright. Found on a real device:
        /// the sprite's tip sits at local -Y (GetSprite draws the two strokes converging below
        /// center, arms opening upward). Quaternion.Euler(90,0,0) maps local +Y (the open end) onto
        /// pose.rotation's forward axis, which puts the actual tip facing backward — chevrons
        /// pointed the way the walker came from instead of where the route goes next. -90 instead
        /// of 90 maps local -Y (the tip) onto forward.
        ///
        /// Eases toward the target each call rather than snapping — DynamicArrowManager.Refresh
        /// recomputes every pooled arrow's pose from a fresh ground raycast every frame, and a
        /// hard set here means any single frame's raycast noise (a plane's normal estimate
        /// wobbling by a fraction of a degree) shows up directly as a visibly twitchy chevron.
        /// Snaps instead of easing the first time a pooled arrow is (re)activated — SetActive
        /// resets _hasPose — so a chevron entering the trail doesn't visibly slide in from
        /// wherever the pool object happened to be left.</summary>
        public void SetPose(Pose pose)
        {
            Quaternion targetRot = pose.rotation * Quaternion.Euler(-90f, 0f, 0f);

            if (!_hasPose || UITween.ReducedMotion)
            {
                transform.SetPositionAndRotation(pose.position, targetRot);
                _hasPose = true;
                return;
            }

            const float smoothing = 18f; // higher = snappier; tuned so a genuine step forward
                                          // (route position actually advancing) still tracks
                                          // within a couple of frames, not several seconds.
            float t = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, pose.position, t),
                Quaternion.Slerp(transform.rotation, targetRot, t));
        }

        public void SetVisual(float alpha, bool bridged)
        {
            var c = bridged ? UITheme.Warning : UITheme.Accent;
            c.a = alpha;
            _renderer.color = c;
        }

        public void SetActive(bool active)
        {
            if (!active) _hasPose = false; // next activation snaps instead of sliding in
            gameObject.SetActive(active);
        }

        private static Sprite GetSprite()
        {
            if (_sprite != null) return _sprite;

            const int size = 96;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            var c = new Vector2(size / 2f, size / 2f);
            float r = size * 0.32f;
            const float t = 10f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float a1 = UIShapes.StrokeAlpha(p, c + new Vector2(-r, r * 0.4f), c + new Vector2(0, -r * 0.6f), t);
                    float a2 = UIShapes.StrokeAlpha(p, c + new Vector2(r, r * 0.4f), c + new Vector2(0, -r * 0.6f), t);
                    float a = Mathf.Max(a1, a2);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            _sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _sprite;
        }
    }
}
