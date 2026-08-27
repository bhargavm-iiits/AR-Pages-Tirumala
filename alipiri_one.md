# Alipiri AR — Status + Geospatial Heritage Plan (Rev 4)

Supersedes `alipiri.md`. Verified 2026-08-27 by reading the actual repository — source files,
`Packages/manifest.json`, `ProjectSettings/*`, `Assets/Plugins/Android/AndroidManifest.xml`, the
generated `landmarks.json`, and `Builds/` — not inferred from filenames.

A fully offline outdoor AR wayfinding app for the 7.29 km pilgrim stairway from Tirupati to
Tirumala. ~78 scripts, ~10,000 lines, 12 screens, and a 46 MB APK built 2026-08-27 13:46.

**The new work:** a connected Geospatial heritage layer at the two route ends, with the existing
offline navigation carrying the middle. **Verdict: adopt it — do not rebuild the app.** Four
corrections and four blockers follow in §04–§05.

---

# PART I — WHERE THE PROJECT ACTUALLY STANDS

## 01 · Corrections to `alipiri.md`

| `alipiri.md` claimed | Actually true | Evidence |
|---|---|---|
| Phase 10 is the "real unknown" — AR "obviously untested outdoors" | **AR has run on a real phone repeatedly and produced fixes.** 32 `// found on a real device` / `// confirmed on-device` comments across 25 files, three about AR behaviour specifically | `ARSessionBootstrapper.cs:139` — a door got tracked as a plane, detection mode restricted to `Horizontal`; `GroundPlacementService.cs:25` — `MinGroundUpDot` vertical-surface backstop; `NavigationArrow.cs:28` — chevrons rotating flat into the ground plane |
| 41 landmarks | **40** — the duplicate "Dorasani Mandapam" (Rev 3 D4) was resolved | `landmarks.json` → `len(landmarks) == 40` |
| ~17 files "not yet built" | **Most were never separate classes — the functionality exists, folded elsewhere.** Judging a Unity project by whether planned filenames exist produces a false negative | Elevation empty state at `ProgressScreen.cs:174-186`; step estimate `TotalStepsEstimate = 3550` at `ProgressScreen.cs:25`; `IBasemapSource` + `TileBasemap.cs` both exist |

What it got right: the completed-work inventory, the deferred-content list, and zero git commits.

## 02 · Status by phase

| # | Phase | Status | Note |
|---|---|---|---|
| 01 | Project hygiene | **partial** | Names and bundle id correct. **Zero git commits** against ~10,000 lines and a built APK |
| 02 | AR packages + Android settings | **done** | AR Foundation 6.5.0, ARCore 6.5.0, XR Management 4.6.1; IL2CPP, ARM64, minSdk 26; manifest verified against a real build with `aapt dump` |
| 03 | UI framework + theme | **done** | `UITheme`, `UIFactory`, `UIShapes`, `UITween`, `IconGraphic`, `ResponsiveUI`, `SafeAreaFitter`, `ConfettiBurst` |
| 04 | Localisation core | **partial** | Working, `en.json` has 130 keys. Picker offers te/hi/ta/kn but only `en.json` exists. **D8 open** — `UITheme.cs:70` names Windows-only fonts absent on Android |
| 05 | Route + landmark data | **done** | Full parse/build/validate chain. 40 landmarks, types normalised, longitude typo fixed, duplicate removed, `Description` added with `VoiceText` fallback (`JsonDatabase.cs:92`) |
| 06 | Shell, login, Excel | **done** | `Assets/login.xlsx` exists from a real Play-mode run |
| 07 | Landmarks, Progress, Settings | **done** | Elevation shows an honest empty state; steps always prefixed `~` |
| 08 | Demo map + live position | **done** | `IBasemapSource` + `DemoBasemap` + `TileBasemap`; bridged waypoints render amber |
| 09 | Positioning + session logic | **done** | Real GPS + trace replay selected by `Application.isEditor`. Rev 3's D11 fixed |
| **10** | **AR navigation on device** | **built, device-exercised, not yet instrumented** | Full chain wired: `ARSessionBootstrapper` → `GroundPlacementService` (3 tiers) → `HybridLocalizationEngine` (101 ln, outlier rejection + re-seed) → `DynamicArrowManager`. **What hasn't happened: a measured walk** producing tuning numbers |
| 11 | Voice via TTS | **done** | Clip → TTS → caption chain, resolved eagerly in `AppBootstrap` |
| 12 | Import KML, close the gap | **not started** | `KmlImporter.cs` written but unused. The 1,171.7 m gap is still open |
| 13 | Content pass | **not started** | No photos, Noto TTFs, translations, recorded audio, or real tiles |
| 14 | Full field test | **unblocked, not run** | `DebugOverlay.cs` (213 ln) + `GpsTraceRecorder.cs` (170 ln) wired in `UIRoot.cs:99-103` |

## 03 · Open defects

| ID | Sev | Item |
|---|---|---|
| D3 | **high** | **Zero git commits.** ~10,000 lines and a 46 MB APK, no version control. Highest-priority item in the project |
| D5 | med | All landmark rows point at `Audio/temple1.mp3`, which doesn't exist. Not a blocker — TTS covers it — but the dead path should be nulled |
| D8 | med | Indic font fallback names Windows-only families. Invisible today; becomes tofu the moment a translation exists — and §09 makes it blocking |
| D10 | low | Stray `Assets/1.unity`, `Assets/_Recovery/0.unity`, empty `Assets/Docs/New folder` |

---

# PART II — THE GEOSPATIAL HERITAGE LAYER

## 04 · Four blockers

Not objections to the direction — the actual cost of entry. Three are decisions, not code.

### ⚠ B1 — This app deliberately strips the INTERNET permission

`Assets/Plugins/Android/AndroidManifest.xml` carries:

```xml
<uses-permission android:name="android.permission.INTERNET" tools:node="remove" />
```

with a six-line comment recording that this line — not `PlayerSettings.ForceInternetPermission`,
which was already false — is what actually removes it, because ARCore's bundled AAR requests
INTERNET unconditionally and Android's manifest merger keeps a library permission unless the app
explicitly strips it. Verified against a real Release build with `aapt dump`.

Geospatial cannot function without INTERNET. Adopting it means **deleting that line and reversing
a headline scope decision** present in Rev 2, Rev 3, and the app's own About framing. That is a
legitimate trade — the connected-ends/offline-middle split is the right shape for it — but it must
be a stated decision, not a side effect of adding a package. It also changes what the app owes a
pilgrim: an app requesting network access needs a truthful answer about what leaves the device.

**Recommendation:** accept it, and restate the scope as "offline navigation, connected heritage at
the two ends" in About and Settings.

### ⚠ B2 — ARCore Extensions is not a registry package

`manifest.json` has `com.unity.xr.arfoundation` 6.5.0 and `com.unity.xr.arcore` 6.5.0. The
Geospatial API lives in **`com.google.ar.core.arfoundation.extensions`** — Google's separate
package, distributed as a `.tgz` from the `arcore-unity-extensions` GitHub releases, not the Unity
registry, so Package Manager will not find it.

It carries its own AR Foundation compatibility matrix. **AR Foundation 6.5 is recent enough that
support must be verified, not assumed.** If the newest Extensions release supports only 6.0–6.2,
the choice is downgrading AR Foundation across a working, device-tested AR stack, or waiting.
Settle this before anything else.

### ⚠ B3 — Geospatial needs a Google Cloud project, not just a package

The ARCore API must be enabled in a Cloud project and the app authorised by API key or keyless
(which binds to the signing certificate). No cloud footprint exists today. An afternoon of setup —
but one that introduces a signing-config dependency `Assets/Editor/BuildScript.cs` does not have.

### ⚠ B4 — `ARSessionBootstrapper` builds the AR hierarchy in code, from no prefab

`BuildHierarchy()` (`:101-145`) constructs AR Session, XR Origin, Camera Offset, AR Camera,
`TrackedPoseDriver` with hand-built `InputAction` bindings, `ARPlaneManager` and `ARRaycastManager`
entirely in C#, deliberately, verified against the installed package's own `XROriginCreateUtil.cs`.
ARCore Extensions expects an `ARCoreExtensions` component with a config asset wired in a scene.

Doable procedurally — `originGo.AddComponent<ARCoreExtensions>()` alongside the existing managers,
config as a `ScriptableObject` from `Resources/` — but it is the one point where this project's
procedural philosophy meets a package that assumes the editor. Verify against installed package
source, the way `ARSessionBootstrapper` already documents doing.

## 05 · The check that runs before any of this

**Does VPS have coverage at Alipiri and Tirumala?**

`PLAN.md:390` rejected the Geospatial API partly on this: VPS coverage derives from Street View
imagery, and a tree-canopied covered stairway has essentially none. The premise here is that the
*ends* differ from the middle — an open road-accessible plaza at the Alipiri arch, a town at
Tirumala. Plausible, unverified, and everything downstream is worthless if it is false.

1. **Desk check, ~30 min, free.** Street View at `13.646761, 79.405174` (start) and
   `13.672393, 79.351832` (end). Blue lines = imagery = probable coverage. No blue = near-certain
   none. This alone can end the direction before a package is installed.
2. **Device check, after B1–B3.** `AREarthManager.CheckVpsAvailabilityAsync(lat, lon)` at both
   endpoints, on a real phone, on network. Authoritative. Log it.

If coverage is absent at both ends, the honest fallback is not "build it anyway" — it is that the
existing `GeoAnchorFrame` + GPS + route-snap fusion, already device-tested, stays the only
localization this route supports, and heritage content is placed by that with correspondingly
looser accuracy claims. Smaller, cheaper, and not a failure.

## 06 · Where it attaches — the real seams

### The pose seam — `HybridLocalizationEngine.FeedFix`

```
FeedFix(lat, lon, compassHeadingDeg, accuracyMeters)   // AR/HybridLocalizationEngine.cs:58
```

Already has an accuracy-scaled outlier gate (`:70`), a consecutive-rejection counter (`:78`), and
a forced re-seed path. **A Geospatial pose is just a better fix through the same door.**
`AREarthManager.CameraGeospatialPose` yields latitude, longitude, heading and horizontal accuracy —
the exact four arguments `FeedFix` accepts, at ~1 m rather than the ~5–25 m `LocationProvider`
delivers.

Highest-leverage integration in the plan: feed Geospatial pose in when Earth tracking is healthy,
fall back to GPS when it is not, and **every existing consumer — arrows, progress, triggers, map
marker — inherits the accuracy with no change.** Not a second localization engine.

**Correction:** the proposal lists `GeospatialManager`, `VPSManager` and `GeospatialPoseProvider`
as three classes. Two suffice — `GeospatialSession` (lifecycle, VPS availability, Earth tracking
state) plus the `FeedFix` bridge, which is ~30 lines and needs no class of its own.

### The trigger seam — `LandmarkTriggerService.OnArrived`

```csharp
public event Action<LandmarkData> OnArrived;   // Positioning/LandmarkTriggerService.cs:18
```

Already dedupes per session (`_firedThisSession`) and writes through `VisitedStore` so automatic
and manual visits cannot disagree. `ARNavigationScreen.OnArrived` (`:652`) is one subscriber.
**The heritage experience manager is a second subscriber**, filtering on `geospatialEnabled`. Costs
nothing — no new geographic maths, no second radius check, no way for the two to disagree.

### The anchor gap — why `ARAnchorService` is genuinely needed now

**Correction:** its absence is deliberate, not deferred. Navigation arrows are pooled and re-placed
every frame by `DynamicArrowManager.Refresh` (`:38`) via `GroundPlacementService.PlaceAtGroundXZ`,
so they never need to persist and anchors would be pure cost. `GeoAnchorFrame.GeoToWorld` (`:39`)
is a **local planar approximation with no altitude term at all** —
`return _originArPos + new Vector3(localX, 0f, localZ)` — fine for a chevron a ground raycast
re-places vertically anyway, useless for a statue that must stand at a correct height on a slope.

Heritage content is the opposite case: it must stay put while the walker circles it, at a correct
altitude. That is exactly what a **Terrain anchor** provides. So build `ARAnchorService` — but for
persistent heritage content, and leave the navigation arrows on the existing anchor-free path.
Do not retrofit anchors onto the chevron trail; it is device-tested and anchors would regress it.

### Folder layout — do less than proposed

**Correction:** skip the `AR/Core|Navigation|Geospatial` reshuffle. Moving files in Unity moves
`.meta` files and rewrites GUID references; doing that across a codebase with zero git commits is
gratuitous risk for zero functional gain. Add `AR/Geospatial/` and `Heritage/` alongside what
exists; leave the six working AR files where they are.

## 07 · Data model — extend, don't fork

`landmarks.json` today: `id, name, type, latitude, longitude, triggerRadius, voiceText, audio,
arPrefab, priority, visited`. `LandmarkData` mirrors it plus `Description`,
`CumulativeDistanceMeters`, `SnappedLatitude/Longitude`.

Follow the precedent `Description` already set — parse with a fallback so every existing entry
keeps working untouched (`JsonDatabase.cs:92`):

```json
{
  "id": 1,
  "...": "existing fields unchanged",

  "zone": "START",              // START | ROUTE | END   — default "ROUTE"
  "geospatialEnabled": true,    // default false
  "anchorType": "TERRAIN",      // TERRAIN | WGS84       — default TERRAIN
  "experienceId": "EXP_L001"    // null = navigation-only landmark
}
```

Absent field → default → behaves exactly as today. Of the 40 landmarks, target **3–5 at the start
zone and 3–5 at the end zone**. The other ~30 stay navigation landmarks, untouched. Do not turn
all 40 into AR experiences.

Heritage content itself belongs in a **separate file** (`StreamingAssets/Database/heritage.json`),
keyed by `experienceId`, so narration/model/video/attribution can be revised without touching the
navigation database or reloading route geometry.

**One consequence the proposal misses:** heritage content is text-and-narration heavy in a way
navigation never was, which makes **D8 blocking** where it previously was not. `UITheme.cs:70`
builds its Indic fallback chain from Windows font *names* that do not exist on Android. Today only
`en.json` ships so nothing renders as tofu; the moment a Telugu heritage story exists, it renders
as boxes. Bundle real Noto TTFs **before** commissioning any translated heritage copy.

## 08 · Phases

Continues Rev 3's lettering. Rev 3's Phase D — the instrumented walk — still gates everything.

| # | Phase | Contents | Gate | Est. |
|---|---|---|---|---|
| **A** | **Commit the work** | D3. `git add` + initial commit of ~10,000 lines and the current APK. `.gitignore` already staged. Nothing below starts first — an architectural change on a month of uncommitted work has no undo | — | ~1 h |
| **G0** | **Kill-or-continue check** | §05 desk check: Street View coverage at both endpoints. Half an hour, can end this direction. Before installing anything | **blocks G–N** | ~30 min |
| **D** | **The instrumented walk** (Rev 3, unchanged) | First 200 steps with `DebugOverlay` visible and `GpsTraceRecorder` running. Output is tuning numbers — reject thresholds, tier distribution — not code. The AR core has been on a phone (§01) but never *measured* | **gates L** | ~1 week field |
| **G** | **Entry cost** | B1 decide INTERNET + restate scope. B2 verify Extensions ↔ AF 6.5, install `.tgz`. B3 Cloud project, ARCore API, auth + signing. B4 attach `ARCoreExtensions` inside `BuildHierarchy()`. Ends when an APK boots with Earth tracking reaching `Tracking` | B1–B4 | ~3–4 days |
| **H** | **Truth on the mountain** | `CheckVpsAvailabilityAsync` at both endpoints on device, on network. Log horizontal + heading accuracy standing still at the Alipiri arch for 60 s. **This number decides whether content places to ±1 m or ±10 m** — everything in J is designed against it | after G, G0 | ~1 day |
| **I** | **The bridge** | `GeospatialSession` + the `FeedFix` bridge (§06). Surface Earth state, horizontal accuracy and active pose source in `DebugOverlay` beside the existing `Source GPS/SIM` row. No content yet — the win is that existing navigation gets measurably more accurate at the ends before any heritage work exists | after H | ~2 days |
| **J** | **One anchor, one object** | `ARAnchorService` — Terrain anchor create/resolve/remove/state. Place **one** cube at **one** real landmark. Walk toward it, around it, away and back. Measure whether it holds across a session and across app restarts. **Do not build a character, a story or a UI until this is boring** | after I | ~2–3 days + field |
| **K** | **First heritage experience** | Extend `landmarks.json` + `LandmarkData` + `JsonDatabase` per §07. `HeritageExperienceManager` subscribing to `LandmarkTriggerService.OnArrived`. One real landmark: anchored 3D representation, info card reusing `LandmarkPopup`'s treatment, narration through the existing `VoiceNavigationManager` TTS chain — **no new audio pipeline for v1** | after J | ~1 week |
| **L** | **Zone integration** | `RouteModeManager`: `START_GEO` → `OFFLINE_NAV` → `END_GEO`, boundaries from Phase H's measured coverage, not guessed. Transitions honest in both directions — entering the offline middle says so and degrades to the device-tested navigation rather than failing. Extend `NavigationSession`'s state machine, don't add a parallel one | after D, K | ~3–4 days |
| **M** | **Scale to 3–5 per zone** | Repeat K for the remaining chosen landmarks. Content-bound, not code-bound. Noto TTFs land here (§07) before any translated copy is commissioned | after L | content-paced |
| **N** | **Backend — only if needed** | `ContentDownloadManager` / `LocalContentCache` / CDN. Right to defer. With 6–10 experiences, bundling in `StreamingAssets` is simpler, works on a weak connection, and removes a whole class of failure. Add a server when content volume demands it | after M | defer |

**Phases A and G0 are today.** One is insurance; the other can save weeks.

## 09 · Definition of done

```
✅ Offline navigation                     — already done, do not regress it
✅ Instrumented device walk (Rev 3 D)     — the outstanding gate
✅ Earth tracking reaching Tracking at both endpoints, with a logged accuracy figure
✅ VPS availability measured, not assumed
✅ Terrain anchors stable across a session AND an app restart
✅ 3–5 START heritage experiences
✅ 3–5 END heritage experiences
✅ START_GEO → OFFLINE_NAV → END_GEO transitions, honest in both directions
✅ Graceful degradation: no network / no VPS / Earth not tracking → existing navigation, stated plainly
✅ Narration via the existing TTS chain
✅ Noto TTFs bundled before any translated heritage copy
✅ Earth state + pose source + anchor count in DebugOverlay
```

Explicitly **not** in v1: a backend, a CDN, remote content management, analytics, animated
characters. All reachable from this architecture later; none on the critical path to proving the
idea works on the mountain.

## 10 · Decisions

| Decision | Status | Notes |
|---|---|---|
| **Accept the INTERNET permission?** | **✓ decided 2026-08-27 — accept** | `AndroidManifest.xml`'s `tools:node="remove"` on INTERNET removed; the permission is now live in the manifest. Recorded inline in the manifest's own comment block, with the reasoning preserved rather than deleted. No code opens a socket yet — `GeospatialSession` is still the documented no-op from Phase I until Phase G's package + Cloud project exist — so this grants the permission ahead of the capability that will use it, deliberately, so Phase G isn't blocked re-adding it later. About/Settings copy audited: no "fully offline" or "no data leaves your device" claim exists anywhere in the shipped strings today, so nothing is currently false. Add a network-use disclosure line to About once Phase G actually makes a network call — not before, since claiming a capability that doesn't exist yet would be its own kind of dishonesty |
| **Which 3–5 landmarks per zone?** | **✓ decided 2026-08-27 — 4 + 4** | Picked by proximity to each endpoint (haversine against the Alipiri arch and Tirumala end) plus type — excluding pure-utility entries (water point, medical center, plain step-count markers). **START** (all within 215 m of the arch): id 6 Rajagopuram, id 4 Venkateshwara Swamy Padalu Mandapam, id 5 Mathsyavataram, id 7 Baktha Anjaneya Swamy Statue. **END** (within 555 m of Tirumala): id 41 Alipiri Last Step, id 40 Alwar Statue, id 39 Kulashekhar Alvar, id 38 Dova Bashyakarla Sannidhi. Flagged in `landmarks.json` with `zone`/`geospatialEnabled: true`/`anchorType: TERRAIN`/`experienceId` — the other 32 entries are untouched. **This is a proximity+type judgment call, not a cultural or narrative one** — swap any entry by editing one JSON object, no code change |
| **What is the heritage content, and who writes it?** | **still open** | Not decided here, deliberately. The 8 flagged landmarks keep their existing one-line `voiceText`/`description` — no heritage narration was written or invented for them. Per the note directly above this row in the previous revision: for a pilgrimage site this wants a source and a reviewer, not a generated draft. `heritage.json` (§07) doesn't exist yet; Phase K is where this gets answered |
| **Does the KML carry altitude?** (from Rev 3) | open | Terrain anchors reduce the need, but altitude still improves the elevation profile and step count for free |
| **The 1,171.7 m route gap** | open | OSM never digitised the middle 16.1%. Rendered amber and honestly flagged, but AR arrows there cannot be trusted until the KML lands — or until the gap is self-surveyed with `GpsTraceRecorder`, which already works |

---

*Route data © OpenStreetMap contributors, ODbL — attribution required in the shipped app.*
*`Docs/Draft1.md` (Rev 3) remains the navigation app's phase record; `PLAN.md` (Rev 2) remains the
historical record and its route-geometry figures are still accurate.*
