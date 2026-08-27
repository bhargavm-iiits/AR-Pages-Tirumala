using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AlipiriAR.UI
{
    /// <summary>The confetti burst on the Arrival overlay (screen 8). Plain UI Images animated
    /// by a coroutine — no ParticleSystem, so it works unmodified inside a Screen Space Overlay
    /// canvas alongside the rest of the procedural UI.</summary>
    public class ConfettiBurst : MonoBehaviour
    {
        private static readonly Color[] Palette =
        {
            UITheme.Success, UITheme.Accent, UITheme.Warning, UITheme.Gold, Color.white
        };

        public static void Play(RectTransform parent, int count = 26)
        {
            var host = new GameObject("ConfettiBurst", typeof(RectTransform));
            var rt = (RectTransform)host.transform;
            rt.SetParent(parent, false);
            UIFactory.StretchFill(rt);
            host.AddComponent<ConfettiBurst>().StartCoroutine(Emit(rt, count));
        }

        private static IEnumerator Emit(RectTransform root, int count)
        {
            var particles = new List<(RectTransform rt, Image img, Vector2 vel, float spin, float life)>(count);

            for (int i = 0; i < count; i++)
            {
                var pRt = UIFactory.CreateRect("Confetti", root);
                UIFactory.SetSize(pRt, Random.Range(8f, 16f), Random.Range(8f, 16f));
                pRt.anchoredPosition = Vector2.zero;
                pRt.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

                var img = pRt.gameObject.AddComponent<Image>();
                img.sprite = Random.value > 0.5f ? UIShapes.Circle() : UIShapes.RoundedRect(3);
                img.color = Palette[Random.Range(0, Palette.Length)];

                float angle = Random.Range(0f, Mathf.PI * 2f);
                float speed = Random.Range(220f, 460f);
                var vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.6f + 0.6f) * speed;

                particles.Add((pRt, img, vel, Random.Range(-260f, 260f), Random.Range(0.9f, 1.4f)));
            }

            const float gravity = -900f;
            float t = 0f;
            float maxLife = 1.4f;

            while (t < maxLife)
            {
                t += Time.unscaledDeltaTime;
                for (int i = 0; i < particles.Count; i++)
                {
                    var pt = particles[i];
                    if (t > pt.life) continue;

                    var vel = pt.vel + new Vector2(0f, gravity * t);
                    pt.rt.anchoredPosition += vel * Time.unscaledDeltaTime;
                    pt.rt.Rotate(0f, 0f, pt.spin * Time.unscaledDeltaTime);

                    float fadeT = Mathf.Clamp01((t - pt.life * 0.6f) / (pt.life * 0.4f));
                    var c = pt.img.color;
                    c.a = 1f - fadeT;
                    pt.img.color = c;
                }
                yield return null;
            }

            Destroy(root.gameObject);
        }
    }
}
