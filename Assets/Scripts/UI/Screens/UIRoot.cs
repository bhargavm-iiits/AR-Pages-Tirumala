using System.Collections.Generic;
using AlipiriAR.Core;
using AlipiriAR.Data;
using AlipiriAR.Diagnostics;
using AlipiriAR.Localization;
using AlipiriAR.Positioning;
using AlipiriAR.Profile;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AlipiriAR.UI
{
    /// <summary>
    /// The whole app's visual entry point. Pages.unity is intentionally an empty scene — Canvas,
    /// EventSystem and every screen are built here in code, so nothing about startup depends on
    /// scene wiring (PLAN.md §07 "why procedural C# UI, not prefabs"). AppBootstrap calls
    /// UIRoot.Bootstrap() once ProfileService and JsonDatabase are ready.
    /// </summary>
    public class UIRoot : MonoBehaviour
    {
        private static readonly (string labelKey, IconType icon) [] Tabs =
        {
            ("tabs.navigate", IconType.Compass),
            ("tabs.map", IconType.North),
            ("tabs.landmarks", IconType.Gopuram),
            ("tabs.progress", IconType.Check),
            ("tabs.settings", IconType.Plus),
        };

        private const int DefaultTabIndex = 0;

        private RectTransform _screensContainer;
        private RectTransform _overlayContainer;
        private readonly List<UIScreen> _screens = new();
        private BottomNavBar _navBar;
        private LoginScreen _loginScreen;
        private RectTransform _shellRoot;
        private int _activeTab = -1;

        public Image EdgeToEdgeBackground { get; private set; }

        public static UIRoot Bootstrap()
        {
            EnsureEventSystem();

            var canvasGo = new GameObject("UICanvas", typeof(RectTransform), typeof(Canvas));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.sortingOrder = 0;
            var responsive = canvasGo.AddComponent<ResponsiveUI>();

            var root = canvasGo.AddComponent<UIRoot>();
            root.EdgeToEdgeBackground = responsive.EdgeToEdgeBackground;
            root.Build(responsive.SafeAreaRoot);
            Core.ServiceLocator.Register(root);
            return root;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<InputSystemUIInputModule>();
        }

        private void Build(RectTransform safeAreaRoot)
        {
            _shellRoot = UIFactory.CreateRect("Shell", safeAreaRoot);
            UIFactory.StretchFill(_shellRoot);
            _shellRoot.gameObject.SetActive(false);

            _screensContainer = UIFactory.CreateRect("Screens", _shellRoot);
            _screensContainer.anchorMin = Vector2.zero;
            _screensContainer.anchorMax = Vector2.one;
            _screensContainer.offsetMin = new Vector2(0f, BottomNavBar.Height);
            _screensContainer.offsetMax = Vector2.zero;

            BuildScreen<ARNavigationScreen>();
            BuildScreen<MapScreen>();
            BuildScreen<LandmarksScreen>();
            BuildScreen<ProgressScreen>();
            BuildScreen<SettingsScreen>();

            _navBar = BottomNavBar.Create(_shellRoot, Tabs, SelectTab);

            // Created after the nav bar, not before — a later sibling renders (and receives
            // input) on top. Overlays/modals must sit above the nav bar so their scrim actually
            // blocks tab-switching while open; found on a real device where the bottom tabs
            // stayed tappable underneath an open overlay because this was ordered the other way.
            _overlayContainer = UIFactory.CreateRect("Overlays", _shellRoot);
            UIFactory.StretchFill(_overlayContainer);

            // Constructed here, before ShowShell()/ShowLogin() below, specifically so a
            // returning user (HasProfile == true) — whose ARNavigationScreen.Build() runs
            // synchronously inside ShowShell()'s SelectTab(0) call, before this method even
            // returns — still finds both already registered (NewPlan.md §04 Phase B).
            var session = NavigationSession.Resolve();
            var trace = new GpsTraceRecorder(session.Location, session);
            ServiceLocator.Register(trace);
            var debugOverlay = DebugOverlay.Create(_overlayContainer, session, trace);
            ServiceLocator.Register(debugOverlay);

            BackStackManager.Instance.Push(HandleRootBack);

            var profileService = ProfileService.Resolve();
            if (profileService.HasProfile)
            {
                ShowShell();
            }
            else
            {
                ShowLogin(safeAreaRoot);
            }
        }

        private void BuildScreen<T>() where T : UIScreen
        {
            var go = new GameObject(typeof(T).Name, typeof(RectTransform));
            var screen = go.AddComponent<T>();
            screen.Initialize(_screensContainer);
            _screens.Add(screen);
        }

        private void ShowLogin(RectTransform safeAreaRoot)
        {
            var go = new GameObject("LoginScreen", typeof(RectTransform));
            _loginScreen = go.AddComponent<LoginScreen>();
            _loginScreen.Initialize(safeAreaRoot);
            _loginScreen.OnCompleted += OnLoginCompleted;
            _loginScreen.Show();
        }

        private void OnLoginCompleted()
        {
            _loginScreen.OnCompleted -= OnLoginCompleted;
            Destroy(_loginScreen.gameObject);
            _loginScreen = null;
            ShowShell();
        }

        private void ShowShell()
        {
            _shellRoot.gameObject.SetActive(true);
            SelectTab(DefaultTabIndex);
        }

        private void SelectTab(int index)
        {
            if (index < 0 || index >= _screens.Count) return;
            if (_activeTab == index) return;

            if (_activeTab >= 0) _screens[_activeTab].Hide();
            _activeTab = index;
            _screens[_activeTab].Show();
            _navBar.SetActive(index);
        }

        /// <summary>Lowest-priority back handler — returns to the default tab if elsewhere,
        /// otherwise does not consume (a future "confirm exit" overlay hooks in here).</summary>
        private bool HandleRootBack()
        {
            if (_loginScreen != null) return false;
            if (_activeTab != DefaultTabIndex)
            {
                SelectTab(DefaultTabIndex);
                return true;
            }
            return false;
        }

        public RectTransform OverlayContainer => _overlayContainer;

        /// <summary>Settings' on-screen back chevron — Settings is a root tab, not a pushed
        /// screen, so there is nothing to pop; returning to the default tab matches what hardware
        /// back already does from any non-default tab (HandleRootBack).</summary>
        public void SelectDefaultTab() => SelectTab(DefaultTabIndex);

        /// <summary>Map screen's gear icon (PLAN.md §06's top pill) — jumps straight to Settings.</summary>
        public void SelectSettingsTab() => SelectTab(_screens.Count - 1);

        /// <summary>AR screen's hamburger drawer — "jump to tab" (PLAN.md §06).</summary>
        public void JumpToTab(int index) => SelectTab(index);

        /// <summary>Settings' "Edit Profile" row — reopens LoginScreen pre-filled rather than
        /// blank. Saving still appends a new Excel row (PLAN.md §06); nothing here mutates the
        /// existing profile record.</summary>
        public void ShowEditProfileOverlay()
        {
            var profile = ProfileService.Resolve().Current;
            if (profile == null) return;

            var go = new GameObject("EditProfileScreen", typeof(RectTransform));
            var screen = go.AddComponent<LoginScreen>();
            screen.Prefill(profile.Value);
            screen.Initialize(_overlayContainer);

            void OnDone()
            {
                screen.OnCompleted -= OnDone;
                Destroy(go);
            }

            screen.OnCompleted += OnDone;
            screen.Show();
        }
    }
}
