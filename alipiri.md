# Alipiri AR Navigation — Project Status

Analysis of what's completed in this Unity project against its own build plan (`PLAN.md`), as of 2026-08-27.

This is a fully offline outdoor AR wayfinding app for the 7.29 km Tirupati→Tirumala pilgrim stairway. It follows a detailed 14-phase build plan, and **phases 1–9 and 11 are essentially done**, with phase 10 (on-device AR) partially built but obviously untested outdoors, and phases 12–14 not started (they depend on external inputs).

## Completed

- **Project setup** — AR Foundation 6.5, ARCore XR Plugin 6.5, XR Plugin Management, Input System 1.19 all installed; product/company names set; URP configured.
- **Data pipeline** — `landmarks.json` (41 landmarks, valid JSON, restructured under a `landmarks` key) and the route GeoJSON copied into `StreamingAssets`; `GeoJsonParser`, `RouteBuilder`, `GeoMath`, `PolylineUtility` all written (428 lines in `Route/`).
- **Localization** — `LocalizationService`/`Loc`/`LocalizedLabel` built, `en.json` has 130 keys, everything wired for live-retranslation.
- **UI framework + all 12 screens** — by far the largest chunk (~5,650 lines): `UITheme`, `UIFactory`, `UIShapes`, `UITween`, `SafeAreaFitter`, plus every screen from the plan (Login, AR Nav, Map, Landmarks, Progress, Settings) and every overlay (LandmarkPopup, TurnInstructionCard, ArrivalCard, LowGpsWarning, PauseResumeCard). No stubs or `TODO`/`NotImplementedException` markers found — the placeholder hits in the code are just input-field placeholder text, not incomplete logic.
- **Positioning/navigation logic** — `LocationProvider`, `GpsKalmanFilter`, `TraceReplaySource` (the desk-testable trace-replay harness from §08), `RouteProgressTracker`, `EtaEstimator`, `LandmarkTriggerService`, `ManeuverDetector`, `NavigationSession` — all present under `Positioning/` (the plan called this folder `Navigation/`, but it's the same content).
- **Map** — `DemoBasemap`, `TileBasemap`, `MapView`, `RouteOverlay`, `UserMarker`, `PoiMarkerLayer` (993 lines) — the procedural offline map with live position.
- **AR core** — `ARSessionBootstrapper`, `GroundPlacementService` (3-tier fallback), `DynamicArrowManager`, `NavigationArrow`, `HybridLocalizationEngine`, `GeoAnchorFrame` (616 lines) — the pose-fusion/arrow system is written.
- **Voice (Phase 11)** — `VoiceNavigationManager` + `AndroidTextToSpeech` + `HapticService` done.
- **Login/Excel (§12)** — `ProfileService`, `ExcelLoginStore`, `XlsxWriter` (dependency-free OOXML writer) all present; a generated `Assets/login.xlsx` already exists, confirming it's been exercised in Play mode.
- **Diagnostics** — `DebugOverlay`, `GpsTraceRecorder` for field-tuning telemetry.

## Not yet built

Matches the plan's own "deferred" list:

`AppConfig.cs`, `UnitFormatter.cs`, `NavigationEvents.cs`, `HeadingProvider.cs`, `LandmarkRepository.cs`, `LoginLog.cs`, `ARAnchorService.cs`, `PermissionRationale.cs`, `IBasemapSource.cs` (interface), `ElevationProfile.cs`, `StepEstimator.cs`, and the editor tools `FontSetup.cs`, `ImportKmlTool.cs`, `BakeElevation.cs`, `ProjectSetup.cs`.

Some of these may be folded into existing files (worth a quick grep check before assuming they're truly absent) rather than separate classes.

## Explicitly out of scope for v1 (by design, per PLAN.md §03)

- The 1,171.7 m route gap (16% of the route) is bridged with a straight line pending the KML.
- No landmark photos (procedural category tiles instead), no elevation chart (empty state), step counts are labelled estimates, no satellite imagery (demo basemap), Indic fonts/translations/voice audio all deferred.

## Not yet verified

Nothing has been committed to git yet (`master` has zero commits — everything is staged). The real unknown, per the plan itself, is **Phase 10**: whether the AR arrows actually stay on the physical steps outdoors — that requires walking the stairway with a phone, which no amount of code review confirms.
