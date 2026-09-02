using System;
using System.Collections.Generic;
using AlipiriAR.AR;
using AlipiriAR.AR.Geospatial;
using AlipiriAR.Core;
using AlipiriAR.Data;
using AlipiriAR.Database;
using AlipiriAR.Localization;
using AlipiriAR.Positioning;
using AlipiriAR.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AlipiriAR.UI
{
    /// <summary>Bare pointer down/up relay — the bottom stat card needs both a click (tap →
    /// Progress tab, already wired via Button) and a long-press (→ Pause overlay), and uGUI has
    /// no built-in long-press gesture.</summary>
    internal class StatCardPointerCatcher : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Action OnPointerDownAction;
        public Action OnPointerUpAction;

        public void OnPointerDown(PointerEventData eventData) => OnPointerDownAction?.Invoke();
        public void OnPointerUp(PointerEventData eventData) => OnPointerUpAction?.Invoke();
    }

    /// <summary>
    /// Frame 1 + 11 — full-bleed AR camera, chevron trail, top pill, left rail (compass/AR/Auto/
    /// brightness), right step badge, bottom stat card (PLAN.md §06/§10). The default tab, so
    /// this is the first screen built after login.
    ///
    /// The AR session itself (ARSessionBootstrapper onward) is the one part of this whole app
    /// that cannot be exercised outside a real ARCore-capable Android device — see that class's
    /// doc comment. In the Editor (and everywhere this was tested), ARSession.state resolves to
    /// Unsupported, which exercises exactly the fallback branch below — a real device is required
    /// to confirm the AR-active path.
    /// </summary>
    public class ARNavigationScreen : UIScreen
    {
        private enum BrightnessMode { Auto, Day, Night }

        private ARSessionBootstrapper _bootstrapper;
        private GroundPlacementService _placement;
        private HybridLocalizationEngine _localization;
        private GeospatialSession _geospatial;
        private DynamicArrowManager _arrows;
        private NavigationSession _session;
        private JsonDatabase _db;
        private List<LandmarkData> _landmarksByDistance;

        private Image _backgroundImg;
        private RectTransform _fallbackPanel;
        private TMP_Text _fallbackTitle;
        private Image _fallbackIcon;
        private Coroutine _fallbackPulseHandle;
        private TMP_Text _fallbackBody;

        private TMP_Text _pillNameLabel;
        private TMP_Text _pillDistanceLabel;
        private Image _speakerIcon;
        private Audio.VoiceNavigationManager _voice;
        private Coroutine _speakerPulseHandle;
        private Image _arPillBg;
        private Image _autoPillBg;
        private GameObject _autoGlow;
        private TMP_Text _autoPillLabel;
        private TMP_Text _brightnessLabel;
        private TMP_Text _stepBadgeLabel;

        private TMP_Text _statDistanceValue;
        private TMP_Text _statNextValue;
        private TMP_Text _statEtaValue;
        private Image _progressFill;

        private RectTransform _startHintPill;

        private BrightnessMode _brightness = BrightnessMode.Auto;
        private bool _autoAdvance = true;
        private LandmarkData _currentTarget;
        private TurnInstructionCard _turnCard;
        private ArrivalCard _arrivalCard;
        private LowGpsWarning _lowGpsCard;
        private PauseResumeCard _pauseCard;
        private float _lowGpsSuppressUntil = -1f;
        private float _pointerDownTime = -1f;
        private float _lastHeadingDeg;
        private bool _isActive;
        private const float LongPressSeconds = 0.5f;

        protected override void Build(RectTransform root)
        {
            _backgroundImg = UIFactory.Panel(root, UITheme.Ground);

            _db = ServiceLocator.Get<JsonDatabase>();
            _landmarksByDistance = new List<LandmarkData>(_db.Landmarks);
            _landmarksByDistance.Sort((a, b) => a.CumulativeDistanceMeters.CompareTo(b.CumulativeDistanceMeters));

            BuildFallbackPanel(root);
            BuildTopChrome(root);
            BuildLeftRail(root);
            BuildRightBadge(root);
            BuildBottomCard(root);
            BuildStartHint(root);

            _session = NavigationSession.Resolve();
            _session.Location.OnFixFiltered += OnLocationFix;
            _session.Triggers.OnArrived += OnArrived;
            _session.Location.OnAccuracyLevelChanged += OnAccuracyLevelChanged;
            _session.OnStateChanged += OnSessionStateChanged;

            RefreshStatCard();
            RefreshTopPill();
            RefreshStepBadge();

            _bootstrapper = ARSessionBootstrapper.Create();
            if (ServiceLocator.TryGet<Diagnostics.DebugOverlay>(out var overlay)) overlay.AttachBootstrapper(_bootstrapper);
            if (ServiceLocator.TryGet<Diagnostics.GpsTraceRecorder>(out var trace)) trace.AttachBootstrapper(_bootstrapper);

            _bootstrapper.OnStateChanged += OnArStateChanged;
            OnArStateChanged(_bootstrapper.State);

            // Backgrounding can invalidate ARCore's tracking origin while GeoAnchorFrame.
            // IsEstablished stays true with no signal saying so (Docs/update1.md §02 F-08) —
            // invalidate on every pause/focus-loss so the next fix re-seeds from scratch
            // instead of placing arrows against a coordinate space that no longer exists.
            _bootstrapper.OnAppPaused += () => _localization?.Frame.Invalidate();
        }

        protected override void OnShown()
        {
            _isActive = true;
            // Previously also called _session.Start() when Idle — opening this tab began walking
            // (real GPS included) with no user action at all, and in the Editor's trace-replay
            // harness the nav board visibly climbed on its own. Starting is now the
            // NavigationDrawer's Start row's job exclusively; a resume-in-progress is not a fresh
            // start, so that half stays automatic. See Docs/Draft1.md D11 follow-up.
            if (_session.State == NavigationState.Paused) _session.Resume();
        }

        /// <summary>All four AR overlays live in UIRoot's single shared OverlayContainer, not
        /// inside this screen's own hierarchy (they need to render above the full-bleed camera
        /// regardless of this screen's own layout), so switching tabs never touches them on its
        /// own — found on a real device, where a TurnInstructionCard stayed floating over the
        /// Settings screen after navigating away from AR mid-walk. Closing whatever's already
        /// open here isn't enough by itself, though: the session's GPS/trace feed keeps running
        /// in the background on purpose, so OnLocationFix/OnArrived keep firing and can create a
        /// *new* overlay while this screen is hidden — confirmed on-device, where a fresh Turn
        /// card appeared over the Map screen minutes after this fix first went in. _isActive
        /// gates every overlay-creation call site (not the data refreshes, which should stay
        /// current) so nothing pops up unless this screen is the one actually showing.</summary>
        protected override void OnHidden()
        {
            _isActive = false;
            if (_turnCard != null) { _turnCard.Close(); _turnCard = null; }
            if (_arrivalCard != null) { _arrivalCard.Close(); _arrivalCard = null; }
            if (_lowGpsCard != null) { _lowGpsCard.Close(); _lowGpsCard = null; }
            if (_pauseCard != null) { _pauseCard.Close(); _pauseCard = null; }
        }

        private void Update()
        {
            if (_bootstrapper == null || _bootstrapper.State != ArAvailabilityState.Ready) return;
            if (_session.State != NavigationState.Active) return;

            _arrows?.Refresh(_session.Progress.CumulativeDistanceMeters, _bootstrapper.ArCamera.transform.forward);
            UpdateLongPress();
        }

        // ---------------------------------------------------------------
        // AR session lifecycle
        // ---------------------------------------------------------------

        private void OnArStateChanged(ArAvailabilityState state)
        {
            bool ready = state == ArAvailabilityState.Ready;
            _fallbackPanel.gameObject.SetActive(!ready);
            RefreshStartHint();
            var clear = new Color(0f, 0f, 0f, 0f);
            _backgroundImg.color = ready ? clear : UITheme.Ground;
            // _backgroundImg alone only covers the safe-area-inset region. The real camera
            // passthrough is blocked by ResponsiveUI's EdgeToEdgeBackground — a full-bleed opaque
            // panel one Canvas sibling behind SafeAreaRoot, meant only to color the notch/status-bar
            // strip outside the safe area, but sized to the whole screen. It has to go transparent
            // too or AR never becomes visible no matter what this screen does to its own background.
            if (ServiceLocator.TryGet<UIRoot>(out var uiRoot) && uiRoot.EdgeToEdgeBackground != null)
                uiRoot.EdgeToEdgeBackground.color = ready ? clear : UITheme.Ground;

            // Pulse only while genuinely checking — a static "please wait" icon gives no sense
            // that anything is happening, and every other fallback state (unsupported, failed,
            // needs-install) is a terminal outcome, not a wait, so it shouldn't breathe.
            if (state == ArAvailabilityState.Checking)
            {
                if (_fallbackPulseHandle == null && _fallbackIcon != null)
                    _fallbackPulseHandle = UITween.Pulse(_fallbackIcon.transform, 0.9f, 1.08f, 1.4f);
            }
            else if (_fallbackPulseHandle != null)
            {
                UITween.StopPulse(_fallbackPulseHandle);
                _fallbackPulseHandle = null;
                if (_fallbackIcon != null) _fallbackIcon.transform.localScale = Vector3.one;
            }

            switch (state)
            {
                case ArAvailabilityState.Checking:
                    SetFallbackText("nav.ar_checking", null);
                    break;
                case ArAvailabilityState.Installing:
                    SetFallbackText("nav.ar_needs_install", null);
                    break;
                case ArAvailabilityState.Unsupported:
                    SetFallbackText("nav.ar_unsupported_title", "nav.ar_unsupported_body");
                    break;
                case ArAvailabilityState.Failed:
                    SetFallbackText("nav.ar_failed_title", "nav.ar_failed_body");
                    break;
                case ArAvailabilityState.Ready:
                    _placement = new GroundPlacementService(_bootstrapper.RaycastManager, _bootstrapper.ArCamera);
                    _localization = new HybridLocalizationEngine(_bootstrapper.ArCamera);
                    // Rev 4 (Docs/GeospatialPlan.md §06 Phase I) — constructed unconditionally
                    // since it's a plain C# class with no package dependency, but every method on
                    // it is a documented no-op until ARCore Extensions is actually installed (see
                    // GeospatialSession's class doc). OnLocationFix below consults it first and
                    // falls straight through to the existing GPS path when it declines.
                    _geospatial = new GeospatialSession();
                    _arrows = new DynamicArrowManager(_db.Route.Waypoints, _placement, _localization, _bootstrapper.Origin.transform);

                    if (ServiceLocator.TryGet<Diagnostics.DebugOverlay>(out var readyOverlay))
                        readyOverlay.AttachAr(_bootstrapper, _localization, _placement, _geospatial);
                    if (ServiceLocator.TryGet<Diagnostics.GpsTraceRecorder>(out var readyTrace))
                        readyTrace.AttachAr(_bootstrapper, _localization, _placement);
                    break;
            }
        }

        private void SetFallbackText(string titleKey, string bodyKey)
        {
            _fallbackTitle.text = string.Empty;
            LocalizedLabel.Bind(_fallbackTitle, titleKey);
            if (bodyKey != null)
            {
                _fallbackBody.gameObject.SetActive(true);
                _fallbackBody.text = string.Empty;
                LocalizedLabel.Bind(_fallbackBody, bodyKey);
            }
            else
            {
                _fallbackBody.gameObject.SetActive(false);
            }
        }

        private void BuildFallbackPanel(Transform parent)
        {
            _fallbackPanel = UIFactory.CreateRect("Fallback", parent);
            UIFactory.StretchFill(_fallbackPanel, UITheme.SpaceXL);
            var vlg = _fallbackPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.spacing = UITheme.SpaceM;

            var iconRt = UIFactory.CreateRect("Icon", _fallbackPanel);
            iconRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 100f;
            _fallbackIcon = UIFactory.CenteredIcon(iconRt, IconType.Compass, 80f, UITheme.TextTertiary);

            var titleRt = UIFactory.CreateRect("Title", _fallbackPanel);
            titleRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;
            _fallbackTitle = UIFactory.Label(titleRt, string.Empty, UITheme.TitleFontSize, FontStyles.Bold, TextAlignmentOptions.Center);

            var bodyRt = UIFactory.CreateRect("Body", _fallbackPanel);
            bodyRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 72f;
            _fallbackBody = UIFactory.Label(bodyRt, string.Empty, UITheme.BodyFontSize, FontStyles.Normal, TextAlignmentOptions.Center, UITheme.TextSecondary);
            _fallbackBody.enableWordWrapping = true;
        }

        // ---------------------------------------------------------------
        // Top chrome — hamburger, landmark pill, speaker
        // ---------------------------------------------------------------

        private void BuildTopChrome(Transform parent)
        {
            var hamburgerRt = UIFactory.CreateRect("Hamburger", parent);
            hamburgerRt.anchorMin = hamburgerRt.anchorMax = new Vector2(0f, 1f);
            hamburgerRt.pivot = new Vector2(0f, 1f);
            hamburgerRt.anchoredPosition = new Vector2(UITheme.SpaceM, -UITheme.SpaceM);
            UIFactory.CircleShadow(hamburgerRt, 72f);
            var hamburgerBtn = UIFactory.CircleButton(hamburgerRt, 72f, OpenDrawer, new Color(0f, 0f, 0f, 0.45f));
            UIFactory.CenteredIcon(hamburgerBtn.transform, IconType.Hamburger, 32f);

            var speakerRt = UIFactory.CreateRect("Speaker", parent);
            speakerRt.anchorMin = speakerRt.anchorMax = new Vector2(1f, 1f);
            speakerRt.pivot = new Vector2(1f, 1f);
            speakerRt.anchoredPosition = new Vector2(-UITheme.SpaceM, -UITheme.SpaceM);
            UIFactory.CircleShadow(speakerRt, 72f);
            var speakerBtn = UIFactory.CircleButton(speakerRt, 72f, ToggleVoiceMute, new Color(0f, 0f, 0f, 0.45f));
            _speakerIcon = UIFactory.CenteredIcon(speakerBtn.transform, IconType.Speaker, 32f);
            RefreshSpeakerIcon();

            // No visual feedback previously existed that narration was actually playing — the
            // icon just sat static whether or not VoiceNavigationManager was speaking. A gentle
            // breathing pulse on the icon itself, for exactly the duration of playback, closes
            // that gap without adding new UI geometry.
            _voice = Audio.VoiceNavigationManager.Resolve();
            _voice.OnPlaybackStarted += OnVoicePlaybackStarted;
            _voice.OnPlaybackFinished += OnVoicePlaybackFinished;

            var pillRt = UIFactory.CreateRect("LandmarkPill", parent);
            pillRt.anchorMin = new Vector2(0.5f, 1f);
            pillRt.anchorMax = new Vector2(0.5f, 1f);
            pillRt.pivot = new Vector2(0.5f, 1f);
            pillRt.anchoredPosition = new Vector2(0f, -UITheme.SpaceM);
            pillRt.sizeDelta = new Vector2(460f, 110f);

            UIFactory.RoundedShadow(pillRt, 55f);
            var pillBg = UIFactory.Pill(pillRt, UITheme.Glass);
            var pillBtn = pillBg.gameObject.AddComponent<Button>();
            pillBtn.targetGraphic = pillBg;
            pillBtn.transition = Selectable.Transition.None;
            pillBtn.onClick.AddListener(OpenCurrentTargetPopup);

            var hlg = pillBg.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset((int)UITheme.SpaceM, (int)UITheme.SpaceM, (int)UITheme.SpaceXS, (int)UITheme.SpaceXS);
            hlg.spacing = UITheme.SpaceS;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandHeight = true;

            var iconRt = UIFactory.CreateRect("Icon", pillBg.transform);
            iconRt.gameObject.AddComponent<LayoutElement>().preferredWidth = 56f;
            UIFactory.CenteredIcon(iconRt, IconType.Gopuram, 44f, UITheme.Gold);

            var textRt = UIFactory.CreateRect("Text", pillBg.transform);
            textRt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var vlg = textRt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleLeft;
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.spacing = 2f;

            var nameRt = UIFactory.CreateRect("Name", textRt);
            nameRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            _pillNameLabel = UIFactory.Label(nameRt, string.Empty, UITheme.LabelFontSize, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            _pillNameLabel.enableWordWrapping = false;
            _pillNameLabel.overflowMode = TextOverflowModes.Ellipsis;

            var distRt = UIFactory.CreateRect("Distance", textRt);
            distRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;
            _pillDistanceLabel = UIFactory.Label(distRt, string.Empty, UITheme.CaptionFontSize, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, UITheme.TextSecondary);
        }

        private void RefreshTopPill()
        {
            _currentTarget = null;
            foreach (var lm in _landmarksByDistance)
            {
                if (lm.CumulativeDistanceMeters > _session.Progress.CumulativeDistanceMeters + 0.5)
                {
                    _currentTarget = lm;
                    break;
                }
            }

            if (_currentTarget == null)
            {
                _pillNameLabel.text = string.Empty;
                _pillDistanceLabel.text = string.Empty;
                return;
            }

            _pillNameLabel.text = _currentTarget.Name;
            double ahead = _currentTarget.CumulativeDistanceMeters - _session.Progress.CumulativeDistanceMeters;
            _pillDistanceLabel.text = Loc.T("nav.ahead_format", DistanceFormatter.FormatMeters(ahead));
        }

        private void OpenCurrentTargetPopup()
        {
            if (_currentTarget == null) return;
            LandmarkPopup.Show(ServiceLocator.Get<UIRoot>().OverlayContainer, _currentTarget);
        }

        private void ToggleVoiceMute()
        {
            var settings = SettingsStore.Resolve();
            settings.VoiceGuidanceEnabled = !settings.VoiceGuidanceEnabled;
            RefreshSpeakerIcon();
        }

        private void RefreshSpeakerIcon()
        {
            bool enabled = SettingsStore.Resolve().VoiceGuidanceEnabled;
            _speakerIcon.sprite = IconGraphic.Get(enabled ? IconType.Speaker : IconType.SpeakerMuted);
        }

        // ---------------------------------------------------------------
        // Left rail — compass, AR, Auto, brightness
        // ---------------------------------------------------------------

        private void BuildLeftRail(Transform parent)
        {
            var railRt = UIFactory.CreateRect("LeftRail", parent);
            railRt.anchorMin = railRt.anchorMax = new Vector2(0f, 0.5f);
            railRt.pivot = new Vector2(0f, 0.5f);
            railRt.anchoredPosition = new Vector2(UITheme.SpaceM, 20f);
            // A point-anchored rect with no explicit size defaults to 0x0 — the VerticalLayoutGroup
            // below then has zero space to lay four 72px circles out in, collapsing them into an
            // unreadable overlapping cluster. Found on a real device; every "rail"-style container
            // built this way (point anchor + LayoutGroup, no SetSize) has the same bug — see
            // MapScreen's right rail for the matching fix.
            UIFactory.SetSize(railRt, 72f, 4f * 72f + 3f * UITheme.SpaceS);

            var vlg = railRt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = UITheme.SpaceS;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            var compassSlot = UIFactory.CreateRect("CompassSlot", railRt);
            SetFixedSlot(compassSlot, 72f);
            UIFactory.CircleShadow(compassSlot, 72f);
            var compassBtn = UIFactory.CircleButton(compassSlot, 72f, RecentreHeading, new Color(0f, 0f, 0f, 0.45f));
            UIFactory.CenteredIcon(compassBtn.transform, IconType.Compass, 32f);

            var arSlot = UIFactory.CreateRect("ArSlot", railRt);
            SetFixedSlot(arSlot, 72f);
            // Always Accent-filled (never toggled off, unlike Auto/North below), so it can carry a
            // static glow rather than needing a reference to flip on/off.
            UIFactory.CircleGlow(arSlot, 72f, UITheme.Accent);
            UIFactory.CircleShadow(arSlot, 72f);
            var arBtn = UIFactory.CircleButton(arSlot, 72f, () => ServiceLocator.Get<UIRoot>().JumpToTab(1), UITheme.Accent);
            _arPillBg = arBtn.GetComponent<Image>();
            var arLabel = UIFactory.Label(arBtn.transform, string.Empty, UITheme.CaptionFontSize, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            LocalizedLabel.Bind(arLabel, "nav.mode_ar");

            var autoSlot = UIFactory.CreateRect("AutoSlot", railRt);
            SetFixedSlot(autoSlot, 72f);
            _autoGlow = UIFactory.CircleGlow(autoSlot, 72f, UITheme.Accent).gameObject;
            UIFactory.CircleShadow(autoSlot, 72f);
            var autoBtn = UIFactory.CircleButton(autoSlot, 72f, ToggleAutoAdvance, UITheme.Surface);
            _autoPillBg = autoBtn.GetComponent<Image>();
            _autoPillLabel = UIFactory.Label(autoBtn.transform, string.Empty, UITheme.CaptionFontSize, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            LocalizedLabel.Bind(_autoPillLabel, "nav.mode_auto");
            RefreshAutoVisual();

            var brightnessSlot = UIFactory.CreateRect("BrightnessSlot", railRt);
            SetFixedSlot(brightnessSlot, 72f);
            UIFactory.CircleShadow(brightnessSlot, 72f);
            var brightnessBtn = UIFactory.CircleButton(brightnessSlot, 72f, CycleBrightness, new Color(0f, 0f, 0f, 0.45f));
            UIFactory.CenteredIcon(brightnessBtn.transform, IconType.Sun, 32f);
        }

        private static void SetFixedSlot(RectTransform rt, float size)
        {
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = size;
            le.preferredHeight = size;
        }

        private void RecentreHeading()
        {
            // Re-anchors the frame's heading offset (and position, cheaply, since we're already
            // at the current fix) using the last known compass reading — the corrective action
            // this button exists for when AR "forward" has drifted from true north.
            if (_bootstrapper == null || _bootstrapper.State != ArAvailabilityState.Ready) return;
            if (_localization == null) return;

            _localization.Frame.Establish(
                _session.Progress.Latitude, _session.Progress.Longitude, _lastHeadingDeg,
                _bootstrapper.ArCamera.transform.position, _bootstrapper.ArCamera.transform.eulerAngles.y);
            HapticService.Pulse();
        }

        private void ToggleAutoAdvance()
        {
            _autoAdvance = !_autoAdvance;
            RefreshAutoVisual();
        }

        private void RefreshAutoVisual()
        {
            _autoPillBg.color = _autoAdvance ? UITheme.Accent : UITheme.Surface;
            _autoGlow.SetActive(_autoAdvance);
        }

        private void CycleBrightness()
        {
            _brightness = _brightness switch
            {
                BrightnessMode.Auto => BrightnessMode.Day,
                BrightnessMode.Day => BrightnessMode.Night,
                _ => BrightnessMode.Auto,
            };

            Screen.brightness = _brightness switch
            {
                BrightnessMode.Day => 1f,
                BrightnessMode.Night => 0.35f,
                _ => -1f, // Auto — let the OS decide; -1 is Unity's documented "no override" sentinel
            };
        }

        // ---------------------------------------------------------------
        // Right badge — step count
        // ---------------------------------------------------------------

        private void BuildRightBadge(Transform parent)
        {
            // StepBadge's own Image (below) draws beneath ITS children, but a shadow parented
            // inside StepBadge would still be one of those children — drawn on top of, not behind,
            // that fill. Root has no LayoutGroup managing its direct children (unlike the
            // Landmarks list), so an ordinary sibling placed here, before StepBadge, with a
            // matching anchor/size, works as a plain drop shadow (Docs/UIplan.md §03 Phase 2).
            var shadowRt = UIFactory.CreateRect("StepBadgeShadow", parent);
            shadowRt.anchorMin = shadowRt.anchorMax = new Vector2(1f, 0.5f);
            shadowRt.pivot = new Vector2(1f, 0.5f);
            shadowRt.anchoredPosition = new Vector2(-UITheme.SpaceM, 20f - 5f);
            shadowRt.sizeDelta = new Vector2(120f, 140f);
            var shadowImg = shadowRt.gameObject.AddComponent<Image>();
            shadowImg.sprite = UIShapes.RoundedRect(28);
            shadowImg.type = Image.Type.Sliced;
            shadowImg.color = new Color(0f, 0f, 0f, 0.28f);
            shadowImg.raycastTarget = false;

            var rt = UIFactory.CreateRect("StepBadge", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-UITheme.SpaceM, 20f);
            rt.sizeDelta = new Vector2(120f, 140f);

            var btn = rt.gameObject.AddComponent<Button>();
            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = UIShapes.RoundedRect(28);
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0f, 0f, 0f, 0.45f);
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => ServiceLocator.Get<UIRoot>().JumpToTab(3));

            var vlg = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth = true;
            vlg.padding = new RectOffset(8, 8, 12, 12);
            vlg.spacing = 4f;

            var iconRt = UIFactory.CreateRect("Icon", rt);
            iconRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;
            UIFactory.CenteredIcon(iconRt, IconType.Steps, 40f, UITheme.Gold);

            var labelRt = UIFactory.CreateRect("Label", rt);
            labelRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;
            _stepBadgeLabel = UIFactory.Label(labelRt, string.Empty, UITheme.CaptionFontSize, FontStyles.Bold, TextAlignmentOptions.Center);
        }

        private int _lastStepBadgeValue = -1;

        private void RefreshStepBadge()
        {
            // Shared with ProgressScreen/PoiMarkerLayer via RouteResult.TotalStepsEstimate —
            // see its doc comment (Docs/update1.md §02 F-05: this used to be an independently
            // hardcoded 3550 literal here, duplicated in the other two screens too).
            int totalSteps = _db.Route.TotalStepsEstimate;
            int steps = Mathf.RoundToInt(totalSteps * _session.Progress.FractionComplete);

            // OnLocationFix calls this on every fix, but the rounded step count only actually
            // changes every few metres — guard so a burst of identical fixes doesn't restart the
            // count-up tween on top of itself and stutter the label.
            if (steps == _lastStepBadgeValue)
            {
                SetStepBadgeText(steps);
                return;
            }

            int from = _lastStepBadgeValue < 0 ? steps : _lastStepBadgeValue;
            _lastStepBadgeValue = steps;
            UITween.CountInt(SetStepBadgeText, from, steps, 0.4f);
        }

        private void SetStepBadgeText(int steps) =>
            _stepBadgeLabel.text = Loc.T("nav.steps_label") + "\n~" + steps.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

        // ---------------------------------------------------------------
        // Bottom stat card
        // ---------------------------------------------------------------

        private void BuildBottomCard(Transform parent)
        {
            var cardRt = UIFactory.CreateRect("StatCard", parent);
            cardRt.anchorMin = new Vector2(0f, 0f);
            cardRt.anchorMax = new Vector2(1f, 0f);
            cardRt.pivot = new Vector2(0.5f, 0f);
            cardRt.anchoredPosition = new Vector2(0f, UITheme.SpaceM);
            cardRt.sizeDelta = new Vector2(-UITheme.SpaceM * 2f, 220f);

            UIFactory.RoundedShadow(cardRt, UITheme.RadiusCard);
            var bg = UIFactory.Card(cardRt, UITheme.Glass);
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => ServiceLocator.Get<UIRoot>().JumpToTab(3));

            var pointerCatcher = bg.gameObject.AddComponent<StatCardPointerCatcher>();
            pointerCatcher.OnPointerDownAction = () => _pointerDownTime = Time.unscaledTime;
            pointerCatcher.OnPointerUpAction = () => _pointerDownTime = -1f;

            var vlg = bg.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.padding = new RectOffset((int)UITheme.SpaceM, (int)UITheme.SpaceM, (int)UITheme.SpaceS, (int)UITheme.SpaceS);
            vlg.spacing = UITheme.SpaceS;

            var rowRt = UIFactory.CreateRect("Row", bg.transform);
            rowRt.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            var hlg = rowRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childForceExpandWidth = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            (_, _statDistanceValue) = BuildStatColumn(rowRt, "nav.distance");
            (_, _statNextValue) = BuildStatColumn(rowRt, "nav.next_landmark");
            (_, _statEtaValue) = BuildStatColumn(rowRt, "nav.eta");

            var progressRt = UIFactory.CreateRect("Progress", bg.transform);
            progressRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;
            _progressFill = UIFactory.ProgressBar(progressRt, 0f);
        }

        private static (TMP_Text caption, TMP_Text value) BuildStatColumn(Transform parent, string captionKey)
        {
            var colRt = UIFactory.CreateRect("Col", parent);
            var vlg = colRt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.spacing = 2f;

            var valueRt = UIFactory.CreateRect("Value", colRt);
            valueRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            var value = UIFactory.Label(valueRt, string.Empty, UITheme.BodyFontSize, FontStyles.Bold, TextAlignmentOptions.Center);
            // Same bug as MapScreen's bottom "Next" card, confirmed on-device: valueRt is fixed
            // at one line tall, but a long landmark name wraps by TMP's default and spills down
            // into the caption ("Next Landmark") directly below it. Distance/ETA are always short
            // so this only ever bites the Next Landmark column, but all three share this method.
            value.enableWordWrapping = false;
            value.overflowMode = TextOverflowModes.Ellipsis;

            var captionRt = UIFactory.CreateRect("Caption", colRt);
            captionRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;
            var caption = UIFactory.Label(captionRt, string.Empty, UITheme.CaptionFontSize, FontStyles.Normal, TextAlignmentOptions.Center, UITheme.TextTertiary);
            LocalizedLabel.Bind(caption, captionKey);

            return (caption, value);
        }

        private void RefreshStatCard()
        {
            _statDistanceValue.text = DistanceFormatter.FormatMeters(_session.Progress.RemainingDistanceMeters);
            _statNextValue.text = _currentTarget != null ? _currentTarget.Name : string.Empty;
            _statEtaValue.text = Loc.T("progress.eta_minutes_format", EtaEstimate.Minutes(_session.Progress.RemainingDistanceMeters));
            _progressFill.fillAmount = _session.Progress.FractionComplete;
        }

        private void UpdateLongPress()
        {
            if (_pointerDownTime < 0f) return;
            if (Time.unscaledTime - _pointerDownTime >= LongPressSeconds)
            {
                _pointerDownTime = -1f;
                OpenPauseOverlay();
            }
        }

        // ---------------------------------------------------------------
        // Start-navigation hint
        // ---------------------------------------------------------------

        /// <summary>Navigation only ever starts from the drawer's "Start Navigation" row
        /// (NavigationSession's own class doc, D11) — opening this tab alone shows the camera but
        /// leaves _session.State at Idle, which means Update() never calls _arrows.Refresh and
        /// Location.Play() is never called, so no chevrons ever appear and no GPS fix is ever
        /// acquired. That's intentional (see NavigationSession.cs), but with nothing on screen
        /// saying so, a first-time walker reasonably reads "camera on, no arrows" as broken rather
        /// than "navigation hasn't been started yet." This pill is the fix — shown only while AR
        /// is actually Ready and the session is Idle/Ended, so it never fights the fallback panel
        /// (which already covers Checking/Installing/Unsupported/Failed) or shows once walking.</summary>
        private void BuildStartHint(Transform parent)
        {
            var rt = UIFactory.CreateRect("StartHint", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(720f, 100f);

            UIFactory.RoundedShadow(rt, 50f);
            var bg = UIFactory.Pill(rt, UITheme.Glass);
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(OpenDrawer);

            var label = UIFactory.Label(bg.transform, string.Empty, UITheme.LabelFontSize,
                FontStyles.Bold, TextAlignmentOptions.Center, UITheme.TextPrimary);
            LocalizedLabel.Bind(label, "nav.start_hint");

            _startHintPill = rt;
            _startHintPill.gameObject.SetActive(false);
        }

        private void RefreshStartHint()
        {
            if (_startHintPill == null) return;
            bool arReady = _bootstrapper != null && _bootstrapper.State == ArAvailabilityState.Ready;
            bool navIdle = _session == null || _session.State is NavigationState.Idle or NavigationState.Ended;
            _startHintPill.gameObject.SetActive(arReady && navIdle);
        }

        private void OnSessionStateChanged(NavigationState state) => RefreshStartHint();

        // ---------------------------------------------------------------
        // Drawer / overlays
        // ---------------------------------------------------------------

        private void OpenDrawer()
        {
            NavigationDrawer.Show(
                ServiceLocator.Get<UIRoot>().OverlayContainer,
                _session.State,
                _session.Start,
                OpenPauseOverlay,
                EndNavigation,
                idx => ServiceLocator.Get<UIRoot>().JumpToTab(idx));
        }

        private void OpenPauseOverlay()
        {
            if (_pauseCard != null) return;
            _session.Pause();
            _pauseCard = PauseResumeCard.Show(
                ServiceLocator.Get<UIRoot>().OverlayContainer,
                onResume: () =>
                {
                    _pauseCard = null;
                    _session.Resume();
                },
                onEndConfirmed: () =>
                {
                    _pauseCard = null;
                    EndNavigation();
                });
        }

        private void EndNavigation()
        {
            _session.End();
            ServiceLocator.Get<UIRoot>().JumpToTab(2); // Landmarks tab, per PLAN.md §06
        }

        private void OnArrived(LandmarkData landmark)
        {
            // Voice fires regardless of which tab is showing — that's the point of voice
            // guidance — but the celebratory popup is AR-screen-specific UI, gated the same way
            // as the turn card and low-GPS warning below (see OnHidden's doc comment).
            Audio.VoiceNavigationManager.Resolve().Speak(landmark.VoiceText);
            if (!_isActive) return;

            if (_arrivalCard != null) return;

            // The next-maneuver turn card is distance-driven and independent of "arrived at
            // this landmark" — confirmed on-device, both were visible stacked on top of each
            // other. The arrival celebration takes priority; RefreshManeuverCard below also
            // bails out early while _arrivalCard is up so it can't pop straight back open.
            if (_turnCard != null) { _turnCard.Close(); _turnCard = null; }

            _arrivalCard = ArrivalCard.Show(ServiceLocator.Get<UIRoot>().OverlayContainer, landmark, () => _arrivalCard = null);

            if (_autoAdvance)
            {
                float autoDismissDelay = 2.2f;
                var card = _arrivalCard;
                StartCoroutine(AutoDismissArrival(card, autoDismissDelay));
            }
        }

        private System.Collections.IEnumerator AutoDismissArrival(ArrivalCard card, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (card != null) card.Close();
        }

        private void OnAccuracyLevelChanged(LocationAccuracyLevel level)
        {
            if (!_isActive) return;
            if (level != LocationAccuracyLevel.Poor) return;
            if (Time.unscaledTime < _lowGpsSuppressUntil) return;
            if (_lowGpsCard != null) return;

            _lowGpsCard = LowGpsWarning.Show(ServiceLocator.Get<UIRoot>().OverlayContainer, () =>
            {
                _lowGpsCard = null;
                _lowGpsSuppressUntil = Time.unscaledTime + 60f;
            });
        }

        // ---------------------------------------------------------------
        // Live position
        // ---------------------------------------------------------------

        private void OnLocationFix(double rawLat, double rawLon, float headingDeg, float accuracyMeters)
        {
            _lastHeadingDeg = headingDeg;

            // Rev 4 (Docs/GeospatialPlan.md §06 Phase I) — a Geospatial pose is just a better fix
            // through FeedFix's existing door. TryGetPose always returns false until the ARCore
            // Extensions package is actually installed and wired (GeospatialSession's class doc),
            // so this is a no-op today and the line below behaves exactly as it did before.
            if (_geospatial != null && _geospatial.TryGetPose(out double geoLat, out double geoLon, out float geoHeading, out float geoAccuracy))
                _localization?.FeedFix(geoLat, geoLon, geoHeading, geoAccuracy);
            else
                _localization?.FeedFix(_session.Progress.Latitude, _session.Progress.Longitude, headingDeg, accuracyMeters);

            RefreshTopPill();
            RefreshStatCard();
            RefreshStepBadge();
            if (_isActive) RefreshManeuverCard(headingDeg);
        }

        private void RefreshManeuverCard(float headingDeg)
        {
            if (_arrivalCard != null) return;

            string towards = _currentTarget != null ? _currentTarget.Name : string.Empty;
            var maneuver = ManeuverDetector.DetectNext(_session.Progress.CumulativeDistanceMeters, _db.Route.Waypoints, towards);

            if (maneuver == null || maneuver.Value.Type == ManeuverType.Straight || maneuver.Value.DistanceMeters > 80.0)
            {
                if (_turnCard != null)
                {
                    _turnCard.Close();
                    _turnCard = null;
                }
                return;
            }

            if (_turnCard == null)
            {
                _turnCard = TurnInstructionCard.Show(ServiceLocator.Get<UIRoot>().OverlayContainer, maneuver.Value, () => _turnCard = null);
                AnnounceManeuver(maneuver.Value);
            }
            else
            {
                _turnCard.Refresh(maneuver.Value);
            }
        }

        private static void AnnounceManeuver(Maneuver maneuver)
        {
            string titleKey = maneuver.Type switch
            {
                ManeuverType.TurnLeft => "overlay.turn_left",
                ManeuverType.TurnRight => "overlay.turn_right",
                _ => "overlay.continue_straight",
            };
            string text = Loc.T(titleKey) + " " + Loc.T("overlay.in_distance_format", DistanceFormatter.FormatMeters(maneuver.DistanceMeters));
            Audio.VoiceNavigationManager.Resolve().Speak(text);
        }

        private void OnVoicePlaybackStarted()
        {
            if (_speakerIcon == null) return;
            _speakerPulseHandle = UITween.Pulse(_speakerIcon.transform, 0.88f, 1.12f, 0.9f);
        }

        private void OnVoicePlaybackFinished()
        {
            UITween.StopPulse(_speakerPulseHandle);
            _speakerPulseHandle = null;
            if (_speakerIcon != null) _speakerIcon.transform.localScale = Vector3.one;
        }

        private void OnDestroy()
        {
            if (_session != null)
            {
                _session.Location.OnFixFiltered -= OnLocationFix;
                _session.Triggers.OnArrived -= OnArrived;
                _session.Location.OnAccuracyLevelChanged -= OnAccuracyLevelChanged;
                _session.OnStateChanged -= OnSessionStateChanged;
            }
            if (_voice != null)
            {
                _voice.OnPlaybackStarted -= OnVoicePlaybackStarted;
                _voice.OnPlaybackFinished -= OnVoicePlaybackFinished;
            }
            UITween.StopPulse(_speakerPulseHandle);
            UITween.StopPulse(_fallbackPulseHandle);
        }
    }
}
