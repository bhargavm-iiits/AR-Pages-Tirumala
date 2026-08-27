using System.Text;
using AlipiriAR.AR;
using AlipiriAR.Positioning;
using AlipiriAR.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlipiriAR.Diagnostics
{
    /// <summary>On-device field-test readout (NewPlan.md §04 Phase B) — surfaces exactly what
    /// Phase D's first device walk needs to tell "working" from "diverged": GPS residual against
    /// the tracked AR pose, consecutive-rejection count, which ground-placement tier is active,
    /// AR session state, fps, battery. Hidden by default — a Settings switch shows it, since it's
    /// meaningless clutter for an actual pilgrim. Also hosts the GpsTraceRecorder start/stop
    /// control, since the two instruments are always used together on a field walk. Lives in
    /// UIRoot's OverlayContainer rather than inside ARNavigationScreen so it keeps recording and
    /// reading real data regardless of which tab is active — a field tester glancing at Progress
    /// or Map mid-walk shouldn't blind the trace.</summary>
    public class DebugOverlay : MonoBehaviour
    {
        private NavigationSession _session;
        private GpsTraceRecorder _trace;
        private ARSessionBootstrapper _bootstrapper;
        private HybridLocalizationEngine _localization;
        private GroundPlacementService _placement;

        private CanvasGroup _group;
        private TMP_Text _readout;
        private TMP_Text _recordLabel;
        private Image _recordPill;

        private float _fpsAccum;
        private int _fpsFrames;
        private float _fps;

        public bool IsVisible { get; private set; }

        public static DebugOverlay Create(RectTransform parent, NavigationSession session, GpsTraceRecorder trace)
        {
            var go = new GameObject("DebugOverlay", typeof(RectTransform));
            var overlay = go.AddComponent<DebugOverlay>();
            overlay._session = session;
            overlay._trace = trace;
            overlay.Build(parent);
            return overlay;
        }

        /// <summary>Called right after ARSessionBootstrapper.Create() — so Checking/Unsupported/
        /// Failed states show up even before the Ready-only components below exist.</summary>
        public void AttachBootstrapper(ARSessionBootstrapper bootstrapper) => _bootstrapper = bootstrapper;

        /// <summary>Called once AR state reaches Ready, when ARNavigationScreen constructs its
        /// placement/localization services.</summary>
        public void AttachAr(ARSessionBootstrapper bootstrapper, HybridLocalizationEngine localization, GroundPlacementService placement)
        {
            _bootstrapper = bootstrapper;
            _localization = localization;
            _placement = placement;
        }

        public void SetVisible(bool visible)
        {
            IsVisible = visible;
            _group.alpha = visible ? 1f : 0f;
            _group.interactable = visible;
            _group.blocksRaycasts = visible;
        }

        private void Build(Transform parent)
        {
            var rt = (RectTransform)transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(UITheme.SpaceM, -UITheme.SpaceM);
            rt.sizeDelta = new Vector2(640f, 0f);

            var bg = gameObject.AddComponent<Image>();
            bg.sprite = UIShapes.RoundedRect(Mathf.RoundToInt(UITheme.RadiusCard));
            bg.type = Image.Type.Sliced;
            // Same translucent-over-camera treatment as the AR HUD's own chrome (top pill,
            // bottom stat card) — this panel sits over the same live feed.
            bg.color = UITheme.Glass;

            _group = gameObject.AddComponent<CanvasGroup>();

            var vlg = gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset((int)UITheme.SpaceM, (int)UITheme.SpaceM, (int)UITheme.SpaceS, (int)UITheme.SpaceS);
            vlg.spacing = UITheme.SpaceS;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            var csf = gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var readoutRt = UIFactory.CreateRect("Readout", rt);
            readoutRt.gameObject.AddComponent<LayoutElement>().minHeight = 200f;
            var readoutCsf = readoutRt.gameObject.AddComponent<ContentSizeFitter>();
            readoutCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _readout = readoutRt.gameObject.AddComponent<TextMeshProUGUI>();
            _readout.fontSize = UITheme.LabelFontSize;
            _readout.alignment = TextAlignmentOptions.TopLeft;
            _readout.color = UITheme.TextPrimary;
            _readout.raycastTarget = false;
            // A fixed height with wrapping off overflowed the panel whenever LastReseedReason
            // carried a long sentence (e.g. "GPS repeatedly disagreed with the tracked pose") —
            // wrapping plus a size fitter that grows with content instead of a guessed constant
            // keeps every line inside the rounded-rect background regardless of message length.
            _readout.enableWordWrapping = true;
            if (UITheme.PrimaryFont != null) _readout.font = UITheme.PrimaryFont;

            BuildRecordButton(rt);

            SetVisible(false);
        }

        private void BuildRecordButton(Transform parent)
        {
            var btnRt = UIFactory.CreateRect("Record", parent);
            btnRt.gameObject.AddComponent<LayoutElement>().preferredHeight = UITheme.MinTouchTarget;

            _recordPill = btnRt.gameObject.AddComponent<Image>();
            _recordPill.sprite = UIShapes.RoundedRect(Mathf.RoundToInt(UITheme.RadiusPill));
            _recordPill.type = Image.Type.Sliced;

            var btn = btnRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = _recordPill;
            btn.onClick.AddListener(ToggleTrace);

            _recordLabel = UIFactory.Label(btnRt, string.Empty, UITheme.BodyFontSize, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);

            RefreshRecordButton();
        }

        private void ToggleTrace()
        {
            if (_trace == null) return;
            if (_trace.IsRecording) _trace.Stop();
            else _trace.Start();
            RefreshRecordButton();
        }

        private void RefreshRecordButton()
        {
            if (_trace == null || _recordPill == null) return;
            bool recording = _trace.IsRecording;
            _recordPill.color = recording ? UITheme.Critical : UITheme.Success;
            _recordLabel.text = recording ? $"■ Stop trace ({_trace.SampleCount})" : "● Start trace";
        }

        private void Update()
        {
            _fpsAccum += Time.unscaledDeltaTime;
            _fpsFrames++;
            if (_fpsAccum >= 0.5f)
            {
                _fps = _fpsFrames / _fpsAccum;
                _fpsAccum = 0f;
                _fpsFrames = 0;
            }

            // Ticks regardless of on-screen visibility — a field walk needs the trace recording
            // even while the panel itself is hidden behind the Settings switch.
            _trace?.Tick(Time.unscaledDeltaTime);

            if (!IsVisible) return;

            _readout.text = BuildReadout();
            RefreshRecordButton();
        }

        private string BuildReadout()
        {
            var sb = new StringBuilder(384);

            if (_session != null)
            {
                var loc = _session.Location;
                sb.Append("<b>Source</b>  ").Append(loc.Mode == Positioning.LocationSourceMode.DeviceGps ? "GPS" : "SIM")
                  .Append("  ").Append(loc.Status);
                if (loc.Status is Positioning.LocationSourceStatus.PermissionDenied
                    or Positioning.LocationSourceStatus.ServicesDisabled
                    or Positioning.LocationSourceStatus.Failed)
                    sb.Append(" <color=#E5573F>⚠</color>"); // UITheme.Critical
                sb.Append('\n');
            }

            sb.Append("<b>AR</b>  ").Append(_bootstrapper != null ? _bootstrapper.State.ToString() : "—").Append('\n');

            if (_localization != null)
            {
                sb.Append("<b>Frame</b>  ").Append(_localization.Frame.IsEstablished ? "established" : "seeding")
                  .Append("  (").Append(_localization.LastReseedReason).Append(")\n");
                sb.Append("<b>GPS residual</b>  ").Append(_localization.LastResidualMeters.ToString("F0")).Append(" m");
                if (_localization.ConsecutiveGpsRejections > 0)
                    sb.Append("  <color=#F0A824>rejects ").Append(_localization.ConsecutiveGpsRejections).Append("</color>"); // UITheme.Warning
                sb.Append('\n');
            }
            else
            {
                sb.Append("<b>Frame</b>  —\n<b>GPS residual</b>  —\n");
            }

            sb.Append("<b>Ground tier</b>  ").Append(_placement != null ? _placement.LastTier.ToString() : "—").Append('\n');

            if (_session != null)
            {
                sb.Append("<b>Route</b>  ").Append(_session.Progress.CumulativeDistanceMeters.ToString("F0"))
                  .Append(" / ").Append(_session.Progress.TotalDistanceMeters.ToString("F0")).Append(" m")
                  .Append("   lateral ").Append(_session.Progress.LateralDistanceMeters.ToString("F0")).Append(" m\n");
            }

            float battery = SystemInfo.batteryLevel;
            sb.Append("<b>FPS</b>  ").Append(_fps.ToString("F0"));
            sb.Append("   <b>Battery</b>  ").Append(battery >= 0f ? Mathf.RoundToInt(battery * 100f) + "%" : "n/a");
            sb.Append("   <b>Trace</b>  ").Append(_trace != null && _trace.IsRecording ? $"● {_trace.SampleCount} samples" : "○ stopped");

            return sb.ToString();
        }
    }
}
