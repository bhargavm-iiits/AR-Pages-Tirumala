# Alipiri AR — Online/Offline Navigation Zones (Rev 5)

**Target:** Unity 6000.5.5f1 · URP 17.5 · AR Foundation 6.5 · ARCore 6.5 · Android
**Rev 5 — extends `Docs/GeospatialPlan.md` (Rev 4).** Rev 4 covers the Geospatial *heritage* layer
and remains plan-of-record for it. This document covers using Geospatial for **navigation itself**
at the two ends of the route, and the online → offline → online handoff that implies. It is
Rev 4's Phase L, promoted from a content concern to a positioning one.

Everything below was verified by reading this repository — `alipiri_mettu.geojson`,
`landmarks.json`, `Packages/manifest.json`, `AndroidManifest.xml`, and the positioning/AR source —
and by recomputing the route geometry with `RouteBuilder`'s own chaining algorithm. Distances are
measured, not carried forward from Rev 2/3/4.

**Verdict on the proposal: adopt it. It is a better use of Geospatial than Rev 4's heritage-only
framing, and the codebase is already most of the way to receiving it.** Four corrections follow,
one of which changes a number in the proposal.

**§12 answers a separate question raised afterwards** — whether the Geospatial API key can be avoided
entirely by building our own map. It can, largely, via ARCore Augmented Images, at the cost of terrain
anchors. §§02–11 assume the Geospatial path; §12 is the fork, and §12.6 sequences the decision so it
does not have to be made before the Phase G0 desk check.

**This document is not neutral between the two paths. §13 records the recommendation and its
reasoning** — briefly, that the cheap offline work should happen first, and that the fork is likely
to resolve away from the Geospatial API. Read §13 before treating §§06–09 as the plan.

---

## 01 · What already exists

The FeedFix seam Rev 4 §04 identified as "the highest-leverage integration in the whole plan" is
**already wired**. This proposal does not need to build it.

| Piece | Status | Evidence |
|---|---|---|
| INTERNET permission | ✅ live, reversal documented inline | `AndroidManifest.xml:47`, comment at `:28-42` |
| `RouteZone` enum (Start/Route/End) | ✅ exists | `Data/RouteZone.cs` |
| `zone` / `geospatialEnabled` / `anchorType` parsed with defaults | ✅ exists | `Database/JsonDatabase.cs:97-99` |
| 8 landmarks flagged START/END (4 + 4) | ✅ **in the shipped data** | `landmarks.json` — ids 4,5,6,7 / 38,39,40,41 |
| `GeospatialSession` shell, honest no-op | ✅ exists | `AR/Geospatial/GeospatialSession.cs` |
| **Geospatial → `FeedFix` bridge** | ✅ **already wired** | `UI/Screens/ARNavigationScreen.cs:768-771` |
| DebugOverlay "Geo" row | ✅ exists | `Diagnostics/DebugOverlay.cs:199-205` |
| ARCore Extensions package | ❌ not installed | `Packages/manifest.json` — arfoundation/arcore 6.5.0 only |
| Google Cloud project / API key | ❌ nothing | no cloud footprint in repo |
| `ARCoreExtensions` in the AR hierarchy | ❌ | `AR/ARSessionBootstrapper.cs:101-145` builds all of it in C# |
| Zone → mode switching | ❌ does not exist | this document |

### Doc drift worth fixing while you're here

`Data/LandmarkData.cs:31-39` still says *"No `zone` field exists in the source data yet"* and
*"still an open decision, none chosen yet."* Both were true when written and are now false —
`landmarks.json` carries `zone` and `geospatialEnabled` on 8 entries, and Rev 4 §10 records the
selection as decided 2026-08-27. Update those two doc comments so the next reader isn't misled
into thinking the data pass hasn't happened.

---

## 02 · Correction 1 — you cannot switch on "steps"

**There is no per-step survey data anywhere in this project.** The step figure the app shows is a
linear map from distance onto a constant:

```csharp
const int totalSteps = 3550;                                        // ARNavigationScreen.cs:528
int steps = Mathf.RoundToInt(totalSteps * _session.Progress.FractionComplete);
```

`Map/PoiMarkerLayer.cs:57-61` carries the same constant and the same derivation, and both render
the result with a `~` prefix precisely because it is never measured (PLAN.md §03). A real stairway
does not distribute its steps linearly over ground distance — the flat approach stretches and the
steep flights compress — so "step 500" is not a place the code can locate.

**Express the boundaries in metres of cumulative route distance instead.**
`RouteProgressTracker.CumulativeDistanceMeters` already is exactly that scalar, and per PLAN.md
§08/§09 nothing else in the app computes a competing version of it.

Recomputed from `alipiri_mettu.geojson` by replicating `RouteBuilder.Build`'s goal-directed
chaining (it reproduces the documented topology exactly — 6 ways chained, `way/365041854` rejected
as a spur, 1,171.7 m bridged):

```
Total route          7,288.4 m       157 waypoints
500 steps ≈ 14.08% ≈ 1,027 m         boundary @ 13.655177, 79.399500
300 steps ≈  8.45% ≈   616 m         boundary @ 13.668626, 79.353753  (the 6,672 m mark)
```

Treat those two metre figures as **provisional seeds, not requirements** — see §06 Phase H.

---

## 03 · Correction 2 — 300 steps is too short for the END landmarks you already chose

Projecting the 8 flagged landmarks onto the built route:

| id | Name | Zone | Cumulative | From end |
|---:|---|---|---:|---:|
| 4 | Venkateshwara Swamy Padalu Mandapam | START | 106 m | 7,183 m |
| 5 | Mathsyavataram | START | 125 m | 7,163 m |
| 6 | Rajagopuram | START | 125 m | 7,163 m |
| 7 | Baktha Anjaneya Swamy Statue | START | 226 m | 7,062 m |
| 38 | Dova Bashyakarla Sannidhi | END | 6,579 m | **709 m** |
| 39 | Kulashekhar Alvar | END | 6,949 m | 340 m |
| 40 | Alwar Statue | END | 6,993 m | 295 m |
| 41 | Alipiri Last Step | END | 7,153 m | 135 m |

**A 616 m END zone starts at 6,672 m and therefore excludes landmark 38 at 6,579 m.** One of the
four END landmarks Rev 4 §10 already committed to falls outside the zone your proposal draws.

Two ways out, and they are a real choice:

- **Widen the END zone to ~750 m** (≈ 365 steps) to cover all four with margin. Costs ~130 m more
  VPS runtime, which is negligible.
- **Accept it** and let landmark 38 be served by the existing GPS path, which is what serves the
  other 32 landmarks today and works.

Recommendation: **widen to 750 m.** The whole point of flagging 38 was that it is an END landmark.

The mirror observation for START: all four START landmarks sit within **226 m**, so 1,027 m is
4.5× more zone than the heritage content needs. That is only wasteful if the goal is heritage. It
is *not* wasteful if the goal is what this proposal actually states — precise AR navigation for
the opening climb — but it does mean the START zone is sized by a navigation argument that should
be stated out loud, and it is the part most likely to exceed real VPS coverage (§06 Phase G0).

---

## 04 · Correction 3 — a 1.17 km stretch of the route is invented, and it sits just before the END zone

Segment map, recomputed:

```
way/30434845        0 →  2,574 m     2,574 m
way/367434743   2,574 →  2,582 m         8 m
way/367434740   2,582 →  3,045 m       463 m
way/367434742   3,045 →  3,060 m        16 m
way/367434744   3,060 →  5,227 m     2,167 m
  ⚠  BRIDGE     5,227 →  6,399 m     1,172 m   straight line, NO surveyed geometry
way/30434846    6,399 →  7,288 m       889 m
```

`RouteBuilder` bridges this deliberately and `JsonDatabase.cs:40` already logs a warning about it,
but the consequence for *this* plan is specific: **16% of the route is a polyline that does not
follow the real path, and it ends 273 m before the END zone begins.**

Cumulative distance is therefore least trustworthy exactly where the `OFFLINE_NAV → END_GEO`
transition has to fire. A walker on the real stairway through that stretch will snap onto a
straight line that cuts across it, and their `CumulativeDistanceMeters` will drift from ground
truth in a way no GPS accuracy improves.

**So do not trigger the END zone on cumulative distance.** Trigger it on **haversine distance to
the final waypoint**, which is immune to the bridge because it never touches the polyline. The
START zone can safely use cumulative distance — `way/30434845` is fully surveyed from 0 m.

(Landmark 38 at 6,579 m is past the bridge, on real geometry. The zone containment question in
§03 is genuine, not a bridge artefact.)

---

## 05 · Correction 4 — the existing bridge does not deliver what Rev 4 claims

Rev 4 §04 states that feeding a Geospatial pose into `FeedFix` means *"every existing consumer —
arrows, progress, triggers, map marker — inherits the accuracy with no change."* **As currently
wired, that is not true.**

`LocationProvider.OnFixFiltered` has three independent subscribers:

```
NavigationSession.HandleFix        NavigationSession.cs:39   → Progress.Feed, Triggers.Feed  (:77-78)
ARNavigationScreen.OnLocationFix   ARNavigationScreen.cs:105 → _localization.FeedFix         (:768-771)
MapScreen.OnLocationFix            MapScreen.cs:89           → marker
```

The Geospatial branch lives only in the second one. `RouteProgressTracker` and
`LandmarkTriggerService` are fed from raw GPS by `NavigationSession.HandleFix` and **never see the
Geospatial pose.** The ~1 m accuracy reaches the AR chevron frame and nothing else — not progress,
not the step badge, not landmark triggering, not the map marker.

To get the claimed win, the pose must enter one level lower — at `LocationProvider.EmitFix`
(`:116-118`), which is already the single funnel every consumer hangs off. Two hazards when you
move it there, both already anticipated by existing code:

1. **Bypass the Kalman filter.** `GpsKalmanFilter` is tuned for the 5–25 m noise real Android
   fixes show under canopy (`GpsKalmanFilter.cs:6`). Running a 1 m pose through it smears a good
   measurement toward a worse prior.
2. **Bypass the jitter floor.** `RouteProgressTracker.MinJitterToleranceMeters = 5.0` (`:28`) means
   a 1 m-accurate walker would advance in 5 m chunks — the accuracy would be thrown away at the
   last step. The `trustExactly` parameter (`:64`) exists for exactly this situation and is already
   plumbed through `NavigationSession.HandleFix:76`. Reuse it; do not add a second mechanism.

Both hazards are the same shape: machinery built to defend against bad GPS actively degrades a
good pose. Neither is hard to fix, but neither is optional.

---

## 06 · Phases

Continues Rev 4's lettering. Rev 4's Phase D (the instrumented device walk) still gates everything
and is not superseded here.

| # | Phase | Contents | Gate | Est. |
|---|---|---|---|---|
| **G0** | **Kill-or-continue check** | Street View coverage at `13.646761, 79.405174` (arch) and `13.672393, 79.351832` (Tirumala). **Also check `13.655177, 79.399500`** — the proposed 500-step START boundary. Blue lines = imagery = probable VPS. Costs 30 minutes and can end this direction. | **blocks all below** | ~30 min |
| **G** | **Entry cost** (Rev 4 Phase G, unchanged) | B2: verify `com.google.ar.core.arfoundation.extensions` supports AR Foundation 6.5 *before* installing — it is a `.tgz` from Google's GitHub releases, not the Unity registry, with its own compatibility matrix. B3: Cloud project + ARCore API + auth (§07). B4: `ARCoreExtensions` inside `ARSessionBootstrapper.BuildHierarchy()`. Ends when an APK boots with Earth tracking reaching `Tracking`. | G0 | ~3–4 days |
| **H** | **Measure the zones** | `CheckVpsAvailabilityAsync` at both endpoints **and at intervals inward** — 100/250/500/750/1000 m from the arch, and the mirror at the top. Log horizontal + heading accuracy standing still 60 s at each. **Output is the two boundary numbers**, replacing the §02 seeds. This is the phase that decides whether 500/300 was a good guess. | G | ~1 day field |
| **I** | **Real `GeospatialSession`** | Replace the two no-op method bodies with calls verified against `Library/PackageCache/com.google.ar.core.arfoundation.extensions/…` — not from memory, per this project's standing rule (`ARSessionBootstrapper` class doc). Nothing else changes shape: `TryGetPose`'s signature already matches `FeedFix`. | G | ~2 days |
| **J** | **`RouteZonePolicy`** | The one new class (§08). Pure logic, no AR dependency, desk-testable against `TraceReplaySource` before the package ever lands. Owned by `NavigationSession` so Map/Progress/AR read one mode and cannot disagree. | — (can start now) | ~1 day |
| **K** | **Move the injection down** | §05 — Geospatial pose into `LocationProvider.EmitFix`, filter bypassed, `trustExactly` set. This is what makes progress, triggers, step badge and map marker actually inherit the accuracy. | I, J | ~1 day |
| **L** | **The handoff** | Lead-in, hysteresis, re-seed on mode change (§09). Honest UI in both directions (§10). | K, H | ~2 days |
| **M** | **Field validation** | Walk both ends with `DebugOverlay` + `GpsTraceRecorder`. Specifically watch for the chevron jump at each boundary (§09). | L, Rev 4 D | field |

**Phase J needs nothing from Google and can start today.** It is the only part of this plan not
blocked on the package, the Cloud project, or the mountain.

---

## 07 · The API key question

ARCore Geospatial authorises either by **API key** or **keyless** (bound to the app's signing
certificate).

**Use an API key for development; plan on keyless for release.** An API key ships inside the APK
and is extractable by anyone who unzips it — for a free pilgrimage app the abuse ceiling is low,
but the quota is billed to your Cloud project. If you ship the key:

- Restrict it in Cloud Console to the **ARCore API only**, and to your Android package name +
  SHA-1 signing fingerprint. An unrestricted key is the actual risk, not the key's presence.
- Set a **quota cap** on the project so a leaked key cannot run up a bill.
- Never commit it. `ARCoreExtensionsConfig` is a `ScriptableObject` asset — if the key lands in
  one, it lands in git. Prefer injecting it at build time from `Assets/Editor/BuildScript.cs`
  reading an environment variable.

Keyless is stronger and removes all of the above, but it introduces a signing-config dependency
that `BuildScript.cs` does not currently have (Rev 4 §04 B3 flags the same thing). That is a real
cost, not a formality — budget it rather than discovering it on release day.

---

## 08 · `RouteZonePolicy` — the one new class

> ⚠ **Conditional on the §12 fork.** Zones exist because VPS is a continuous, expensive,
> network-dependent service that has to be switched off across the middle. On the Augmented Images
> path this class is **not needed at all** — see §13.3. Do not build it before the Phase G0 check.

Pure logic, no AR or Unity-service dependency, so it is testable at a desk against the existing
`TraceReplaySource` harness long before ARCore Extensions is installed.

```csharp
// Assets/Scripts/Positioning/RouteZonePolicy.cs
public enum RouteMode { StartGeo, OfflineNav, EndGeo }

public class RouteZonePolicy
{
    public RouteMode Mode { get; private set; }

    /// <summary>Whether the Geospatial session should be RUNNING right now — true inside a zone
    /// AND in the lead-in before it. Not "whether Geospatial works": that is Availability's
    /// answer. This is a power policy.</summary>
    public bool GeospatialRequested { get; private set; }

    public event Action<RouteMode> OnModeChanged;

    /// <param name="cumulativeMeters">RouteProgressTracker.CumulativeDistanceMeters.</param>
    /// <param name="metresToFinalWaypoint">Haversine to the route's last waypoint — NOT derived
    /// from cumulative distance, which is unreliable across the 1,172 m bridge (§04).</param>
    public void Feed(double cumulativeMeters, double metresToFinalWaypoint) { … }
}
```

Boundaries are constructor parameters seeded from §02/§03 and replaced by Phase H's measurements:

| Constant | Seed | Basis |
|---|---:|---|
| `startZoneEndMeters` | 1,027 m | 500 steps, §02 — the one genuinely arbitrary number here |
| `endZoneFromEndMeters` | 750 m | §03 — covers all four flagged END landmarks, not 616 m |
| `leadInMeters` | 200 m | §09 — Earth tracking convergence time |
| `hysteresisMeters` | 75 m | §09 — pilgrims rest |

Own it on `NavigationSession` beside `Progress` and `Triggers`, fed from `HandleFix` after
`Progress.Feed`. That follows the existing rule (`NavigationSession` class doc): one shared
instance, so no two screens can disagree about what mode the walk is in.

With the policy in place the call-site change at `ARNavigationScreen.cs:768` is one condition:

```csharp
if (_geospatial != null && _session.Zones.GeospatialRequested
    && _geospatial.TryGetPose(out var geoLat, out var geoLon, out var geoHeading, out var geoAcc))
```

— and then §05 Phase K moves that whole branch down into `LocationProvider`.

---

## 09 · The handoff — three things that will bite

### Lead-in, not a hard edge

Earth tracking needs roughly 10–30 s to converge after the session starts. Arming Geospatial *at*
the 6,538 m boundary means the walker spends the first half-minute of the "precise" zone on plain
GPS — the exact stretch the feature exists to improve. `leadInMeters` starts the session ~200 m
early so it is already tracking on arrival.

### Hysteresis

Pilgrims stop, rest, and backtrack. A walker sitting at 1,025 m with GPS noise of ±10 m will cross
a bare boundary repeatedly. Each crossing would tear down and rebuild a VPS session. 75 m of
hysteresis costs nothing and removes the failure mode entirely.

### The mode-change discontinuity — watch for this on the device walk

The two paths currently feed **different things**:

```csharp
_localization?.FeedFix(geoLat, geoLon, …);                                    // raw Geospatial pose
_localization?.FeedFix(_session.Progress.Latitude, _session.Progress.Longitude, …);  // route-SNAPPED
```

At a mode boundary the AR frame's origin semantics change from "snapped to the corridor" to "raw
pose." Two consequences:

1. **A visible chevron jump.** `DynamicArrowManager.Refresh` anchors the whole trail to the frame
   (`DynamicArrowManager.cs:40-61`). This codebase has already been bitten once by exactly this
   class of bug — `RouteProgressTracker.cs:14-27` records arrows visibly jumping backward when the
   underlying scalar dipped.
2. **The outlier gate may reject the good fix.** `HybridLocalizationEngine:70` computes
   `rejectThreshold = max(25, 4 × accuracy)`. At 1 m Geospatial accuracy that collapses to the 25 m
   floor — so if the tracked pose has drifted more than 25 m, the *better* fix gets rejected as an
   outlier, six times, before the re-seed path (`:78`) rescues it.

**Fix: force `Establish()` on mode change** rather than letting the nudge path fight it out.
`HybridLocalizationEngine` already has the mechanism and already records a human-readable reason
(`LastReseedReason`, surfaced in `DebugOverlay:212`) — pass `"entered END_GEO"` and the device
walk will show you exactly when it happened.

### What "offline" actually buys

Geospatial does not need continuous connectivity in the naive sense — once localized, Earth
tracking continues on VIO. But VPS acquisition and refresh do need network, and running the
Geospatial stack for a 2–3 hour climb is a **thermal and battery** problem on a phone that is also
running the camera, AR plane detection and the screen at full brightness. Turning it off across the
5.5 km middle is the real win, and it is a power argument rather than a coverage one. Say that in
the commit message; it is the reason the middle zone exists.

---

## 10 · Honesty requirements

This proposal changes what the app is, and two debts are already recorded against it.

**The manifest owes a copy update.** `AndroidManifest.xml:36-39` states that About/Settings copy
must be updated *before shipping a build where code actually opens a socket*. Phase I is when that
becomes due. The new scope sentence:

> Offline navigation for the full climb, with precise AR positioning and heritage content at the
> Alipiri arch and the Tirumala approach.

**The mode must be visible.** `LocationSourceMode` was made public specifically because a hidden
position source turned out to be a liability — the app advanced on its own while the phone sat
still and nothing on screen said why (`LocationProvider.cs:11-15`, Draft1.md D11). A silent
online/offline switch is the same mistake in a new place. Surface the mode in the top pill
alongside the existing source row, and in `DebugOverlay` next to the `Geo` line.

**Degradation must be honest in both directions.** No network, no VPS coverage, or Earth not
tracking inside a zone → fall back to the existing device-tested GPS path and say so. The fallback
is not a failure state; it is what the other 32 landmarks use today.

---

## 11 · Decisions

| Decision | Status | Notes |
|---|---|---|
| **Boundaries in metres, not steps** | **✓ decided — metres** | §02. No per-step survey data exists; `3550` is a hardcoded constant in two files and every step figure ships with a `~`. `CumulativeDistanceMeters` is the only real scalar. |
| **END zone depth** | **open — recommend 750 m** | §03. 616 m (300 steps) excludes landmark 38 at 709 m from the end, one of the four already committed in Rev 4 §10. |
| **START zone depth** | **open — 1,027 m provisional** | §03. Heritage needs only 226 m; 1,027 m is a navigation argument. Most likely of the two to exceed real VPS coverage — Phase H decides. |
| **END trigger metric** | **✓ decided — distance to final waypoint** | §04. Cumulative distance is unreliable across the 1,172 m bridge that ends 273 m before the zone. |
| **Where the Geospatial pose enters** | **✓ decided — `LocationProvider.EmitFix`** | §05. The current `ARNavigationScreen` injection reaches only the AR frame, not progress/triggers/map. |
| **API key vs keyless** | **open — key for dev, keyless for release** | §07. Key must be API-restricted, package-restricted and quota-capped if it ships. |
| **Does VPS cover either end?** | **UNKNOWN — blocks everything** | §06 G0. Unverified as of this document. |
| **Geospatial API at all, vs. a self-owned alternative?** | **open — recommend Augmented Images (§13)** | §12.3. Most of the positioning win with no key, no Cloud project, no INTERNET — and it removes the need for `RouteZonePolicy` entirely (§13.3). Costs terrain anchors. Blocked on the G0 check, not on further design. |
| **Build `RouteZonePolicy` now?** | **✓ decided — no, wait for G0** | §13.3. The class is unnecessary on the Augmented Images path. Building it before the 30-minute desk check risks writing the wrong abstraction. |
| **Does the route data carry altitude?** | **✓ answered — NO** | §12.4. All 165 coordinate tuples in `alipiri_mettu.geojson` are 2-element `[lon, lat]`; `RouteBuilder`'s `elevation` is always 0. Closes the question Rev 4 §10 left open. |

---

## 12 · Alternatives to the API key

Raised after the plan above was written: *can we build our own map instead of paying for / depending
on a Google API key?* The answer splits, because "map" means two unrelated things in this app.

### 12.1 · The display map — already built, never baked

`Map/TileBasemap.cs` loads real OpenStreetMap raster tiles from `StreamingAssets/Tiles/{z}/{x}/{y}.png`,
zoom 14–18, viewport-culled with an LRU texture cache and ODbL attribution via MapScreen's existing
`map.attribution` label. **It needs no API key and no network, and it is already written and wired.**

⚠ **`Assets/StreamingAssets/Tiles/` does not exist, and no baker tool exists in `Assets/Editor/`.**
Every `LoadTile` call therefore returns nothing and exits silently at `TileBasemap.cs:122`, and
`DemoBasemap`'s green schematic shows through as the documented fallback. The map currently rendering
in the app is **not** the OSM basemap the loader was written for. `TileBasemap`'s class doc describes
a bake ("baked once into StreamingAssets/Tiles") that has not happened.

Fixing this is roughly a day: an editor tool that fetches the ~1,360 tiles covering the corridor
bounding box across z14–18 and writes them into `StreamingAssets`. It is unrelated to Geospatial and
worth doing regardless of how §12.2 is decided.

**This does not position the walker.** It is a picture of a map, not a localization source.

### 12.2 · The localization map — the actual question

The Geospatial API key does not buy map imagery. It buys Google's global 3D visual feature map
(derived from Street View) plus their VPS matcher. "Building our own" means building a visual
positioning system. Four options, ranked by realism at this project's scale:

| Option | Key / network | Verdict |
|---|---|---|
| **ARCore Augmented Images** | none | **Recommended alternative** — see §12.3 |
| Self-hosted photogrammetric VPS | none | Not viable here — see below |
| Third-party VPS (Immersal, Lightship) | someone else's key | Trades one vendor for another |
| Better use of existing sensors | none | **Complementary, do this anyway** — §12.4 |

**Self-hosted photogrammetric VPS** — capture the corridor, reconstruct with COLMAP, match features
on-device. `com.unity.ai.inference` 2.6.1 is already in `Packages/manifest.json`, so on-device
inference is technically open. But this is research-grade work measured in months, and outdoor
feature maps decay with season, vegetation, lighting and crowd density — which is precisely why
Google re-captures Street View. Seven kilometres of stairway is a great deal of map to maintain.
Not viable for this project.

**Third-party VPS** — Immersal is the one materially different option, since it supports downloading
a scanned map to the device for offline localization rather than requiring a live lookup. Still an
account, a vendor dependency, and probably a paid tier. Verify current terms directly rather than
from any summary in this document.

### 12.3 · ARCore Augmented Images — the recommended alternative

`ARTrackedImageManager` ships inside `com.unity.xr.arfoundation` 6.5.0, **already installed**. No new
package, no API key, no Cloud project, no INTERNET permission, no billing, no signing dependency —
the entire §06 Phase G entry cost and all of §07 disappear.

Register geo-referenced image targets at surveyed lat/lon along the two ends. When the camera
recognises one, ARCore returns a full 6-DoF pose relative to that target: position **and heading**.

That is exactly the signature the frame already takes:

```csharp
_frame.Establish(lat, lon, compassHeadingDeg, cameraPos, cameraYaw);   // GeoAnchorFrame, via
                                                                       // HybridLocalizationEngine:92
```

So an image hit is a re-seed source, not a second localization engine — the same architectural fit
that made the `FeedFix` bridge cheap (Rev 4 §04). It is also **strictly better than a GPS seed**:
GPS never yields trustworthy heading, which is the whole reason `HybridLocalizationEngine.FeedFix`
is nudging compass yaw at `:87` in the first place.

Candidate targets that need no installation: the Rajagopuram inscription (landmark 6), TTD
signboards, the plaques at the Alvar statues (landmarks 39/40), painted step numbers.

**Real limits, not to be glossed:**
- Requires line of sight to a target; nothing between targets except the existing VIO + GPS fusion.
- Working range roughly 2–8 m depending on target size and print quality.
- Each target needs high-contrast, non-repeating texture. A flat painted signboard with large plain
  areas tracks poorly.
- Installing anything new on the pathway needs TTD permission — a schedule dependency, not a
  technical one.

### 12.4 · Get more from sensors already on the phone

Independent of §12.2 and worth doing either way. The corridor constraint here is unusually strong —
a single path with no alternative route, which is what makes snapping safe at all (PLAN.md §08
step 3). That already collapses 2D error into 1D **along-track** error. Two unused offline signals
address exactly that residual:

**Hardware step counter.** Android's `TYPE_STEP_COUNTER` is hardware-backed and returns *real* steps.
This directly replaces the linear fiction §02 identified — `3550 × FractionComplete` at
`ARNavigationScreen.cs:528` and `PoiMarkerLayer.cs:61`. Under canopy on a stairway, step count is a
better along-track estimate than GPS. The JNI pattern already exists in this codebase
(`Audio/AndroidTextToSpeech.cs`, `com.unity.modules.androidjni`). Requires the
`ACTIVITY_RECOGNITION` permission on API 29+; `AndroidMinSdkVersion` here is 26.

Note this would make the step badge *measured* rather than estimated — at which point the `~` prefix
that `PoiMarkerLayer.cs:328-330` deliberately carries could finally come off for the live figure.

**Barometer → relative altitude.** On a monotonic ~500 m climb, altitude is a strong along-track
proxy. ⚠ **Currently blocked:** all 165 coordinate tuples in `alipiri_mettu.geojson` are 2-element
`[lon, lat]` — zero elevation anywhere. `RouteBuilder.AppendWaypoints` stores `(float)elevation`
that is always 0, and `KmlImporter.cs:195`'s "altitude present" branch has never fired on this data.
A free DEM (SRTM or Copernicus) baked against the route polyline would unblock it, and would also
give the elevation profile Rev 4 §10 wanted.

### 12.5 · What dropping Geospatial actually costs

**Terrain anchors.** `GeoAnchorFrame.GeoToWorld` is a local planar approximation with no altitude
term at all (`:39`), which is fine for a chevron the ground raycast re-places anyway and useless for
a statue that must stand at correct height on a slope. That was Rev 4 §04's stated reason for
wanting Geospatial. Augmented Images gives accurate *positioning*; it does not give a
height-resolving anchor.

Counter-argument worth weighing: heritage content here is clustered at 8 known landmarks, and an
image target *at* each landmark is arguably a better anchor than a terrain anchor — it is locally
exact, needs no network, and cannot drift when VPS coverage is marginal.

**The upside is equally concrete.** Choosing this path means `tools:node="remove"` goes back onto
INTERNET in `AndroidManifest.xml`, and the "fully offline" scope reversed on 2026-08-27 (Rev 4 §10)
is reinstated rather than needing the disclosure copy §10 above requires.

### 12.6 · Recommended sequencing

Both branches share a prerequisite and a first step, so the decision does not have to be made today:

1. **Bake the OSM tiles** (§12.1). Unrelated to the fork, fixes a live latent gap, ~1 day.
2. **Run §06 Phase G0**, the Street View desk check. It is 30 minutes and it decides the fork: no
   coverage at the ends means Augmented Images is not an alternative, it is the only option.
3. **Build `RouteZonePolicy`** (§08). It is pure logic and is needed either way — the zones are a
   power/precision policy regardless of which source provides the precision.
4. **Then fork**, informed by 2.

---

## 13 · Recommendation

§§02–11 are a sound plan for the Geospatial path. This section records the view that the Geospatial
path is probably not the one to take, and why, so the document does not read as neutral between two
options it is not neutral about.

**Recommendation: do the cheap offline work first, run the G0 check, then fork — and expect the fork
to resolve toward Augmented Images (§12.3).**

### 13.1 · The core mismatch

Geospatial delivers ~1 m **positioning**. What this app needs to know is:

- where the walker is along the path
- which way the arrows point
- whether a landmark is near

This is a **single corridor with no alternative route** — the constraint that makes snapping safe at
all (PLAN.md §08 step 3) and that collapses 2D error into 1D along-track error. Fifteen metres of
along-track error means the chevrons are slightly early or late. It does not mean they point the
wrong way, because there is no wrong way to point.

**So ~1 m is over-specified for the navigation problem and correctly specified for the content
problem.** Rev 4's original heritage framing was the better-matched use of Geospatial. Reframing it
as a navigation aid (§§02–11) tells a more appealing story but aims the most expensive dependency in
the project at the part of the app that was already least broken.

### 13.2 · What is actually weak today

Three real gaps, none of which Geospatial addresses, all cheap and vendor-independent:

| Gap | Evidence | Cost |
|---|---|---|
| **The map is a green schematic** — OSM tiles never baked | §12.1, `TileBasemap.cs:122` | ~1 day |
| **The step count is fiction** — `3550 × FractionComplete` | §02, `ARNavigationScreen.cs:528` | ~2 days (§12.4) |
| **The instrumented walk has never happened** — every threshold is an unmeasured guess | Rev 3 Phase D, still open | ~1 week field |

Steps are how pilgrims actually measure this climb, and on a stairway where flat approaches stretch
and steep flights compress, a linear estimate can be badly wrong. That is the defect users will
notice, and a hardware step counter fixes it offline for a fraction of Phase G's cost.

### 13.3 · The simplification that decides it

**Zones exist only because VPS is a continuous, expensive, network-dependent service that must be
switched off.** Augmented Images is free and event-driven: leave `ARTrackedImageManager` running for
the whole climb and take a re-seed whenever a target happens to be in view.

On that path the following all become unnecessary:

- `RouteZonePolicy` (§08) — the class this document is organised around
- The 500-vs-300-step boundary argument (§02, §03)
- Boundary measurement (§06 Phase H)
- Lead-in, hysteresis, and the mode-change chevron jump (§09)
- The API key handling in §07, the Cloud project, and the signing dependency
- The INTERNET reversal and its outstanding copy debt (§10)

That is most of this plan deleting itself, which is a strong signal about which path is actually
simpler rather than merely cheaper.

### 13.4 · Sequencing

```
Week 1   Bake OSM tiles (§12.1) · hardware step counter (§12.4) · instrumented walk (Rev 3 D)
30 min   Phase G0 Street View check (§06)
Then     Fork, with measured numbers in hand
```

Every item in week 1 is certain, offline, and needed on both paths. The expectation is that after it,
the felt need for Geospatial is much reduced — because the step counter fixes what users notice, the
tiles fix what they see, and the walk finally says whether GPS-in-a-corridor was ever the bottleneck.

### 13.5 · What would reverse this recommendation

**Real VPS coverage at both ends.** If G0 shows blue lines and Phase H confirms Earth tracking, take
Geospatial. Terrain anchors genuinely solve the altitude problem — `GeoAnchorFrame.GeoToWorld` has no
altitude term at all (`:39`) — and they arrive without negotiating physical signage with TTD.

### 13.6 · The risk on the recommended path is institutional, not technical

If TTD will not permit installed markers, **and** the existing signboards, inscriptions and plaques
turn out to be poor tracking targets — low contrast, repetitive, weathered — then Augmented Images
collapses and the project is back at the Geospatial path having lost weeks.

**Mitigation, cheap and early:** photograph six candidate features on the next trip (Rajagopuram
inscription, the Alvar plaques, TTD signboards, painted step numbers) and run them through ARCore's
image quality scorer before committing to anything. That is an afternoon and it de-risks the whole
recommendation.

### 13.7 · Status of the rest of this document

- **§§02–05 (corrections)** — true regardless of the fork. Keep.
- **§§06–09 (phases, key handling, `RouteZonePolicy`, handoff)** — **conditional.** Superseded if the
  fork goes to Augmented Images.
- **§10 (honesty requirements)** — applies to the Geospatial path; the INTERNET debt disappears on
  the other one.
- **§12** — the fork.

---

*Route data © OpenStreetMap contributors, ODbL — attribution required in the shipped app.*
*Rev 5 covers online/offline navigation zoning only. `Docs/GeospatialPlan.md` (Rev 4) remains
plan-of-record for the heritage layer; `Docs/Draft1.md` (Rev 3) for the navigation app.*
