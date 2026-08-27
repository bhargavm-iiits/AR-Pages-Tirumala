# Alipiri AR — Geospatial Heritage Layer (Rev 4)

**Target:** Unity 6000.5.5f1 · URP 17.5 · AR Foundation 6.5 · ARCore 6.5 · Android
**Rev 4 — extends `Docs/Draft1.md` (Rev 3).** Rev 3 is still the plan-of-record for the navigation
app; this document covers only the new Geospatial heritage layer and where it attaches.

Everything below was verified by reading the actual repository this session — source files,
`Packages/manifest.json`, `Assets/Plugins/Android/AndroidManifest.xml`, the generated
`landmarks.json`, and `Builds/` — not carried forward from Rev 2 or Rev 3's picture.

**Verdict on the proposal: adopt it, with four corrections and four blockers it does not mention.**
The core judgement — *do not rebuild; add Geospatial as a layer on the existing AR core* — is
correct, and this codebase is in better shape to receive it than the proposal assumes.

---

## 01 · The record, corrected

Rev 3 (`Draft1.md`, updated 2026-08-20) is now stale in three ways that matter for this plan.

| Rev 3 said | Actually true today | Evidence |
|---|---|---|
| Phase 10 "entirely device-unverified", "none of it has run on a phone" | **AR has run on a real phone, repeatedly, and produced bug fixes.** 32 separate `// found on a real device` / `// confirmed on-device` comments across 25 files — including three in the AR core specifically about *AR behaviour*, not UI | `ARSessionBootstrapper.cs:139` (a door tracked as a plane → detection mode restricted to Horizontal), `GroundPlacementService.cs:25` (vertical-surface backstop, `MinGroundUpDot`), `NavigationArrow.cs:28` (chevrons rotated flat into the ground plane) |
| 41 landmarks, D4 duplicate open | **40 landmarks**, duplicate resolved | `landmarks.json` — `len(landmarks) == 40` |
| D6 open (no `description` field) | **Fixed** — `LandmarkData.Description` exists and falls back to `VoiceText` on parse | `Data/LandmarkData.cs:16`, `Database/JsonDatabase.cs:92` |
| D7 open (INTERNET in the APK) | **Fixed, and deliberately so** — see §02, this is now the single biggest obstacle | `AndroidManifest.xml` — `tools:node="remove"` on INTERNET |

Still open from Rev 3, unchanged: **D3 — zero git commits.** There are now ~10,000 lines and a
46 MB APK built 2026-08-27 13:46, with no version control at all. Phase 08 Aug 13 → today is a
month of uncommitted work. This is Phase A below and it is not optional before an architectural
change of this size.

**What this means for the proposal:** its Phase I step 1 ("finish outdoor testing of current AR
navigation") is further along than it assumes. The AR arrows have been on real stone. What has
*not* happened is a full instrumented walk with `DebugOverlay` + `GpsTraceRecorder` producing
tuning numbers — Rev 3 Phase D. That still gates everything.

---

## 02 · Four blockers the proposal does not mention

These are not objections to the direction. They are the actual cost of entry, and three of them
are decisions rather than code.

### ⚠ B1 — This app deliberately strips the INTERNET permission

`Assets/Plugins/Android/AndroidManifest.xml` carries:

```xml
<uses-permission android:name="android.permission.INTERNET" tools:node="remove" />
```

with a 6-line comment recording that this line — not `PlayerSettings.ForceInternetPermission`,
which was already false — is what actually removes it, because ARCore's own bundled AAR requests
INTERNET unconditionally and Android's manifest merger keeps a library permission unless the app
explicitly strips it. Verified against a real Release build with `aapt dump`.

The Geospatial API cannot function without INTERNET. Adopting it means **deleting that line and
reversing a headline scope decision** that appears in Rev 2, Rev 3, and the app's own About screen
framing. That is a legitimate trade — the proposal's connected-start/end/offline-middle split is
exactly the right shape for it — but it must be a stated decision, not a side effect of adding a
package. It also changes what the app must say to a pilgrim: an app that requests network access
needs a truthful answer about what leaves the device.

**Recommendation:** accept it, and scope it honestly — the app becomes "offline navigation with
connected heritage content at the two ends", and the About/Settings copy says so.

### ⚠ B2 — The ARCore Extensions package is not installed and is not a registry package

`Packages/manifest.json` has `com.unity.xr.arfoundation` 6.5.0 and `com.unity.xr.arcore` 6.5.0.
The Geospatial API lives in **`com.google.ar.core.arfoundation.extensions`**, which is Google's
separate package, distributed as a `.tgz` from the `arcore-unity-extensions` GitHub releases —
not from the Unity registry, so `Window ▸ Package Manager` will not find it.

It carries its own AR Foundation compatibility matrix. **AR Foundation 6.5 is recent enough that
support must be verified, not assumed** — check the release notes of the newest Extensions tag
before committing to it. If the newest Extensions release supports only AR Foundation 6.0–6.2,
the choice is downgrading AR Foundation across a working, device-tested AR stack, or waiting.
Establish this before anything else in §04 Phase G.

### ⚠ B3 — Geospatial needs a Google Cloud project, not just a package

The ARCore API must be enabled in a Google Cloud project, and the app authorised either by API key
or keyless (which binds to the app's signing certificate). Neither exists here — the project has
no cloud footprint at all today. This is an afternoon of setup, but it is an afternoon that also
introduces a signing-config dependency the current `Assets/Editor/BuildScript.cs` does not have.

### ⚠ B4 — `ARSessionBootstrapper` builds the AR hierarchy in code, from no prefab

`ARSessionBootstrapper.BuildHierarchy()` (`:101-145`) constructs AR Session, XR Origin, Camera
Offset, AR Camera, `TrackedPoseDriver` with hand-built `InputAction` bindings, `ARPlaneManager`
and `ARRaycastManager` entirely in C#, deliberately, and verified against the installed package's
own `XROriginCreateUtil.cs`. ARCore Extensions expects an `ARCoreExtensions` component with a
`ARCoreExtensionsConfig` asset and a session-origin reference wired in a scene.

Adding it procedurally is doable — `originGo.AddComponent<ARCoreExtensions>()` alongside the
existing managers, with the config as a `ScriptableObject` loaded from `Resources/` — but it is
the one integration point where this project's procedural-construction philosophy meets a package
that assumes the editor. Budget real time for it and verify against the installed package source,
the way `ARSessionBootstrapper` already documents doing for AR Foundation.

---

## 03 · The check that runs before any of this

**Does VPS have coverage at Alipiri and Tirumala?**

`PLAN.md` §390 rejected the Geospatial API partly on this: VPS coverage derives from Street View
imagery, and a tree-canopied covered stairway has essentially none. The proposal's whole premise
is that the *ends* are different from the middle — an open road-accessible plaza at the Alipiri
arch, a town at Tirumala — and that is plausible. It is also unverified, and everything downstream
of it is worthless if it is false.

Two checks, in order, before writing a line of Geospatial code:

1. **Desk check, ~30 minutes, free.** Open Google Maps Street View at
   `13.646761, 79.405174` (Alipiri start) and `13.672393, 79.351832` (Tirumala end). Blue lines =
   imagery = probable VPS coverage. No blue = near-certain no coverage. This alone can end the
   direction before any package is installed.
2. **Device check, once B1–B3 are done.** `AREarthManager.CheckVpsAvailabilityAsync(lat, lon)` at
   both endpoints, on a real phone, on network. This is authoritative. Log the result.

If coverage is absent at both ends, the honest fallback is not "build it anyway" — it is that the
existing `GeoAnchorFrame` + GPS + route-snap fusion, already device-tested, remains the only
localization this route can support, and heritage content gets placed by that instead, with
correspondingly looser accuracy claims. That is a smaller, cheaper project and it is not a failure.

---

## 04 · Where it attaches — the real seams in this code

The proposal is right that the existing localization and trigger infrastructure should be shared,
not duplicated. Here are the exact attachment points, with the corrections noted.

### The pose seam — `HybridLocalizationEngine.FeedFix`

```
HybridLocalizationEngine.FeedFix(lat, lon, compassHeadingDeg, accuracyMeters)   // :58
```

Already takes an accuracy-scaled outlier gate (`:70`), a consecutive-rejection counter (`:78`),
and a forced re-seed path. **A Geospatial pose is just a better fix through the same door.**
`AREarthManager.CameraGeospatialPose` yields latitude, longitude, heading and a horizontal
accuracy in metres — the exact four arguments `FeedFix` already accepts, at ~1 m rather than the
~5–25 m `LocationProvider` delivers.

This is the highest-leverage integration in the whole plan: feed Geospatial pose into `FeedFix`
when Earth tracking is healthy, fall back to the GPS path when it is not, and **every existing
consumer — arrows, progress, triggers, map marker — inherits the accuracy with no change.**
Not a second localization engine. One extra source into the one that already exists.

Correction to the proposal: it lists `GeospatialManager`, `VPSManager` and `GeospatialPoseProvider`
as three classes. Two are enough — `GeospatialSession` (lifecycle, VPS availability, Earth
tracking state) and the `FeedFix` bridge, which is ~30 lines and does not need its own class.

### The trigger seam — `LandmarkTriggerService.OnArrived`

```csharp
public event Action<LandmarkData> OnArrived;   // Positioning/LandmarkTriggerService.cs:18
```

Already dedupes per session (`_firedThisSession`), already writes through `VisitedStore` so
automatic and manual visits cannot disagree. `ARNavigationScreen.OnArrived` (`:652`) is one
subscriber. **A heritage experience manager is a second subscriber**, filtering on the new
`geospatialEnabled` flag. The proposal is right to insist on this and it costs nothing — no new
geographic maths, no second radius check, no possibility of the two disagreeing.

### The anchor gap — and why `ARAnchorService` is genuinely needed now

The proposal says `ARAnchorService.cs` is "deferred" and should be built first. The framing is
wrong but the conclusion is right, for a different reason.

Its absence is **deliberate, not deferred**. Navigation arrows are pooled and re-placed every
frame by `DynamicArrowManager.Refresh` (`:38`) via `GroundPlacementService.PlaceAtGroundXZ` —
they never need to persist, so anchors would be pure cost. `GeoAnchorFrame.GeoToWorld` (`:39`) is
a **local planar approximation with no altitude term at all** (`return _originArPos + new
Vector3(localX, 0f, localZ)`) — fine for a chevron the ground raycast will vertically re-place
anyway, useless for a statue that must stand at a fixed height on a slope.

Heritage content is the opposite case: it must stay put while the walker circles it, and it must
sit at a correct altitude. That is precisely what a **Terrain anchor** provides — lat/lon plus a
height relative to ground that ARCore resolves. So build `ARAnchorService`, but build it for
persistent heritage content, and leave the navigation arrows on the existing anchor-free path.
Do not retrofit anchors onto the chevron trail; it is device-tested and anchors would regress it.

### Folder layout — do less than the proposal suggests

The proposal reorganises `AR/` into `Core/`, `Navigation/` and `Geospatial/`. **Skip the reshuffle.**
Moving files in Unity moves `.meta` files and rewrites GUID references, and doing that across a
codebase with zero git commits is gratuitous risk for zero functional gain. Add `AR/Geospatial/`
and `Heritage/` alongside what exists; leave the six working AR files where they are.

---

## 05 · Data model — extend, don't fork

`landmarks.json` today, per entry: `id, name, type, latitude, longitude, triggerRadius, voiceText,
audio, arPrefab, priority, visited`. `LandmarkData` mirrors it plus `Description`,
`CumulativeDistanceMeters`, `SnappedLatitude/Longitude`.

The proposal is right that this should be extended rather than duplicated. Follow the precedent
already set by `Description` — parse with a fallback so every existing entry keeps working
untouched (`JsonDatabase.cs:92`):

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

Absent field → default → entry behaves exactly as it does today. Of the 40 landmarks, target
**3–5 at the start zone and 3–5 at the end zone** with `geospatialEnabled: true`. The other ~30
stay navigation landmarks and are untouched. The proposal's instinct here is correct: do not turn
all 40 into AR experiences.

Heritage content itself belongs in a **separate file** (`StreamingAssets/Database/heritage.json`),
keyed by `experienceId`, so narration/model/video/attribution can be revised without touching the
navigation database or reloading route geometry.

### One consequence the proposal misses

Heritage content is text-and-narration heavy in a way navigation never was. That makes **Rev 3's
D8 blocking** where it previously was not: `UITheme.cs:70` builds its Indic font fallback chain
from Windows font *names* (`"Nirmala UI"`, `"Gautami"`, `"Mangal"`, `"Latha"`, `"Tunga"`) which do
not exist on Android. Today only `en.json` ships so nothing renders as tofu. The moment a Telugu
heritage story exists, it renders as boxes. Bundle real Noto TTFs **before** commissioning any
translated heritage copy, not after.

---

## 06 · Phases

Continues Rev 3's lettering (A–F). Rev 3's D — the instrumented device walk — still gates
everything and is not superseded by any of this.

| # | Phase | Contents | Gate | Est. |
|---|---|---|---|---|
| **A** | **Commit the work** | D3. `git add` + initial commit of ~10,000 lines and the current APK. `.gitignore` already staged. Nothing below starts until this is done — an architectural change on an uncommitted month of work has no undo. | — | ~1 h |
| **G0** | **Kill-or-continue check** | §03 desk check: Street View coverage at both endpoints. Costs half an hour, can end this direction. Do it before installing anything. | **blocks all of G–L** | ~30 min |
| **D** | **The instrumented walk** (Rev 3, unchanged) | Walk the first 200 steps with `DebugOverlay` visible and `GpsTraceRecorder` running. Output is tuning numbers — reject thresholds, ground-tier distribution — not code. The AR core has been on a phone (§01) but has never been *measured*. | **gates L** | ~1 week field |
| **G** | **Entry cost** | B1: decide INTERNET, remove the `tools:node` line, document the reversal in About copy. B2: verify Extensions ↔ AR Foundation 6.5 compatibility, install the `.tgz`. B3: Cloud project, ARCore API, auth + signing. B4: get `ARCoreExtensions` attached inside `ARSessionBootstrapper.BuildHierarchy()`. Ends when an APK boots with Earth tracking reaching `Tracking`. | B1–B4 | ~3–4 days |
| **H** | **Truth on the mountain** | `AREarthManager.CheckVpsAvailabilityAsync` at both endpoints on a real device, on network. Log horizontal + heading accuracy standing still at the Alipiri arch for 60 s. **This number decides whether heritage content can be placed to ±1 m or ±10 m** — everything in J is designed against it. | after G, G0 | ~1 day |
| **I** | **The bridge** | `GeospatialSession` (lifecycle, VPS availability, Earth tracking state) + the `FeedFix` bridge (§04). Surface Earth state, horizontal accuracy and active pose source in `DebugOverlay` alongside the existing `Source GPS/SIM` row. No content yet — the win here is that existing navigation gets more accurate at the ends, measurably, before any heritage work exists. | after H | ~2 days |
| **J** | **One anchor, one object** | `ARAnchorService` — Terrain anchor create/resolve/remove/state. Place **one** cube at **one** real Alipiri landmark. Walk toward it, around it, away and back. Measure whether it stays put across a session and across app restarts. **Do not build a character, a story or a UI until this is boring.** | after I | ~2–3 days + field |
| **K** | **First heritage experience** | Extend `landmarks.json` + `LandmarkData` + `JsonDatabase` per §05. `HeritageExperienceManager` subscribing to `LandmarkTriggerService.OnArrived`. One real landmark: anchored 3D representation, info card reusing `LandmarkPopup`'s existing treatment, narration through the existing `VoiceNavigationManager` TTS chain — **no new audio pipeline for v1**, `voiceText` already proves the chain works. | after J | ~1 week |
| **L** | **Zone integration** | `RouteModeManager`: `START_GEO` → `OFFLINE_NAV` → `END_GEO`, boundaries from Phase H's measured coverage, not guessed. Transition must be honest in both directions — entering the offline middle says so, and degrades to the existing device-tested navigation rather than failing. Extend `NavigationSession`'s existing state machine rather than adding a parallel one. | after D, K | ~3–4 days |
| **M** | **Scale to 3–5 per zone** | Repeat K for the remaining chosen landmarks. Content-bound, not code-bound. Noto TTFs land here (§05) before any translated copy is commissioned. | after L | content-paced |
| **N** | **Backend — only if needed** | `ContentDownloadManager` / `LocalContentCache` / CDN. The proposal is right to defer this. With 6–10 experiences, bundling in `StreamingAssets` is simpler, works on a weak connection, and removes an entire class of failure. Add a server when content volume actually demands it. | after M | defer |

**Phases A and G0 are today.** Both are cheap; one is insurance and the other can save weeks.

---

## 07 · Definition of done for the Geospatial layer

Adapted from the proposal's §23, corrected against what this codebase already has:

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
characters. Every one of those is reachable from this architecture later and none of them is on
the critical path to proving the idea works on the mountain.

---

## 08 · Open decisions

| Decision | Needed by | Notes |
|---|---|---|
| **Accept the INTERNET permission?** | Phase G, day one | Reverses a headline scope decision (§02 B1). Recommendation: accept, and restate the app's scope as "offline navigation, connected heritage at the ends" in About and Settings |
| **Which 3–5 landmarks per zone?** | Phase K | Of the 40, needs a human choice — the ones with real physical presence and a story worth 60 seconds, not just the geometrically convenient ones |
| **What is the heritage content, and who writes it?** | Phase K | The proposal assumes historical stories exist. They do not — `voiceText` is one-line navigation copy ("You are now Started from Alipiri Mettu"). Heritage narration is a writing project, and for a pilgrimage site it wants a source and a reviewer, not a generated draft |
| **Does the KML carry altitude?** (carried from Rev 3) | Phase F, and now also J | Terrain anchors reduce the need, but altitude would still improve the elevation profile and step count for free |

---

*Route data © OpenStreetMap contributors, ODbL — attribution required in the shipped app.*
*Rev 4 covers the Geospatial heritage layer only. `Docs/Draft1.md` (Rev 3) remains plan-of-record
for the navigation app; `PLAN.md` (Rev 2) remains the historical record and its route-geometry
figures are still accurate.*
