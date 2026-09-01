# Alipiri AR — Implementation Plan (Rev 6)

Supersedes the phase tables in `Online_Nav.md` §06 and `GeospatialPlan.md` §06. Written after a
full read of the project tree (81 runtime scripts, 11,283 LOC, 1 scene) and revised against the
real corridor figures supplied by the project owner:

```
3,550 stone steps
975 m elevation gain
9,000 – 12,000 m walking distance (endpoint-dependent)
```

Companion documents (published, private links):

- Corridor Review — https://claude.ai/code/artifact/2d54365f-ff47-4ad5-a5a6-8b72ce0efc2c
- Build Plan — https://claude.ai/code/artifact/b9299a43-ad79-4c08-a081-7201d6691415

---

## 00 · Bottom line

The code is better than its plan. The engineering is careful, the device-found bugs are recorded
at the line that produced them, and the single-scalar discipline (`RouteProgressTracker.
CumulativeDistanceMeters` as the one source every screen derives from) is the right architecture.
**The architecture is not what needs fixing.**

What needs fixing is that **the modelled route is 19–39% shorter than the real footpath**, and
every user-facing number is derived from that total.

On the online/offline proposal: the observation is right, the mode switch is wrong. Do not build
two navigation modes. Build **one always-on offline estimator** and let network-derived fixes
enter it as opportunistic corrections with a stated uncertainty. That removes the entire class of
transition bugs (boundary detection, hysteresis, mid-walk discontinuity, cold start) before they
are written, and it works identically whether or not Geospatial ever ships.

---

## 01 · What is wrong right now, quantified

Computed by replaying `RouteBuilder.Build` over `Assets/StreamingAssets/Route/alipiri_mettu.geojson`:

```
way/30434845        0 →  2,574 m     2,574 m    65 vtx
way/367434743   2,574 →  2,582 m         8 m     2 vtx
way/367434740   2,582 →  3,045 m       463 m    12 vtx
way/367434742   3,045 →  3,060 m        16 m     2 vtx
way/367434744   3,060 →  5,227 m     2,167 m    47 vtx
  ⚠  BRIDGE     5,227 →  6,399 m     1,172 m    straight line, NO surveyed geometry
way/30434846    6,399 →  7,288 m       889 m    33 vtx

built route      7,288.4 m   (157 waypoints)
surveyed only    6,116.4 m
bridged          1,172.0 m
mean spacing        39.6 m   (longest way)
way/365041854 rejected as an 84.6 m spur
```

| Quantity | App today | Reality | Consequence |
|---|---:|---:|---|
| Total route length | **7,288 m** | 9,000–12,000 m | Deficit **1,712–4,712 m** (19–39%) |
| Mean vertex spacing | **39.6 m** | ≤ 5 m needed | Every switchback corner-cut |
| Elevation gain | **absent** | 975 m | All 165 coords are 2-element `[lon, lat]` |
| Stair steps | **`3550 × fraction`** | 3,550 measured | Linear fiction, duplicated in 3 files |
| Progress at real 5 km | **69%** | 42–56% | Overstates by 13–27 points |
| ETA at real 5 km | **~48 min** | ~94 min | **Understates by ~2×** |

The ETA error is the one to take seriously. On a three-hour climb undertaken by elderly, fasting,
often barefoot pilgrims, an ETA optimistic by an hour is a safety defect, not a display bug.

**Why the bridge alone does not explain the deficit.** Replacing 1,172 m of straight line with real
switchback geometry typically doubles or triples that length (2,300–3,500 m), taking the total to
roughly 8,400–9,600 m. Re-surveying the existing ways at 5 m spacing recovers the rest. Both fixes
are the same field trip.

### Where the 975 m changes a recommendation

This is the best news in the revision. Barometric along-track uncertainty is altitude noise divided
by local gradient:

| Terrain | Gradient | σ along-track | Verdict |
|---|---:|---:|---|
| Steep stair flights | 25–40% | **2.5–4 m** | Best offline signal on the route |
| Route mean | 8–11% | **9–12 m** | Beats GPS under canopy |
| Flat approaches | < 3% | > 33 m | Gate it out; step counter carries |

975 m of monotonic ascent promotes the barometer from a supporting constraint to a **co-primary**
along-track signal. Where one of (barometer, step counter) is weak the other is strong — they are
close to complementary on this route, which is unusual and worth exploiting.

⚠ **Correction worth carrying forward:** 3,550 steps against 975 m gain implies steep risers, and
`TYPE_STEP_COUNTER` counts *walking* steps (~12,000–16,000 over the route), not stairs. A single
calibrated stride constant is therefore insufficient. Calibrate **two** — one for grade < 8%, one
for grade ≥ 8% — and rely on the per-segment stair tally from the survey for the mapping.

---

## 02 · Other findings from the full read

Ordered by consequence. Full detail in the Corridor Review.

| # | Finding | Severity |
|---|---|---|
| F-01 | `Assets/StreamingAssets/map_api_key.txt` holds a live `AIzaSy…` key and is **not** in `.gitignore`. Untracked today; the next `git add -A` commits it. | **Urgent** |
| F-02 | Route deficit (§01). | **Blocker** |
| F-03 | `StreamingAssets/Tiles/` **does not exist**. `TileBasemap` looks there first, always misses, falls through to Google's live Map Tiles API. The offline-first map has no offline tier — it is the app's *most* network-dependent surface. | **Blocker** |
| F-04 | The Geospatial bridge reaches only the chevrons. `RouteProgressTracker` and `LandmarkTriggerService` are fed raw GPS by `NavigationSession.HandleFix` and never see the pose. It must enter at `LocationProvider.EmitFix` instead — and must bypass `GpsKalmanFilter` (tuned for 5–25 m noise) and the 5 m jitter floor via the existing `trustExactly` path. | Design |
| F-05 | Step count is `3550 × fraction` in `ARNavigationScreen.cs:528`, `PoiMarkerLayer.cs:61`, `ProgressScreen.cs:25`. | Correctness |
| F-06 | No `internetReachability` check anywhere in 11,283 lines. `GoogleTileSession._attempted` is a once-per-process latch: one failure in a dead zone disables online tiles for the rest of the run, with no retry. | Missing |
| F-07 | No GPS trace in the repo. Every threshold in the positioning stack (25 m seed gate, 4× outlier factor, 6 rejections, 5 m jitter floor, 6%/2% nudge) is an untuned guess. `GpsTraceRecorder` was built for exactly this. | Process |
| F-08 | Latent: static `Texture2D` retention in `NavigationArrow`; unowned textures from `StreamingAssetsLoader.LoadTexture`; compass read raw with no pitch correction feeding `NudgeHeading`; two clocks in one pipeline (`Time.unscaledTimeAsDouble` vs GPS epoch); **no `OnApplicationPause` anywhere** — backgrounding invalidates ARCore's origin while `GeoAnchorFrame.IsEstablished` stays `true`; all 81 scripts in one assembly. | Latent |

Also absent, though named in the docs: ARCore Extensions is **not** in `Packages/manifest.json`
(`GeospatialSession` is an honest, well-documented stub); no Addressables; no ScriptableObjects;
no assembly definitions outside the two test folders.

---

## 03 · Architecture — four layers

Each layer is useful alone, and each higher one degrades to the layer below **without a mode
change**.

```
L0  Ground truth          surveyed geometry + elevation + per-segment step counts
                          (data, no code — everything above is only as good as this)

L1  Along-track estimator 1-D filter over route distance s
                          ALWAYS ON, fully offline, identical in every zone
                          ← this IS the offline navigation system

L2  Re-anchor sources     discrete high-confidence position+heading fixes
                          Augmented Images · manual landmark tap · Geospatial (if it ships)

L3  Local AR rendering    VIO + GeoAnchorFrame + GroundPlacementService + arrows
                          100 m horizon only — consumes s, never sources it
```

### The one interface

```csharp
IPositionSource → PositionMeasurement {
    double  S;              // along-track metres
    float   Sigma;          // 1-sigma, metres
    float?  HeadingDeg;     // null for GPS — it never gives trustworthy heading
    float?  HeadingSigma;
    double  Timestamp;
    SourceKind Provenance;  // fixes F-04: lets trustExactly apply correctly
}
```

### Separate the two accuracy problems

The current design conflates them through `GeoAnchorFrame`. They are different problems:

| Problem | Accuracy needed | Right solution |
|---|---:|---|
| **Global** — where am I on the route? Drives progress, ETA, next landmark, triggers, map. | ±10–20 m | L1 estimator |
| **Local** — where does this chevron sit on the ground in front of me? | ±0.2 m | VIO + plane raycast, already built and already correct |

The arrow trail only renders 100 m ahead, where VIO drift is 1–3 m — and `GroundPlacementService`
re-raycasts every frame anyway. Chasing 1 m *global* accuracy to place a chevron a local raycast
will reposition regardless is spending a network dependency on nothing.

### What each existing class becomes

| Today | Change |
|---|---|
| `LocationProvider` | One `IPositionSource` among several, not the funnel |
| `GpsKalmanFilter` | Stays, but moves *inside* the GPS source — must never touch a 1 m pose |
| `RouteProgressTracker` | **Public surface unchanged.** Fed by the fusion service. Every consumer compiles untouched |
| `HybridLocalizationEngine` | Narrows to L3 — local AR frame only |
| `GeospatialSession` | Becomes an `IPositionSource`. Stub stays honest until the package lands |
| `NavigationSession` | Owns the fusion service lifecycle |

---

## 04 · State machine — confidence, not connectivity

Connectivity is an **input** to the estimator, not a state of the app. What the app genuinely has
states about is how well it knows where you are.

```
BOOTING ──► AWAITING FIX ──► CONFIDENT ◄──────────────┐
                                 ▲ ▼  σ               │
                              COASTING                │ re-anchor
                                 ▲ ▼  σ               │
                             UNCERTAIN ──► LOST ──────┘
                                 
  any ──► PAUSED (user or OnApplicationPause)
  any ──► ARRIVED (haversine < 30 m to final waypoint)
```

| From → To | Fires when | Walker sees |
|---|---|---|
| Booting → Awaiting | DB loaded, AR state resolved, permissions settled | "Finding you", with the reason if a permission was denied |
| Awaiting → Confident | First measurement σ < 15 m. Seeds `s` and the AR frame together | Arrows appear, nav board live |
| Confident ⇄ Coasting | σ crosses 15 m. **Hysteresis: enter 15 m, leave 12 m.** Normal mid-route | Nothing changes — this is the design working |
| Coasting ⇄ Uncertain | σ crosses 40 m (enter) / 32 m (leave) | Distances become ranges; trail shortens to 30 m and fades |
| Uncertain → Lost | Lateral > 150 m sustained 60 s, or σ > 200 m | Arrows hide entirely; "Tap the landmark you can see". **A wrong arrow is worse than no arrow.** |
| Lost → Confident | Any re-anchor: image target, manual tap, good GPS fix | Frame re-seeds, σ collapses, arrows return |
| any → Paused | User pause or `OnApplicationPause(true)` | Session persists; **AR frame invalidated** |
| any → Arrived | Haversine < 30 m to final waypoint — *not* cumulative distance | Completion, summary, Landmarks tab |

One time-based edge: if no measurement of any kind has arrived for 120 s, σ has grown past the
coasting band by growth alone, and the app must say so rather than keep drawing arrows it no longer
believes.

Note what is **not** a state: online and offline.

---

## 05 · The estimator, concretely

Three states over route distance. Small enough to reason about; every term observable on this route.

```
state  x = [ s, k, b ]
         s  along-track position             (m)
         k  metres per step, current terrain  (m/step)
         b  barometric bias                   (m)

predict — on each step-counter tick, Δn steps
         s ← s + Δn · k
         σs² ← σs² + (Δn · k · 0.04)²        // 4% miscount on stairs

update — GPS fix
         z  = ClosestPoint(fix).cumulative
         σz = f(horizontalAccuracy)
         reject if lateral > 3σz             // off-corridor, not a real fix

update — barometer                            ← the 975 m payoff
         a  = altitudeFromPressure(p) − b
         z  = ElevationProfile⁻¹(a)
         σz = σ_alt / grade(s)                // gate: skip when grade < 0.05

update — image target / manual anchor
         z  = checkpoint.s ,  σz = 2 m
         also: reset step baseline to checkpoint.cumulativeSteps
               re-solve b from checkpoint.ele
               re-seed GeoAnchorFrame with checkpoint.bearingDeg

all updates
         K = σs² / (σs² + σz²)
         s ← s + K(z − s) ;  σs² ← (1 − K)σs²
```

**Why `σz = σ_alt / grade` is the important line.** It makes the barometer self-weighting. On a 30%
stair flight it contributes a 3 m measurement and dominates; on a 2% approach it contributes a 50 m
measurement and is correctly ignored. You get the benefit of 975 m of ascent exactly where the
ascent is, with no mode logic at all.

`k` is estimated online but clamped to ±20% of its calibrated value per surface type, so a burst of
miscounted steps cannot run the stride constant away.

### Expected drift — design targets, to be falsified on the Phase 1 walk

Along-track error, no network, from a confident fix:

| Distance | VIO alone | Steps uncal. | Steps calibrated | Fused + re-anchors |
|---|---:|---:|---:|---:|
| 100 m | 1–3 m | 8 m | 3 m | **2 m** |
| 500 m | 10–40 m | 40 m | 15 m | **8 m** |
| 1,000 m | 20–90 m | 80 m | 30 m | **12 m** |
| 3,000 m | unusable | 240 m | 90 m | **18 m** |
| Full (~9,500 m) | unusable | 760 m | 285 m | **< 25 m** |

The last column is **bounded** rather than accumulating. That is the entire argument for
checkpoints: with a re-anchor roughly every 500 m, error never compounds past one segment.

### Sensor roles — honest assessment

| Signal | Role | Note |
|---|---|---|
| Step counter (`TYPE_STEP_COUNTER`) | **Primary along-track** | Hardware-backed, near-zero battery, works in a pocket. Needs `ACTIVITY_RECOGNITION` on API 29+; min SDK here is 26 |
| Barometer (`TYPE_PRESSURE`) | **Co-primary** | See §01. Blocked until the route has elevation |
| Corridor constraint | **Free accuracy** | Already implemented in `PolylineUtility.ClosestPoint`. Highest-leverage thing already in the codebase |
| ARCore VIO | **Local only** | 1–3% outdoors, 5–10% degraded. Over 9.5 km that is 95–950 m — unusable globally. Use for L3, never L1 |
| Magnetometer | Fallback heading | Weakest signal. Uncorrected for pitch today. Once AR tracking is up, **use camera yaw instead** |
| Raw accel/gyro | **Do not integrate** | Double-integrating consumer MEMS diverges in seconds. The step counter is the correct abstraction over the same hardware |

---

## 06 · Phases

Critical path: Phase 1. Phases 0 and 2 need nothing and start today.

```
wk 0        3        6        9       13
P0 ▮
P1 ▬▬▬▬▬▬▬▬                              ← critical path, blocks P3–P5
P2 ▬▬▬                                    (parallel with P1)
P3          ▬▬▬▬▬▬▬▬
P4                   ▬▬▬
P5                      ▬▬▬▬▬
P6                          ▭▭▭▭▭        (optional, gated on desk check)
P7                              ▬▬▬▬▬▬▬▬
```

### Phase 0 — Stop the bleeding
**Half a day · trivial · no dependencies · do today**

1. Add `Assets/StreamingAssets/map_api_key.txt` (+ its `.meta`) to `.gitignore`. Ship a
   `map_api_key.example.txt` instead.
2. Cloud Console: restrict the key to Android package `com.alipiriar.navigation` + release signing
   SHA-1; restrict to **Map Tiles API only**; set a daily quota cap. `GoogleTileSession`'s class
   comment states this requirement correctly — verify it was actually applied.
3. `git log --all --stat | grep -i proton` — confirm the 125 MB installer is not in history.
   GitHub rejects files over 100 MB outright.
4. Add `OnApplicationPause`/`OnApplicationFocus` to `ARSessionBootstrapper`. On pause, call a new
   `GeoAnchorFrame.Invalidate()` clearing `IsEstablished`. On resume, let
   `HybridLocalizationEngine` re-seed normally.
5. Fix `GoogleTileSession._attempted`: replace the once-per-process latch with a timestamped retry
   (backoff 30 s → 5 min, reset on reachability change).

**Acceptance** — `git add -A` does not stage the key · backgrounding mid-navigation and resuming
does not place arrows behind the walker · toggling airplane mode off recovers online tiles without
an app restart.

---

### Phase 1 — Ground-truth survey
**2–3 weeks · high · CRITICAL PATH · needs site access**

*Objective: replace a route 19–39% short with measured geometry, elevation and step counts.*

**Field capture — one walk, five channels**

1. **GNSS track at 1 Hz**, raw and unsmoothed, exported as GPX with per-fix accuracy. Full path,
   Alipiri arch → Mahadwaram.
2. **Barometric altitude** on the same device, same clock. Even uncalibrated, the *shape* is what
   you need.
3. **Manual stair tallies at every landmark.** One person, one tally counter, counting stair steps
   between consecutive landmarks. The least glamorous and highest-value task in the plan — it is
   what converts the step counter from an estimate into a measurement.
4. **Photograph every candidate image target** — signage, inscriptions, plaques, painted step
   numbers — square-on, daylight, at 2 m and 5 m. Record lat/lon and route bearing for each.
5. **Signal log** — cell technology and bar count every 100 m. This is what sizes the zones by
   measurement instead of assumption.

**Desk work**

6. Clean the GPX (drop fixes > 20 m accuracy), then resample to uniform **5 m** along-track
   spacing. Do not decimate — corner-cutting at 39.6 m is a direct cause of the deficit.
7. Attach elevation: prefer the barometric profile tied to two known absolute altitudes; fall back
   to a DEM (SRTM 30 m / Copernicus) sampled along the polyline. Savitzky–Golay smooth before
   differentiating for grade.
8. Build the `steps → metres` profile by distributing each landmark-to-landmark tally across that
   segment, weighted by grade.
9. Author `checkpoints.json` (§07). Target 18–20 checkpoints.
10. Extend `KmlImporter` for elevation + step columns, or add a GPX path beside it.
    `Editor/ImportKmlTool.cs` already provides the menu scaffolding.
11. Extend `Editor/ValidateData.cs` to **fail the build** on: any bridged metres · mean vertex
    spacing > 6 m · any `NaN` elevation · non-monotonic cumulative distance · total outside
    8,500–12,500 m.

**Risks** — Site access and TTD photography permission are lead times, not tasks; start the
conversation now. GNSS may be poor in exactly the bridged stretch: walk it twice on different days
and merge, trace from satellite imagery where it fails, and validate length against the barometric
profile (unaffected by canopy). Schedule risk is the real one — this phase cannot be compressed by
working harder, which is why it starts in week 0.

**Acceptance** — zero bridged metres · total in 8,500–12,500 m · mean spacing ≤ 5 m, no gap > 10 m ·
elevation on every waypoint, total ascent within 10% of 975 m · per-segment tallies sum within 2%
of 3,550 · all 41 landmarks re-project with lateral offset < 15 m.

---

### Phase 2 — Genuine offline data
**1 week · medium · parallel with P1**

*Objective: make the offline claim true for the first time.*

1. Define the AOI as a **300 m buffer around the route polyline** — not a bounding box, which
   triples the tile count for empty hillside.
2. Bake z14–z18 into `Assets/StreamingAssets/Tiles/{z}/{x}/{y}`. Roughly 1,300–1,600 tiles.
   Respect the provider's terms and cache policy; keep MapScreen's existing ODbL attribution.
3. Convert to **ASTC 6×6** or KTX2, not PNG. Main-thread PNG decode while walking is a visible
   hitch, and compression roughly quarters the size.
4. New `ConnectivityService` (`Scripts/Core/`): `internetReachability` as a cheap gate, then a real
   HEAD probe with a 3 s timeout before committing to any request. Expose `IsOnline` with
   hysteresis — 2 consecutive successes to go online, 3 failures to go offline.
5. Route both existing network calls through it (`GoogleTileSession.EnsureSession`,
   `TileBasemap.FetchOnlineTile`).

**Risk** — APK size. Mitigate with compression and the corridor buffer; if still uncomfortable,
drop z18 outside the two end zones.

**Acceptance** — airplane mode, cold start, pan the full route at every zoom: real imagery
throughout, `DemoBasemap` never visible · zero network requests logged offline · APK growth
< 250 MB.

---

### Phase 3 — The along-track estimator
**2–3 weeks · high · depends: P1**

*Objective: replace "GPS drives everything" with one always-on 1-D filter.*

**New types** (`Scripts/Positioning/Sources/`)

1. `IPositionSource` — §03's contract.
2. `GpsPositionSource` — wraps today's `LocationProvider` + `GpsKalmanFilter`, projects through
   `PolylineUtility.ClosestPoint`, sets `Sigma` from `horizontalAccuracy`, rejects lateral > 3σ.
3. `StepCounterSource` — JNI against `TYPE_STEP_COUNTER`. Follow the pattern in
   `Audio/AndroidTextToSpeech.cs`. Add `ACTIVITY_RECOGNITION` to the manifest and request at
   runtime on API 29+.
4. `BarometerSource` — `TYPE_PRESSURE` → altitude → invert the elevation profile. **Gate on grade
   > 5%**, set `Sigma = σ_alt / grade`.
5. `AlongTrackEstimator` — §05.
6. `PositionFusionService` — owns the sources and the estimator, drives `RouteProgressTracker.Feed`.

**Changes to existing code**

7. `RouteProgressTracker` — **public surface unchanged**; `Feed` takes a fused `s` and σ, jitter
   band becomes σ-driven. Every downstream consumer compiles untouched.
8. `NavigationSession.Resolve` — constructs the fusion service; keep the Editor trace-replay branch.
9. `HybridLocalizationEngine` — narrows to the local AR frame.
10. Delete `3550 × fraction` in all three files; read measured steps from the estimator. Drop the
    `~` prefix on the live figure — it is a measurement now.
11. Put the whole pipeline on **one clock**. Use GPS fix timestamps throughout.

**Stride calibration**

12. Over the first 500 m (good GPS, confirmed by the signal log), least-squares solve for
    metres-per-step against GPS-derived distance. Store per profile in `PlayerPrefs`.
13. Calibrate **two** constants — grade < 8% and grade ≥ 8%. With 975 m of ascent both are used
    heavily and they are genuinely different quantities.

**Risks** — highest-complexity phase. Mitigate by keeping `RouteProgressTracker`'s public surface
byte-identical and putting the fusion path behind a runtime flag, so the GPS-only path stays
available for on-device A/B. Budget devices may lack a barometer: every source registers only if
its sensor is present, and σ reflects what is actually running.

**Acceptance** — replaying the P1 trace with GPS disabled after 1,000 m, final along-track error
< 40 m over the full route · with all sources, σ < 20 m for > 90% of the walk · live step badge
within 3% of the manual tally at each landmark · reported total within 5% of surveyed length · ETA
error < 15% past halfway.

---

### Phase 4 — Confidence states & recovery
**1 week · medium · depends: P3**

1. `NavigationStateMachine` — §04's seven states, σ-driven, hysteresis on every band.
2. UI honesty pass: **Uncertain** renders distances as ranges ("2.1–2.4 km"), trail shortens to
   30 m and fades; **Lost** hides arrows entirely and shows a landmark picker.
3. `SessionJournal` — write `{s, stepBaseline, baroBias, state, utc}` to
   `Application.persistentDataPath` every 30 s and on pause. On launch offer "Resume from ~4.2 km?"
   rather than silently trusting it.
4. Low-power mode below 15% battery: AR off, step counter + occasional GPS, screen dimmed, audio
   guidance continues. On a three-hour mountain climb this is a safety feature.
5. Trigger **Arrived** on haversine distance to the final waypoint, never cumulative distance.

**Failure recovery matrix**

| Failure | Response |
|---|---|
| Tracking lost (`Limited`/`None`) | Hide chevrons immediately; nav board stays live from L1 |
| ARCore session reset | `GeoAnchorFrame` **must** invalidate (it currently does not); re-establish from L1's `s` + camera yaw |
| GPS lost | Expected, not exceptional. One source stops; σ grows. No user-visible event |
| Compass error | Detect via > 30° disagreement with camera yaw sustained 10 s; prefer camera yaw, mark magnetometer untrusted |
| Phone in pocket | Proximity sensor or black camera frames → suspend AR rendering, **keep the step counter running** |
| Backgrounded | Persist state, invalidate AR frame. Android's step counter survives backgrounding — recover elapsed distance from the delta |
| App restart / crash | Restore from journal, with confirmation |
| Sensor absent | Sources register conditionally; σ reflects reality. Never assume a sensor exists |
| Battery critical | Low-power mode |

**Acceptance** — each state reachable on device by a scripted action, no state flaps more than once
per 60 s while walking · force-kill mid-route and relaunch restores within 20 m · cover the camera
for 2 minutes: arrows hide, nav board stays correct.

---

### Phase 5 — Re-anchoring
**1–2 weeks · medium · depends: P1, P3**

*Objective: bound the drift instead of merely slowing it — using `ARTrackedImageManager`, which
ships inside AR Foundation 6.5 and is **already installed**. No package, key, Cloud project,
billing or INTERNET dependency.*

1. Build an `XRReferenceImageLibrary` from the P1 photographs. Unity's editor scores trackability
   — reject anything below "Good" rather than shipping a target that will not track.
2. `ImageAnchorSource : IPositionSource` — on `trackablesChanged`, look up the target's checkpoint,
   emit `S` with σ ≈ 2 m **and heading** with σ ≈ 3°. The only source giving trustworthy heading.
3. On an image hit: re-seed `GeoAnchorFrame`, reset the step baseline to `cumulativeSteps`,
   re-estimate barometric bias from the known elevation. **One hit corrects all three drift
   channels.**
4. `ManualAnchorSource` — the Lost-state landmark picker, σ ≈ 15 m, works with the camera pocketed.
5. Retire the raw magnetometer as heading source once AR tracking is up.

**Real limits, not glossed** — needs line of sight; works at roughly 2–8 m; each target needs
high-contrast non-repeating texture (a flat signboard with large plain areas tracks poorly); test
each candidate at 2, 5 and 8 m in morning and evening light before committing; anything newly
installed on the pathway needs TTD permission.

**Acceptance** — ≥ 8 targets track reliably at 2–6 m in both lighting conditions · starting from a
deliberately corrupted position (+200 m), the first checkpoint recovers to < 5 m · full-route error
< 25 m with GPS disabled throughout.

---

### Phase 6 — Geospatial (gated, optional)
**1–2 weeks · medium · may never run**

**The gate — 30 minutes, do it this week.** Open Street View at the Alipiri arch and at the
Mahadwaram. **No Street View coverage means no VPS coverage.** If absent, skip this phase
permanently, reinstate `tools:node="remove"` on INTERNET in `AndroidManifest.xml`, and restore the
fully-offline scope claim.

If the gate passes:

1. Install ARCore Extensions from Google's GitHub release (a `.tgz`, not a registry package).
   **Verify AR Foundation 6.5 compatibility before writing a line of API code.**
2. Configure the Cloud project; enable the ARCore API; restrict credentials.
3. Implement `GeospatialSession` against the *actually installed* package source in
   `Library/PackageCache/`, not from memory. The existing stub's comment on this is correct —
   method names have moved between Extensions releases. Keep that discipline.
4. Register it as an `IPositionSource`. It is a source, not a second engine.
5. `ARAnchorService` — resolve Terrain anchors for the 8 landmarks with `GeospatialEnabled`. This
   is the one capability nothing offline replaces: `GeoAnchorFrame.GeoToWorld` has no altitude term,
   so a statue on a slope needs it.
6. Budget quota **by event** — zone entry, tracking loss, heritage landmark trigger. ~20 lookups
   per pilgrimage, not a polling loop.
7. Update About/Settings copy to match actual scope: "offline navigation, connected heritage
   content at the two ends."

**Acceptance** — with the network forcibly disabled, navigation behaviour is **bit-identical** to
Phase 5. That is the test proving the demotion worked · heritage anchors resolve within 10 s at
both ends and sit at correct height on sloped ground · ≤ 25 lookups per full walk.

---

### Phase 7 — Hardening for real pilgrims
**2 weeks · medium · depends: P4**

**Structure**

1. Assembly definitions per folder, one-way dependencies: `UI → Positioning → Route → Data`, with
   `AR → Positioning` and never the reverse. Enforces §03's layering at compile time.
2. Split `ARNavigationScreen` (900 lines) — extract `ArNavigationController` for AR lifecycle and
   overlay orchestration; leave the screen as a view.
3. Move the ~40 tuning constants into a `NavigationTuning` ScriptableObject so the post-walk tuning
   pass needs no rebuild.

**Battery — the binding constraint.** A pilgrim walks this in 2–4 hours; AR camera + VIO + screen
is roughly 15–25%/hour on a mid-range phone.

4. **AR on demand.** Default to the 2-D map; raise AR on a lift gesture or tap. Roughly halves
   consumption, and most walkers on steep steps are watching their feet anyway.
5. `Application.targetFrameRate = 30` during navigation.
6. Throttle `DynamicArrowManager.Refresh` from every frame to ~5 Hz — it currently issues ~40
   raycasts per frame from `Update`.
7. Disable `ARPlaneManager` once a stable ground plane is found; re-enable on tracking loss.
8. Stop GPS polling while Coasting is stable.

**Correctness & care**

9. Fix the static `Texture2D` retention in `NavigationArrow` and unowned textures from
   `StreamingAssetsLoader.LoadTexture`. Give the tile cache an explicit disposal path.
10. Give `PolylineUtility.ClosestPoint` a segment-index hint and ±20-segment window — mandatory
    once the polyline has ~2,000 vertices, and it removes a teleport-on-noise bug class.
11. Compute densified waypoints once in `JsonDatabase`, not per `DynamicArrowManager` construction.
12. Telugu and Tamil localization — the framework exists, only `en.json` is populated.
13. Safety surface: nearest water and medical post always visible; pace-decay rest prompts; an
    emergency card showing checkpoint, step count and lat/lon in large type for reading aloud,
    working with zero signal; SMS fallback for distress (SMS often gets through where data does not).
14. Crash reporting and opt-in anonymous trace upload — the latter feeds crowd-sourced route
    refinement later.

**Acceptance** — full-route walk completes on a mid-range device with > 25% battery remaining ·
sustained 30 fps, no GC spike > 5 ms during navigation · clean walks on three device tiers, at dawn
and after dark.

---

## 07 · Data schemas

Version these **now**, before shipping installs that need migrating.

### route.json — replaces the GeoJSON

```jsonc
{
  "schemaVersion": 2,
  "routeId": "alipiri_mettu",
  "surveyedUtc": "2026-09-20T05:30:00Z",
  "totalDistanceMeters": 9420.0,   // measured, not chained
  "totalAscentMeters": 975.0,
  "totalStairSteps": 3550,
  "waypoints": [
    { "lat": 13.646192, "lon": 79.405825,
      "ele":   168.4,        // m ASL — never NaN
      "s":       0.0,        // cumulative metres
      "steps":     0,        // cumulative stair steps
      "grade":  0.04,        // local gradient, drives baro weighting
      "surface": "paved" }   // paved | steps | ramp
  ]
}
```

### checkpoints.json — new

```jsonc
{
  "schemaVersion": 1,
  "checkpoints": [
    { "id": "CP07",
      "nameKey": "cp.mokala_parvatham",
      "s": 4180.0,               // the along-track value this asserts
      "lat": 13.6612, "lon": 79.3771,
      "ele": 612.0,              // re-estimates barometric bias
      "cumulativeSteps": 1980,   // resets the step baseline
      "bearingDeg": 312.5,       // lets it re-seed heading too
      "imageTargetRef": "img_cp07_plaque",
      "confirmPromptKey": "cp.confirm.mokala",
      "spacing": "dense" }       // dense at ends, sparse mid-route
  ]
}
```

**Checkpoint spacing** — ~500 m through the middle, ~200 m at the two ends where landmark density
is already high, and one mandatory checkpoint immediately after the former bridge section, where
accumulated error is worst. Roughly 18–20 total.

`landmarks.json` stays as it is; add a `schemaVersion`. The Rev 4 fields (`zone`,
`geospatialEnabled`, `anchorType`, `experienceId`) already parse to safe defaults, so nothing breaks.

---

## 08 · Testing

The Phase 1 walk is not only a survey — it is the test fixture, and it is worth treating as one.

1. **Extend `TraceReplaySource` into a multi-channel replayer.** It currently replays waypoints;
   make it replay the recorded CSV with GPS, barometer and step-counter channels on the original
   timeline. Every estimator change then validates at a desk against a real walk, in seconds.
   **This is the single highest-leverage piece of test infrastructure in the project.**
2. **Ablation suite.** Replay the same trace with sources disabled — GPS off after 1 km, no
   barometer, no step counter, no anchors — asserting an error bound for each combination. This is
   how you discover which sensor actually carries the route.
3. **Fill the empty test assemblies.** `Assets/Tests` holds one placeholder. Unit-test
   `PolylineUtility`, `RouteBuilder`, `AlongTrackEstimator` and the elevation inversion — all four
   are pure functions with no Unity dependency.
4. **Golden-data validation in CI.** Extend `Editor/ValidateData.cs`; run on every build. A route
   regression should fail the build, not surface on the mountain.
5. **Device matrix.** Low, mid, high tier; dawn and after dark; one walk in crowd conditions, which
   is what VIO handles worst.

---

## 09 · Risk register

| Risk | Impact | Mitigation |
|---|---|---|
| Site access / TTD permission slow | Blocks P1, therefore P3–P5 | Open the conversation week 0. Trace from satellite imagery as interim so P3 starts against provisional geometry |
| GNSS unusable in the bridged stretch | P1 partially fails where it matters most | Two walks on different days; satellite tracing; validate length against the barometric profile |
| No usable image targets exist | P5 weakens to manual anchors only | Manual tap still bounds drift and needs nothing installed. Keep BLE beacons as a Phase 8 option if TTD is supportive |
| Target devices lack a barometer | Loses the strongest offline signal on that device | Conditional source registration; σ reflects reality. Survey which phones pilgrims actually carry |
| ARCore Extensions incompatible with AR Foundation 6.5 | P6 only | Already isolated; P6 is optional by design |
| Real length lands near 12 km | Battery and ETA assumptions shift | P7's AR-on-demand becomes mandatory rather than optional. Decide after P1 measures it |

---

## 10 · Decisions needed

| Decision | Needed by | Recommendation |
|---|---|---|
| **Which endpoints define the route?** The 9–12 km spread is mostly disagreement about where it starts and ends | P1 planning | Alipiri arch → Mahadwaram, stated in the app. Ambiguity here is why the published figure has a 3 km range |
| **Is AR the primary interface, or audio?** | P7 | **Audio primary, AR on demand.** Walkers on steep steps watch their feet; the TTS layer already exists; battery roughly halves |
| **Pursue Geospatial at all?** | After the 30-min desk check | Only if Street View covers both ends, and only for heritage anchoring. Run the check this week — it is free and removes an open question |

---

## 11 · Technologies considered and declined

- **UWB** — needs installed infrastructure over 9+ km; phone support is scarce. No.
- **BLE beacons** — technically excellent (weatherproof, ~₹500 each, ~30 units, years of battery).
  The blocker is TTD permission and maintenance, not engineering. Keep as a Phase 8 option.
- **Self-hosted photogrammetric VPS** — months of research-grade work, and outdoor feature maps
  decay with season, vegetation, lighting and crowd density, which is exactly why Google re-captures
  Street View. Nine kilometres is a great deal of map to maintain. No.
- **Third-party VPS (Immersal, Lightship)** — only Immersal is materially different, supporting a
  downloadable map for offline localization. Still a vendor dependency and probably a paid tier.
  Verify current terms directly rather than from any summary.

---

## 12 · The one-line version

Do Phase 0 today. Start the TTD conversation and survey planning this week. Bake the tiles while you
wait. Then build one estimator that fuses steps and altitude against a route you have actually
measured — and the online/offline question largely dissolves, which is the strongest sign it was
the wrong axis to build the architecture on.

---

*Route deltas computed by replaying `RouteBuilder.Build` over `alipiri_mettu.geojson`: 7,288.4 m
built · 6,116.4 m surveyed · 1,172.0 m bridged · 39.6 m mean vertex spacing. Real-world figures
(3,550 steps · 975 m ascent · 9–12 km) supplied by the project owner. Drift and σ figures are
design targets — falsify them on the Phase 1 walk.*
