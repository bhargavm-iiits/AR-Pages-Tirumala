# Alipiri AR Navigation — Repository Audit (2026-09-02)

**Scope:** full working-tree analysis of `U:\BHARGAV\AR_pages\AR_pages` — code, docs, packages, and
git/repo hygiene. This is a snapshot report, not a plan; it does not supersede `Docs/update1.md`
(the architecture/positioning plan currently being implemented) or the `PLAN.md` → `Draft1.md` →
`GeospatialPlan.md` → `Online_Nav.md` lineage below. Saved as `update2.md` deliberately, to avoid
overwriting `update1.md`, which dozens of in-code comments cite by name.

---

## 1. What this project is

Unity 6000.5.5f1, URP 17.5, AR Foundation 6.5 / ARCore 6.5, targeting Android. A GPS-and-AR guided
walking-navigation app for the Alipiri → Tirumala pilgrim route (~9,000–12,000 m, ~3,550 steps,
~975 m elevation gain). Offline-first by design, with an optional online map-tile fallback and a
gated Geospatial heritage layer at the two route endpoints. Not related to `AR_Anchor`
(`U:\AR_Tirumala\...`), a separate Cloud-Anchors project sometimes confused with this one.

## 2. Directory map

```
Assets/
  Scripts/          — all app code (see §3)
  Editor/            — 4 editor-only tools (BuildScript, ImportKmlTool, XRSetup, TMP importer)
  Scenes/, Resources/, Settings/, Plugins/Android/, StreamingAssets/, TextMesh Pro/, XR/
  Tests/             — 1 real test asmdef + script
  Tests 1/           — stray duplicate asmdef, no script (see §5.2)
  Videos/            — 1 video asset (see §5.3 — untracked .meta files)
Docs/                — planning docs + reference assets (kml/kmz, route PDF, landmark art)
Packages/, ProjectSettings/, Tools/
PLAN.md, alipiri.md, alipiri_one.md   — root-level planning docs (see §4)
```

`Builds/`, `Library/`, `Temp/`, `Logs/`, `UserSettings/` are present on disk but correctly
gitignored — not a hygiene problem, just normal Unity working-tree bulk (`.git` is 66 MB,
`Assets` 69 MB).

## 3. Code architecture (`Assets/Scripts/`, 91 files, ~12,500 lines)

| Module | Role |
|---|---|
| `Core/` | `ServiceLocator` (simple singleton registry, 14 call sites), `AppBootstrap` (init order), `ConnectivityService` (online/offline hysteresis) |
| `Data/`, `Database/` | Plain data models + `JsonDatabase` (loads route/landmarks from StreamingAssets) |
| `Positioning/` | The navigation core: `NavigationSession` (lifecycle), `PositionFusionService` + `AlongTrackEstimator` (1-D Kalman fusion of GPS/steps/barometer), `RouteProgressTracker`, `NavigationConfidenceMachine`, `SessionJournal`, plus `Sources/` (`GpsPositionSource`, `StepCounterSource`, `BarometerSource`, `IPositionSource`) |
| `AR/` | `ARSessionBootstrapper`, `HybridLocalizationEngine`, `GroundPlacementService`, `DynamicArrowManager`, `Geospatial/GeospatialSession` (documented no-op, gated) |
| `Map/` | `MapView`, `TileBasemap` (offline tiles) + `GoogleTileSession` (online fallback), `DemoBasemap`, `PoiMarkerLayer`, `RouteOverlay`, `UserMarker` |
| `UI/` | `Screens/` (Login, Map, Landmarks, Progress, Settings, ARNavigation — see §5.1), `Overlays/`, `Framework/` (hand-rolled UI toolkit: `UIFactory`, `UITween`, `UITheme`, `UIShapes`) |
| `Route/` | `KmlImporter`, `GeoJsonParser`, `RouteBuilder` |
| `Audio/`, `Localization/`, `Profile/`, `Diagnostics/`, `Utilities/` | TTS/voice guidance, i18n, login/profile + xlsx export, debug overlay + GPS trace recorder, geo/distance/ETA math |

No asmdefs exist for any of this — all 91 files compile into the implicit `Assembly-CSharp`. Only
`Assets/Tests/Tests.asmdef` is a real assembly boundary. Practical effect: every script change
recompiles the entire codebase, and there's no compiler-enforced separation between e.g. `Utilities`
and `UI`.

## 4. Documentation landscape

Two independent doc threads exist and are easy to conflate:

**A. App build plan (revision chain, each explicitly supersedes/extends the last):**
`PLAN.md` (root, Rev 2) → `Docs/Draft1.md` (Rev 3) → `Docs/GeospatialPlan.md` (Rev 4, heritage layer)
→ `Docs/Online_Nav.md` (Rev 5, online/offline nav zones). `Docs/UIplan.md` is a companion to Rev 3
covering only the UI redesign. `Docs/NewPlan.md` also self-identifies as "Rev 3, supersedes PLAN.md"
and is 132 lines vs. `Draft1.md`'s 143 — the two are **not identical** (`diff` confirms real
differences) but claim the same revision number against the same base. This is ambiguous: it's
unclear which of `Draft1.md` / `NewPlan.md` is authoritative, or whether one is a stale fork of the
other. Worth resolving directly with whoever last edited them, since nothing in the files themselves
states which one lost.

**B. Positioning/architecture review:** `Docs/update1.md` — the corridor-fidelity, along-track
estimator, and confidence-state plan currently being implemented (Phases 0–4 done this session;
Phase 1 fieldwork, Phase 6, and Phase 5 UI wiring outstanding). This report is filed alongside it as
`update2.md`, not folded into the A-chain above.

Root-level `alipiri.md` (35 lines) and `alipiri_one.md` (280 lines) weren't cross-referenced by
anything read during this audit — worth a pass to confirm they're still needed or fold their content
into the Docs/ chain.

`Docs/` also holds non-planning assets that are fine where they are: `Alipir.kml`/`Alipiri.kmz`
(route source data consumed by `KmlImporter`/`Tools ▸ Import KML Route`), `Map.pdf`, landmark art
(`Dashavatar/`, `Images/`). `Docs/ProtonVPN_v5.1.7_x64.exe` (125 MB) sits in the working tree but is
correctly `.gitignore`'d (`*.exe`, with a comment explaining why) — not a repo problem, just local
clutter worth deleting if it's no longer needed.

## 5. Findings

### 5.1 `ARNavigationScreen.cs` is a 839-line god-class
Single-handedly the largest file in the project (next largest is 587 lines). Owns AR session
bootstrap, ground placement, hybrid localization, geospatial, arrows, the nav-session lifecycle
callbacks, and every UI element on the AR screen (18+ private fields for labels/images/cards) in
one class. This was already flagged as a deferred Phase 7 item in `update1.md`; this audit confirms
it's now the single biggest concentration of risk for future blind edits — any change here needs a
full read first, as has been the practice this session.

### 5.2 `Assets/Tests 1/` — stray empty duplicate
Contains only `Tests 1.asmdef` (+ .meta), no script, no obvious purpose distinct from
`Assets/Tests/Tests.asmdef`. Reads as an accidental duplicate (e.g. from a copy-paste or a rename
that didn't fully complete). Low risk, but it's dead clutter — worth deleting unless there's a
reason to keep a second empty test assembly.

### 5.3 `Assets/Videos/1.mp4.meta` and `Assets/Videos.meta` are untracked
`git status` shows these two `.meta` files as untracked while `Assets/Videos/1.mp4` itself **is**
tracked. Unity `.meta` files carry the asset's GUID; if these are never committed, a fresh clone (or
CI, or a teammate) gets fresh auto-generated GUIDs for this asset and folder, which can silently
break any existing reference to the video (import as a new asset, orphaning whatever component
pointed at the old GUID). This is a real, cheap-to-fix bug: `git add` both `.meta` files and commit.

### 5.4 No compile-time module boundaries
Flagged in §3 — everything is one `Assembly-CSharp`. Not urgent, but matches `update1.md`'s own
Phase 7 backlog item on asmdef splitting; this audit found no code-level blocker to doing it
incrementally (e.g. `Utilities/` and `Positioning/` have the fewest inbound UI dependencies and
would be the cheapest first split).

### 5.5 Package manifest includes several packages with no confirmed use
`Packages/manifest.json` lists `com.unity.ai.assistant`, `com.unity.ai.inference`,
`com.unity.ai.navigation`, `com.unity.visualscripting`, `com.unity.timeline`,
`com.unity.multiplayer.center` alongside the packages this app clearly uses (AR Foundation, ARCore,
XR management, URP, Input System, Newtonsoft Json, TestFramework). None of the six were referenced
by anything under `Assets/Scripts` in this audit's greps. They may be Unity 6 project-template
defaults rather than deliberate additions. Each one adds editor load time and (for some) build
size; worth a pass to remove whichever aren't actually load-bearing, but verify via Package Manager
"in use" checks or a clean build before removing anything, since absence from `Assets/Scripts` grep
doesn't rule out use from a Scene/Prefab/ScriptableObject.

### 5.6 Positives worth noting
- **Zero `TODO`/`FIXME`/`HACK` markers** anywhere in `Assets/Scripts` — either genuinely finished
  work or a project convention of writing done work instead of leaving markers; either way, unusual
  and good.
- `AndroidManifest.xml`'s permission block carries real device-verified reasoning for every entry
  (including a documented, dated, deliberate reversal on stripping `INTERNET`) — this is a much
  higher documentation bar than most Unity Android projects hold, and should be the template for
  comments elsewhere in the project.
- Git working tree is otherwise clean: only the two `Videos` `.meta` files are untracked; nothing
  build-artifact-shaped has leaked into tracked history.

## 6. Suggested next actions (not applied — audit only)

1. Fix §5.3 (`git add` the two `Videos` `.meta` files) — cheapest, highest-value fix here.
2. Resolve the `Draft1.md` vs `NewPlan.md` ambiguity (§4A) with whoever owns those docs.
3. Delete `Assets/Tests 1/` if it's confirmed to be an accidental duplicate.
4. Decide on `alipiri.md` / `alipiri_one.md` — fold into `Docs/` or remove.
5. Audit the six packages in §5.5 against actual Scene/Prefab usage before removing.
6. When Phase 7 work resumes: `ARNavigationScreen.cs` split and asmdef boundaries are the two
   highest-leverage structural changes, both already anticipated by `update1.md`.
