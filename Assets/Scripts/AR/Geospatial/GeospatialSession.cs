using System.Collections;

namespace AlipiriAR.AR.Geospatial
{
    public enum GeospatialAvailability { NotChecked, Unsupported, Unavailable, Available }

    /// <summary>Bridges an ARCore Geospatial pose into HybridLocalizationEngine.FeedFix, so every
    /// existing consumer (arrows, progress, triggers, map marker) would inherit Geospatial's ~1 m
    /// accuracy with zero further change (Docs/GeospatialPlan.md §06, Rev 4 Phase I) — a second,
    /// better fix source through the door FeedFix already has, not a second localization engine.
    ///
    /// DELIBERATELY INERT TODAY — no ARCore Extensions API calls. The Extensions package
    /// (com.google.ar.core.arfoundation.extensions) is not installed in this project
    /// (Docs/GeospatialPlan.md §04 B2): it ships as a .tgz from Google's GitHub releases, not the
    /// Unity registry, and its AR Foundation 6.5 compatibility is unverified. This project's own
    /// standard — see ARSessionBootstrapper's class doc — is to verify every AR API call against
    /// the actual installed package source before writing it, never from memory, precisely
    /// because that gap already produced one real on-device bug here (ARSessionBootstrapper.cs
    /// :139, a door tracked as a plane). Writing AREarthManager calls now would mean guessing
    /// method/property names that have changed between Extensions releases (CheckVpsAvailability
    /// vs CheckVpsAvailabilityAsync; CameraGeospatialPose's exact fields) — code that reads as
    /// verified but silently breaks the day the package actually lands.
    ///
    /// Availability and TryGetPose below are the honest, tested behaviour today: Geospatial is
    /// off, every consumer uses the existing GPS path untouched. When Docs/GeospatialPlan.md's
    /// Phase G is done (package installed, Cloud project configured, ARCoreExtensions attached in
    /// ARSessionBootstrapper.BuildHierarchy), replace the two method bodies below with real calls
    /// verified against Library/PackageCache/com.google.ar.core.arfoundation.extensions/... —
    /// nothing else in this class, or in any caller, needs to change shape.</summary>
    public class GeospatialSession
    {
        public GeospatialAvailability Availability { get; private set; } = GeospatialAvailability.Unsupported;

        /// <summary>-1 until a real pose has been read. Surfaced to DebugOverlay (Rev 4 Phase I).</summary>
        public float LastHorizontalAccuracyMeters { get; private set; } = -1f;

        /// <summary>-1 until a real pose has been read. Surfaced to DebugOverlay (Rev 4 Phase I).</summary>
        public float LastHeadingAccuracyDeg { get; private set; } = -1f;

        /// <summary>Docs/GeospatialPlan.md §05's device-check step — VPS availability at a given
        /// fix, meant to run once after the AR session reaches Ready. Always resolves Unsupported
        /// until the real implementation lands (see class doc) — that is the correct, honest
        /// result for "package not installed," not a bug to silently swallow.</summary>
        public IEnumerator CheckAvailability(double lat, double lon)
        {
            Availability = GeospatialAvailability.Unsupported;
            yield break;
        }

        /// <summary>Reads the current Earth-tracked camera pose in the exact (lat, lon,
        /// headingDeg, horizontalAccuracyMeters) shape HybridLocalizationEngine.FeedFix already
        /// accepts (AR/HybridLocalizationEngine.cs:58) — wiring this in later is a one-line branch
        /// at the caller, not a new consumer path. Always returns false today (see class doc);
        /// every out parameter is left at its default so a caller that forgets to check the return
        /// value fails obviously rather than silently trusting zeroed coordinates.</summary>
        public bool TryGetPose(out double lat, out double lon, out float headingDeg, out float horizontalAccuracyMeters)
        {
            lat = 0;
            lon = 0;
            headingDeg = 0;
            horizontalAccuracyMeters = 0;
            return false;
        }
    }
}
