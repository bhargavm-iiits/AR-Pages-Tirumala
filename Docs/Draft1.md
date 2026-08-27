# Alipiri AR Navigation — Build Plan (Rev 3)

**Target:** Unity 6000.5.5f1 · URP 17.5 · AR Foundation 6.5 · ARCore 6.5 · Android, fully offline
**Project:** `D:\Unity Projects\AR_pages`
**Rev 3 — supersedes `PLAN.md` (Rev 2).** Rev 2 was written when this project had zero app code —
its own §02 records "Scripts: **none**". That is no longer true and can no longer be used to plan
from: the project now has ~77 scripts (~9,700 lines), all 12 screens built, and a 65 MB APK
(`Builds/AlipiriAR.apk`) built as of 13 Aug 2026. Rev 2 reads as a greenfield plan for work that is
roughly 80% done, which hides the handful of things that genuinely still block a shippable build.
This document replaces it as plan-of-record. `PLAN.md` stays in place as the Rev 2 historical
record — it is not deleted, and its route-geometry and scope-decision sections are still accurate;
only its "what's built" picture is stale.

Everything below was verified this session by reading the actual repository state — source files,
`Packages/manifest.json`, `ProjectSettings/*`, the generated `landmarks.json`, and the manifest
inside the built APK — not carried forward from Rev 2's assumptions.

---

## 01 · Where the project actually stands

The headline: an APK exists. Every screen, the login/Excel flow, localisation core, route/landmark
data pipeline, the demo map, GPS positioning, and TTS voice are built and wired. What remains is
narrower than a full build-out — it's AR-on-device verification, a materially thin piece of the AR
pose fusion, two missing diagnostic tools, and a content pass.

> **2026-08-20 update:** Phases B (instruments) and C (pose-fusion hardening) below are now
> **done** — `DebugOverlay.cs`, `GpsTraceRecorder.cs`, and a much-expanded `HybridLocalizationEngine`
> (outlier rejection, re-seed, `ConsecutiveGpsRejections`) all exist and are wired through
> `UIRoot.cs`. Phase E's basemap item is also done (`IBasemapSource` + `TileBasemap.cs`). Rows 08,
> 09, 10, 14 and D1/D2 below are updated accordingly. In the same pass a new defect was found and
> fixed — **D11** — the app was unconditionally driving every screen from the simulated
> trace-replay walker, on-device included, because `NavigationSession.Resolve()` had only one
> `LocationProvider` factory call site to choose from.

| # | Phase (Rev 2 §11) | Status | Evidence |
|---|---|---|---|
| 01 | Project hygiene | **partial** | `productName` "Alipiri AR Navigation", `companyName` "AlipiriAR", bundle `com.alipiriar.navigation` all set correctly. But **zero git commits** (`git log` → "does not have any commits yet") against ~9,700 lines and a built APK, and stray scenes `Assets/1.unity` + `Assets/_Recovery/0.unity` + an empty `Assets/Docs/New folder` remain from earlier iteration |
| 02 | AR packages + Android settings | **done** | `com.unity.xr.arfoundation` 6.5.0, `com.unity.xr.arcore` 6.5.0, `com.unity.xr.management` 4.6.1 all installed; `Assets/XR/Loaders/ARCoreLoader.asset` present; IL2CPP (`scriptingBackend: Android: 1`), ARM64 (`AndroidTargetArchitectures: 2`), `AndroidMinSdkVersion: 26`; hand-authored `Assets/Plugins/Android/AndroidManifest.xml` with CAMERA + `com.google.ar.core` optional meta-data, documented in-file against a real device build |
| 03 | UI framework + theme | **done** | `UITheme`, `UIFactory` (508 ln), `UIShapes`, `UITween`, `IconGraphic` (332 ln), `ResponsiveUI`, `SafeAreaFitter`, `ConfettiBurst` all present under `Scripts/UI/Framework/` |
| 04 | Localisation core | **partial** | `LocalizationService` / `Loc` / `LocalizedLabel` implemented; `Assets/StreamingAssets/Localization/en.json` has 127 keys. The language picker offers Telugu/Hindi/Tamil/Kannada (`LocalizationService.cs:34-37`) but **only `en.json` exists on disk** — selecting any other locale currently has nothing to load. No `FontSetup.cs` editor tool exists; instead `UITheme.SetPrimaryFont` (`UITheme.cs:57-78`) builds a runtime fallback chain from OS font *names* — `"Nirmala UI", "Gautami", "Mangal", "Latha", "Tunga"` — which are Windows-installed fonts and **do not exist on Android** |
| 05 | Route + landmark data | **partial** | `GeoJsonParser`, `RouteBuilder`, `GeoMath`, `PolylineUtility` (incl. interpolation), `KmlImporter`, and `Scripts/Editor/ValidateData.cs` (146 ln) are all present and wired. `landmarks.json` was generated: 41 entries, valid JSON, the longitude typo is fixed (longitudes now range 79.3524°–79.4058° E, no outliers), and type casing is normalised to six clean types (`Temple`, `Water Point`, `Statue`, `Steps`, `Shops`, `Medical`). Several defects from the source data remain — see §03 below |
| 06 | Shell, login, Excel | **done** | `UIRoot`, `BackStackManager`, `BottomNavBar`, `LoginScreen` (478 ln), `ProfileService`, `XlsxWriter` (146 ln), `ExcelLoginStore` (140 ln) all present; `Assets/login.xlsx` exists on disk from a Play-mode run; `Assets/StreamingAssets/Templates/login_template.xlsx` backs the writer |
| 07 | Landmarks, Progress, Settings | **done** | `LandmarksScreen` (437 ln), `ProgressScreen` (221 ln), `SettingsScreen` (258 ln) all functional. Elevation renders an honest empty state gated on `waypoint.HasElevation` (`ProgressScreen.cs:174-186`) rather than fabricating a chart. "Steps Climbed" is always prefixed `~` and scaled from a documented `TotalStepsEstimate = 3550` constant (`ProgressScreen.cs:25`, `:213-214`), never presented as measured. Settings has voice/language/units/auto-brightness/haptic switches plus **Edit Profile** and **Export Login Sheet** rows both wired to real handlers (`SettingsScreen.cs:84-125`) |
| 08 | Demo map + live position | **done** | `MapView` (271 ln), `DemoBasemap`, `RouteOverlay`, `UserMarker`, `PoiMarkerLayer` all present and functional; `isBridged` waypoints render amber via `RouteOverlay.cs:34,41`. `IBasemapSource` now exists with `DemoBasemap` and `TileBasemap.cs` (180 ln) both implementing it — the v1.1 tile swap-in point Rev 2 planned is now a real interface substitution, not a rewrite |
| 09 | Positioning + session logic | **⚠ fixed this session — see D11** | `LocationProvider` drives real `Input.location`/`Input.compass` with a `FineLocation` runtime-permission flow; `GpsKalmanFilter`, `TraceReplaySource`, `RouteProgressTracker`, `EtaEstimator`, `LandmarkTriggerService`, `ManeuverDetector`, `NavigationSession` all present. But until today `NavigationSession.Resolve()` was the only call site constructing a `LocationProvider`, and it only ever called `CreateTraceReplay` — so the real-GPS path, though fully written, was dead code, and the simulated 1.2 m/s trace walker drove every screen unconditionally, including on-device. Now selects by `Application.isEditor`; see D11 |
| **10** | **AR navigation on device — the hard gate** | **⚠ written, entirely device-unverified** | Every AR file compiles and is wired end-to-end: `ARSessionBootstrapper` builds the AR session hierarchy in code, `ARNavigationScreen` reacts to its state machine and constructs `GroundPlacementService` → `HybridLocalizationEngine` → `DynamicArrowManager` on `Ready` (`ARNavigationScreen.cs:107-175`). `GroundPlacementService` implements the full three-tier fallback (Plane → FeaturePoint → fixed-offset Fallback). `HybridLocalizationEngine` is now 101 lines with GPS-outlier rejection, a `ConsecutiveGpsRejections` counter, and a forced re-seed path — D1 below is resolved. Every AR-adjacent class still carries an explicit "Device-unverified" note in its own doc comment; none of it has run on a phone |
| 11 | Voice via TTS | **done** | `VoiceNavigationManager` (112 ln) implements the clip → TTS → caption fallback chain; `AndroidTextToSpeech` (96 ln) present. `VoiceNavigationManager.Resolve()` is called eagerly in `AppBootstrap.InitializeRoutine` specifically so Android's TTS engine has time to finish async init before the first Listen tap |
| 12 | Import KML, close the gap | **not started** | `KmlImporter.cs` is written but unused — there is no `ImportKmlTool` editor menu item calling it. Route is still 9 GeoJSON features / 165 vertices / 2D-only, and the 1,171.7 m straight-line gap is still open |
| 13 | Content pass | **not started** | No landmark photographs, no Noto TTFs, no te/hi/ta/kn translation files, no recorded audio, no real map tiles |
| 14 | Full field test | **unblocked, not yet run** | `DebugOverlay.cs` (213 ln, now also surfacing GPS source/status) and `GpsTraceRecorder.cs` (170 ln) both exist and are wired into `UIRoot.cs:99-103`, resolving the original blocker. The field test Rev 2 §11 describes can now actually be run; it hasn't been yet — that's still Phase D below |

---

## 02 · The route — unchanged from Rev 2, re-verified

Computed from `Assets/StreamingAssets/Route/alipiri_mettu.geojson` (a copy of `Assets/Docs/alipiri_mettu.geojson`): **9 GeoJSON features** — 7 `LineString` ways plus 2 `Point` nodes — **165 total vertices, strictly 2-dimensional** (no altitude in any coordinate triple). `way/365041854` (the 84.6 m parallel spur, 116.6 m off the main chain) is still present in the source data and is correctly excluded by `RouteBuilder` at load time.

**The one risk that still outranks everything else:** OpenStreetMap has never digitised the middle
1,171.7 m of the Alipiri stairway (16.1% of the 7,288.6 m total route). The route builder bridges it
with a straight line, and a straight line across a switchbacking mountain staircase points through
the hillside. `RouteOverlay` and `DynamicArrowManager` both already know to flag this — bridged
waypoints render amber (§01, Phase 08) — but until real geometry lands, AR arrows on that 16% of
the walk cannot be trusted. Plan any live demo to end before the gap, or to route around it on the
map screen.

- **Start (Alipiri):** `13.646761, 79.405174` · **End (Tirumala):** `13.672393, 79.351832`
- Total route length: **7,288.6 m** · surveyed geometry: 6,116.9 m · bridged: 1,171.7 m

---

## 03 · Defect register

Found by direct inspection this session. Ordered by severity; each produces a visible or
structural problem if left as-is.

| ID | Sev | Defect | Evidence | Fix |
|---|---|---|---|---|
| D1 | ~~high~~ **fixed** | ~~AR pose fusion is materially thinner than planned...~~ `HybridLocalizationEngine` is now 101 lines: GPS-outlier rejection scaled against `LocationProvider.LowAccuracyThresholdMeters` (`:18`), a `ConsecutiveGpsRejections` counter that forces a re-seed, and a `LastReseedReason` string surfaced to `DebugOverlay` | `HybridLocalizationEngine.cs:16-18, 62` | — |
| D2 | ~~high~~ **fixed** | ~~No `Diagnostics/DebugOverlay.cs`, no `Diagnostics/GpsTraceRecorder.cs`~~ Both now exist and are constructed in `UIRoot.cs:99-103`, attached to the AR bootstrapper/localization/placement in `ARNavigationScreen.cs:109-110, 189-192` | `Diagnostics/DebugOverlay.cs` (213 ln), `Diagnostics/GpsTraceRecorder.cs` (170 ln) | — |
| D3 | **high** | Zero git commits. ~9,700 lines of app code and a 65 MB built APK exist with no version control at all | `git log` → "does not have any commits yet"; `git status` shows everything untracked | Initial commit now, before any further edits risk being unrecoverable |
| D4 | med | Duplicate landmark: **"Dorasani Mandapam" appears twice**, id 31 and id 32, with identical `voiceText` | `Assets/StreamingAssets/Database/landmarks.json` | Delete id 32 (or differentiate if it's meant to be a second, distinct location) |
| D5 | med | All 41 landmark rows still point at `"audio": "Audio/temple1.mp3"`, a file that does not exist anywhere in the project | `landmarks.json`, all 41 rows | Not a blocker for v1 — `voiceText` carries real English and TTS covers it — but the dead path should be nulled or documented rather than left pointing at a phantom file |
| D6 | med | No `description` field on landmark entries. Rev 2 §04 called for one specifically so display copy and voice copy can diverge; `LandmarkPopup` currently binds the shared `VoiceText` into the description slot | `landmarks.json` schema (`id, name, type, latitude, longitude, triggerRadius, voiceText, audio, arPrefab, priority, visited` — no `description`); `LandmarkPopup.cs:115` | Add `description`, falling back to `voiceText` when absent, so existing data keeps working |
| D7 | med | **`INTERNET` permission is present in the shipped APK** despite "fully offline" being a headline scope decision in both Rev 2 and Rev 3 | Manifest extracted from `Builds/AlipiriAR.apk`: `ACCESS_COARSE_LOCATION`, `ACCESS_FINE_LOCATION`, `CAMERA`, **`INTERNET`**, `VIBRATE` | Player Settings → Android → Internet Access → set to **Not Required** / Disabled, rebuild, re-verify the manifest |
| D8 | med | The Indic font fallback chain names Windows-only font families. On Android these resolve to nothing, so the moment a te/hi/ta/kn translation file exists, that text renders as tofu | `UITheme.cs:70` — `{"Nirmala UI", "Gautami", "Mangal", "Latha", "Tunga", "Segoe UI", "Arial", "Noto Sans"}` | Bundle real Noto Sans {Telugu,Devanagari,Tamil,Kannada} TTFs and reference them directly, rather than relying on OS font-name resolution |
| D9 | ~~low~~ **fixed** | ~~One hardcoded English string bypasses the localisation layer~~ `MapScreen.cs:55` now calls `Loc.T("map.route_unavailable")` | `MapScreen.cs:55` | — |
| D10 | low | Stray scenes and folders left over from iteration: `Assets/1.unity`, `Assets/_Recovery/0.unity`, an empty `Assets/Docs/New folder` | filesystem; only `Assets/Scenes/Pages.unity` is registered in `ProjectSettings/EditorBuildSettings.asset` | Delete once confirmed unneeded — low risk, but they clutter the project and could confuse a future build-scene picker |
| D11 | **high — fixed this session, two parts** | **Part 1 (source):** `NavigationSession.Resolve()` (`:84`) was the only site that ever constructed a `LocationProvider`, and it called only `LocationProvider.CreateTraceReplay` — the real-GPS path (`RealGpsRoutine`) was fully written but unreachable, since no factory ever set `_useTrace = false`, so a device build would have run on the simulated walker too. **Part 2 (the actual reported symptom):** independent of source, both `ARNavigationScreen.OnShown()` and `MapScreen.OnShown()` called `_session.Start()` unconditionally the first time their tab opened — navigation began itself with no user action at all, which is why the nav board visibly climbed on its own in the Editor (confirmed by the user: reproduces in Play mode) even after Part 1's fix, since the Editor correctly keeps the trace harness and Part 1 never touched *when* it starts, only *which source* drives it once started. Compounding: a denied permission or disabled location service in the real-GPS path only logged a warning and silently stalled, indistinguishable on-screen from standing still | `Positioning/NavigationSession.cs:84` and both screens' `OnShown()` (before fix) | **Fixed:** added `LocationProvider.CreateDeviceGps()`; source now picked by `Application.isEditor`. Added `LocationSourceMode`/`LocationSourceStatus`, surfaced in `DebugOverlay` as `Source  GPS/SIM  <status>`. Removed the auto-`Start()` from both screens' `OnShown()` — `NavigationSession.Start()` is now called from exactly one place, a new "Start Navigation" row in `NavigationDrawer` (`nav.drawer_start`, tri-state with the existing Pause/Resume row based on `NavigationState`). Auto-`Resume()` after a `Pause` is intentionally kept, since resuming isn't a fresh start |

---

## 04 · What remains — six phases, ordered by what unblocks what

Rev 2 folded instrumentation *into* the field-test phase (its old Phase 14). That ordering no
longer makes sense: everything that can be verified at a desk already has been, and the only
unknowns left in this project are physical — whether AR chevrons actually land on real stone steps,
whether the pose fusion holds up under real GPS noise and real ARCore drift. You cannot measure a
physical unknown with an instrument you haven't built, so instrumentation (Phase B below) has to
come *before* the device walk (Phase D), not during it.

| # | Phase | Contents | Est. |
|---|---|---|---|
| **A** | **Make the work safe** | D3 — initial git commit. D10 — delete stray scenes/folders. D7 — disable Android Internet Access and rebuild. All cheap, no dependencies, do first. | ~1 h |
| **B** | ~~Build the instruments~~ **done** | `Diagnostics/DebugOverlay.cs` and `Diagnostics/GpsTraceRecorder.cs` exist and are wired into `UIRoot.cs:99-103`. D2 resolved. | — |
| **C** | ~~Harden the AR pose fusion~~ **done** | `HybridLocalizationEngine` now has an accuracy-scaled outlier gate, `ConsecutiveGpsRejections`, and a forced re-seed path, surfaced to `DebugOverlay`. D1 resolved. | — |
| **D0** | **New — fix the position-source bug found while wiring Phase D up** | D11: `NavigationSession.Resolve()` only ever constructed a trace-replay `LocationProvider`, so the nav board auto-advanced on its own regardless of build target. Fixed: added `LocationProvider.CreateDeviceGps()`, source now picked by `Application.isEditor`, source mode/status surfaced in `DebugOverlay`. **This was silently blocking Phase D** — a device walk under the old code couldn't have produced real tuning data no matter how carefully C was written, since real GPS was never actually driving the session. | done |
| **D** | **⚠ First device walk — the hard gate** | Install the APK to a real Android phone, walk the first 200 steps of the actual stairway with `DebugOverlay` visible and `GpsTraceRecorder` running. Nothing downstream matters until chevrons visibly sit on real treads rather than floating or drifting. Expect the output of this phase to be *tuning numbers* (reject thresholds, ground-tier fallback timing) rather than new code — everything before this phase is desk-verifiable, nothing after it is. | ~1 week, mostly field time |
| **E** | **Data + content correctness** | D4 (delete duplicate landmark), D5 (resolve dead audio path), D6 (add `description` field). `IBasemapSource`/`TileBasemap` and D9 are already done (§01). Remaining: D8 — bundle real Noto TTFs and produce the four translation JSON files. | ~1–2 days + translation/content time |
| **F** | **Close the route gap** | Add the missing `ImportKmlTool` editor menu item over the already-written `KmlImporter`, merge real KML geometry when it arrives, re-derive total route length, clear `isBridged` on the newly-real waypoints, drop the amber rendering. If the KML carries altitude, the elevation chart and a real (non-estimated) step count both light up in the same pass, for free. If a KML never arrives, Phase B's `GpsTraceRecorder` doubles as the survey tool — physically walk the 1.17 km gap with it running and merge the resulting trace into `alipiri_mettu.geojson` instead. | ~1 day (+ field time if self-surveying) |

---

## 05 · Scope decisions — still standing, several now delivered

These decisions from Rev 2 §"Scope locked for v1" have not changed. What's new is which of them
are now actually built rather than merely decided.

| Decision | Status |
|---|---|
| **Login screen** capturing name/age/language → `Assets/login.xlsx` | **Delivered** — `LoginScreen`, `XlsxWriter`, `ExcelLoginStore` all functional, `login.xlsx` exists on disk from a real run |
| **Everything written fresh** in `AR_pages`, nothing ported from `AR_Navigation` | Holds — confirmed no code sharing; `HybridLocalizationEngine` here is a much thinner independent implementation than the sibling project's (see D1, and consider skimming that project's `HybridLocalizationEngine.cs` for the outlier-rejection pattern to adapt, not copy) |
| **Demo basemap**, no tile licence question | **Delivered**, but not yet behind an interface — see D8/Phase E |
| **Only `Assets/Docs/` data** for v1 — English only, procedural landmark tiles, empty-state elevation, labelled step estimates | **Delivered** — elevation and step-count fallbacks confirmed honest and correctly labelled (§01, Phase 07) |
| **Straight-line gap until KML lands** | Holds — still open, still amber-rendered, still the largest single risk (§02) |

---

## 06 · Still open — carried from Rev 2 §13

| Open item | Needed by | Why it matters |
|---|---|---|
| **Does your KML export carry altitude?** | Phase F | Unlocks a real elevation profile and a real step count at zero extra cost — check the export options before generating it |
| **What should `age` drive?** | content pass | Still logged and unused. Candidates from Rev 2 still stand: rest reminders for 60+, larger base type size, gentler ETA pacing |
| **Confirm the language list** | before translation work in Phase E | Telugu/Hindi/Tamil/Kannada are already wired into the picker (`LocalizationService.cs:34-37`) and just need `.json` files — say now if that list is wrong, before translation effort is spent |

Nothing else is required to begin. Phase A starts with `git add` and a first commit.

---

*Route figures computed from `Assets/StreamingAssets/Route/alipiri_mettu.geojson` via haversine ·
165 vertices · 7 LineString ways · 7,288.6 m total.*
*Route data © OpenStreetMap contributors, ODbL — attribution required in the shipped app.*
