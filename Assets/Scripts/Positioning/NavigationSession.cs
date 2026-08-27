using System;
using AlipiriAR.Core;
using AlipiriAR.Data;
using AlipiriAR.Database;
using UnityEngine;

namespace AlipiriAR.Positioning
{
    public enum NavigationState { Idle, Active, Paused, Ended }

    /// <summary>
    /// Owns the whole live-walking session lifecycle (PLAN.md §06/§09) — starts/stops
    /// LocationProvider and feeds RouteProgressTracker + LandmarkTriggerService from its fixes.
    /// One shared instance (via Resolve()) so Map/Progress/Landmarks/AR Navigation all read the
    /// same numbers and can never disagree.
    ///
    /// Start() is called from exactly one place: the AR screen's NavigationDrawer "Start
    /// Navigation" row. Both the AR and Map screens used to call Start() themselves the instant
    /// their tab was first opened — meant as a desk-testing convenience, but it meant navigation
    /// (the simulated trace walker in the Editor, real GPS and its permission prompt on-device)
    /// began before the user had asked for it, with the nav board visibly climbing on its own.
    /// Resume() from a Pause is not a fresh start and stays automatic on tab-show. See
    /// Docs/Draft1.md D11.
    /// </summary>
    public class NavigationSession
    {
        public NavigationState State { get; private set; } = NavigationState.Idle;
        public LocationProvider Location { get; }
        public RouteProgressTracker Progress { get; }
        public LandmarkTriggerService Triggers { get; }

        public event Action<NavigationState> OnStateChanged;

        public NavigationSession(LocationProvider location, RouteProgressTracker progress, LandmarkTriggerService triggers)
        {
            Location = location;
            Progress = progress;
            Triggers = triggers;
            Location.OnFixFiltered += HandleFix;
        }

        public void Start()
        {
            if (State == NavigationState.Active) return;
            Triggers.ResetSession();
            Location.Play();
            SetState(NavigationState.Active);
        }

        public void Pause()
        {
            if (State != NavigationState.Active) return;
            Location.Pause();
            SetState(NavigationState.Paused);
        }

        public void Resume()
        {
            if (State != NavigationState.Paused) return;
            Location.Play();
            SetState(NavigationState.Active);
        }

        public void End()
        {
            Location.Pause();
            SetState(NavigationState.Ended);
        }

        private void HandleFix(double lat, double lon, float headingDeg, float accuracyMeters)
        {
            if (State != NavigationState.Active) return;
            Progress.Feed(lat, lon, Time.unscaledTimeAsDouble);
            Triggers.Feed(lat, lon);
        }

        private void SetState(NavigationState state)
        {
            State = state;
            OnStateChanged?.Invoke(state);
        }

        public static NavigationSession Resolve()
        {
            if (ServiceLocator.TryGet<NavigationSession>(out var existing)) return existing;

            var db = ServiceLocator.Get<JsonDatabase>();
            // Was previously LocationProvider.CreateTraceReplay unconditionally — the only call
            // site that ever constructs a LocationProvider, which meant the simulated 1.2 m/s
            // walk along the route drove every screen on every build, device included, and the
            // real Input.location/Input.compass path below (device-only) never ran. The Editor
            // has no GPS hardware, so it keeps the trace harness as PLAN.md's desk-testable
            // default; a device build now drives the app from real GPS. See Docs/Draft1.md D11.
            var location = Application.isEditor
                ? LocationProvider.CreateTraceReplay(db.Route.Waypoints)
                : LocationProvider.CreateDeviceGps();
            var progress = new RouteProgressTracker(db.Route);
            var triggers = new LandmarkTriggerService(db.Landmarks, VisitedStore.Resolve());

            var session = new NavigationSession(location, progress, triggers);
            ServiceLocator.Register(session);
            return session;
        }
    }
}
