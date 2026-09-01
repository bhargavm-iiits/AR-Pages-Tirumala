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
    /// LocationProvider and the fused position pipeline together, and feeds
    /// LandmarkTriggerService from every fused update. One shared instance (via Resolve()) so
    /// Map/Progress/Landmarks/AR Navigation all read the same numbers and can never disagree.
    ///
    /// Docs/update1.md §03/Phase 3 item 8: this class now owns a PositionFusionService instead
    /// of feeding RouteProgressTracker directly from raw GPS. GpsPositionSource (inside Fusion)
    /// subscribes to the same Location.OnFixFiltered event this class used to handle itself —
    /// starting/stopping Fusion in lockstep with Location achieves the same "only live while
    /// Active" behaviour the old State-check inside HandleFix did, just structurally instead of
    /// per-callback.
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
        /// <summary>Minimum real time between journal writes while Active — §04 Phase 4 item 3
        /// says "every 30 s and on pause"; the periodic half is a throttle on HandleProgressUpdated
        /// rather than its own timer, since that already fires on every fused update.</summary>
        private const float JournalSaveIntervalSeconds = 30f;

        public NavigationState State { get; private set; } = NavigationState.Idle;
        public LocationProvider Location { get; }
        public RouteProgressTracker Progress { get; }
        public LandmarkTriggerService Triggers { get; }
        public PositionFusionService Fusion { get; }
        public NavigationConfidenceMachine Confidence { get; } = new();

        public event Action<NavigationState> OnStateChanged;

        private readonly SessionJournal _journal = new();
        private double _lastJournalSaveTime = double.NegativeInfinity;

        public NavigationSession(LocationProvider location, RouteProgressTracker progress, LandmarkTriggerService triggers, PositionFusionService fusion)
        {
            Location = location;
            Progress = progress;
            Triggers = triggers;
            Fusion = fusion;
            Progress.OnUpdated += HandleProgressUpdated;
        }

        public void Start()
        {
            if (State == NavigationState.Active) return;
            Triggers.ResetSession();
            Confidence.MarkReady();
            Location.Play();
            Fusion.Start();
            // §00/Phase 7 item 5: battery is the binding constraint on a 2-4 hour climb: AR
            // camera + VIO + screen is roughly 15-25%/hour. Nothing in the project set this
            // before, so the app ran at whatever the platform's default/vSync rate was.
            UnityEngine.Application.targetFrameRate = 30;
            SetState(NavigationState.Active);
        }

        public void Pause()
        {
            if (State != NavigationState.Active) return;
            Location.Pause();
            Fusion.Stop();
            SaveJournal();
            SetState(NavigationState.Paused);
        }

        public void Resume()
        {
            if (State != NavigationState.Paused) return;
            Location.Play();
            Fusion.Start();
            SetState(NavigationState.Active);
        }

        public void End()
        {
            Location.Pause();
            Fusion.Stop();
            SaveJournal();
            SetState(NavigationState.Ended);
        }

        /// <summary>Fires on every fused position update regardless of which source produced it
        /// (GPS, step counter, barometer) — landmark triggers now check against the same fused
        /// position everything else reads, not just raw GPS fixes.</summary>
        private void HandleProgressUpdated()
        {
            if (State != NavigationState.Active) return;

            Triggers.Feed(Progress.Latitude, Progress.Longitude);

            double now = UnityEngine.Time.unscaledTime;
            Confidence.Feed(Fusion.Estimator.SigmaMeters, Progress.LateralDistanceMeters, now);

            if (now - _lastJournalSaveTime >= JournalSaveIntervalSeconds)
            {
                SaveJournal();
                _lastJournalSaveTime = now;
            }
        }

        private void SaveJournal()
        {
            _journal.Save(Fusion.Estimator.S, Fusion.Estimator.SigmaMeters, Fusion.Estimator.BarometricBiasMeters, Confidence.Confidence);
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
            var fusion = new PositionFusionService(db.Route, progress, location);

            var session = new NavigationSession(location, progress, triggers, fusion);
            ServiceLocator.Register(session);
            return session;
        }
    }
}
