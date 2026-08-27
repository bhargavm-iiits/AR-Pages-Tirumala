# Alipiri AR Navigation — Build Plan

**Target:** Unity 6000.5.5f1 · URP 17.5 · AR Foundation 6.5 · ARCore 6.5 · Android, fully offline
**Project:** `D:\Unity Projects\AR_pages` (all work happens here)
**Rev 2** — scope locked

A fully offline outdoor AR wayfinding app for the **7.29 km** pilgrim stairway from Tirupati to
Tirumala. 12 screens matching the mockups in `Assets/Imgaes/`, live GPS position on a map, and
every control wired to a real service. Written against **verified folder contents**, not assumptions.

## Scope locked for v1

| Decision | Consequence |
|---|---|
| **Login screen** capturing name/age/language → `Assets/login.xlsx` | Becomes screen 0, gates first launch. Dependency-free OOXML writer, dual path by platform (§12) |
| **Everything written fresh** in this project | Nothing ported from `AR_Navigation`. Adds ~1 week, concentrated in AR pose/placement work |
| **Demo basemap** | No tile licence question, no 20 MB bake. Projection + live tracking still real; swap-in later touches one class (§08) |
| **Only `Assets/Docs/` data** | English only; TTS covers voice; procedural category tiles replace 41 photos; elevation chart shows empty state; step counts are labelled estimates (§03) |
| **Straight-line gap until KML** | Bridge waypoints flagged `isBridged`, rendered amber. `KmlImporter` built in v1 so your file drops straight in (§01) |

---

## 01 · The route, measured from your own GeoJSON

Computed from `Assets/Docs/alipiri_mettu.geojson` with the haversine formula. These are facts about
your file, and one of them dominates the whole project.

### Route composition — 7 ways, 6 chain at 0.0 m, one does not

| OSM way | Vertices | Length | Join to next | Role |
|---|---:|---:|---:|---|
| `way/30434845` | 65 | 2,573.7 m | 0.0 m | Alipiri arch → first landing |
| `way/367434743` | 2 | 7.9 m | 0.0 m | connector stub |
| `way/367434740` | 12 | 463.1 m | 0.0 m | mid flight |
| `way/367434742` | 2 | 15.6 m | 0.0 m | connector stub |
| `way/367434744` | 47 | 2,167.1 m | **1,171.7 m** | → ends mid-mountain |
| **⚠ NO GEOMETRY** | — | **1,171.7 m** | — | from `13.664720, 79.365567` to `13.666465, 79.354873` |
| `way/30434846` | 33 | 889.5 m | — end | Mokallu Mettu → Tirumala |
| `way/365041854` | 4 | 84.6 m | 116.6 m off | **REJECT** — parallel spur |

- **Start (Alipiri):** `13.646761, 79.405174`
- **End (Tirumala):** `13.672393, 79.351832`

### Derived figures

| Figure | Value | Consequence |
|---|---:|---|
| Surveyed geometry | 6,116.9 m | real vertices you can navigate on |
| Straight-line bridge | 1,171.7 m | **16.1 % of the route** is a guess |
| Total route length | 7,288.6 m | drives ETA, progress %, distance remaining |
| Total vertices | 165 | ~44 m mean spacing — needs interpolation to ~5 m for AR |
| Coordinate dimensions | 2 | **zero elevation data** — elevation chart cannot be built |
| Bounding box | 79.3518→79.4052 E, 13.6468→13.6724 N | 5.78 × 2.83 km |

### ⚠ The one risk that outranks everything else

OpenStreetMap has never digitised the middle 1.17 km of the Alipiri stairway. The route builder must
bridge it with a straight line, and a straight line between two points on a switchbacking mountain
staircase **points through the hillside**. For ~16 % of the walk, AR arrows will confidently aim at
rock and trees.

No amount of code fixes this. Until your KML lands, the honest behaviour is: render those arrows
amber, drop to map-only mode, show "route approximate in this section". Plan any demo to end before
the gap.

---

## 02 · What you have in `AR_pages` today

| Asset | Status | Detail |
|---|---|---|
| UI design mockups | **have** | 11 screen PNGs in `Assets/Imgaes/pilgrimage_navigation_individual_screens/` + composite `Demo_1.png` (carries an APP FLOW bar) + full-res `steps.png`. A complete, unusually well-specified visual brief. |
| Route geometry | **have** | `Assets/Docs/alipiri_mettu.geojson` — 7 ways, 165 vertices |
| Landmark data | partial | `Assets/Docs/json_coorinates.txt` — 41 landmarks. Invalid JSON as written (§04) |
| Latin typeface | **have** | Google Sans full family in `Assets/Fonts/static/`. Matches the mockups. **Latin glyphs only.** |
| Render pipeline | **have** | URP 17.5 with `Mobile_Renderer` + `Mobile_RPAsset` — correct for Android AR |
| TextMesh Pro | **have** | Essentials imported + `Assets/Editor/TMPEssentialResourcesImporter.cs` |
| Input System | **have** | 1.19.0 + `InputSystem_Actions.inputactions` — needed for map gestures |
| Scene | stub | `Assets/Scenes/Pages.unity` — one empty scene |
| Scripts | **none** | Zero app C#. Only the TMP editor importer. |
| AR packages | **none** | `manifest.json` has no AR Foundation, no ARCore, no XR Plugin Management |
| StreamingAssets | **none** | Folder does not exist |
| Template cruft | delete | `Assets/TutorialInfo/` + `Readme.asset`. `Assets/Docs/New folder` is empty. |

### On the sibling project

`D:\Unity Projects\AR_Navigation` is a separate working project with ~14k LOC and a shipping APK.
Per your decision, **nothing is ported from it** — every file in §10 is written fresh here. The
expensive rewrites are the GPS Kalman filter, the route chainer, and the AR pose fusion — roughly a
week of the estimate. What you get back is a codebase with no inherited assumptions, which matters
most in exactly those three files.

---

## 03 · Content deferred, and how v1 degrades honestly

v1 is scoped to `Assets/Docs/` data only. Four things in the mockups draw on data that does not
exist in those two files. Rather than fabricate values, each gets a defined honest fallback.

| Mockup element | Data needed | v1 behaviour |
|---|---|---|
| Landmark thumbnails (screens 3, 6) | 41 photographs | Procedural category tile — gold gopuram / blue droplet / stone statue / step glyph on a tinted ground, keyed off normalised `type`. Reads as design, not a broken image. Swaps to photos by dropping files in. |
| Elevation profile (screen 4) | Per-vertex altitude | GeoJSON is strictly 2D, so the chart cannot be truthful. Render an **empty state**: "Elevation data not available". May arrive free with your KML. |
| "200 Steps" / "Steps Climbed ~1,250" (screens 1, 4) | Step counts | No step field exists. Derive a **clearly-labelled estimate** from distance against the ~3,550 total steps, prefixed `~` as the mockup itself does. Never present as measured. |
| Satellite basemap (screen 2) | ~480 raster tiles | Demo basemap — §08. Correct projection, no imagery. |
| Icon set (all screens) | ~22 glyphs | Drawn procedurally via `UIShapes` where geometry allows. Only the gopuram brand mark really wants an authored asset. |

### One ask for when you send the KML

KML `<coordinates>` supports `lon,lat,altitude` triples. **If your export can include altitude**,
the elevation profile and a far better step estimate both come free — no DEM sampling, no survey
walk. Check the export options before generating it. If it's 2D only, the fallbacks above hold.

### Deferred to a later version

| Missing | Volume | Notes |
|---|---:|---|
| Indic fonts | 4 TTFs | Noto Sans Telugu / Devanagari / Tamil / Kannada, free from Google Fonts. Without these, translated labels render as tofu boxes. |
| UI translations | ~130 × 4 | Machine translation will mangle *Mathsyavataram*, *Padalu Mandapam*, *Sannidhi* — needs a human. |
| Landmark text translations | 41 × 4 | Names + descriptions + voice lines |
| Voice audio | 41 × 5 + ~15 × 5 | All 41 rows point at a placeholder `Audio/temple1.mp3` that doesn't exist. **v1 needs none of it** — `voiceText` carries real English, so Listen works through Android TTS from day one. |
| AR chevron asset | 1 | **In v1** — built procedurally, URP unlit + distance fade. No authored asset. |
| App icon + splash | 1 set | Adaptive icon, all density buckets. Needed before distribution, not before it runs. |
| Route gap geometry | 1,171.7 m | **You're supplying** — superseded by your KML |

---

## 04 · Data defects to fix while creating `landmarks.json`

Found by inspecting `json_coorinates.txt` directly. Each produces a visible bug if shipped.

| Defect | Evidence | Symptom |
|---|---|---|
| Longitude typo | Line 60: `"longitude": 779.40456473877819` | Extra leading `7`. Landmark lands ~700° east. Kills bbox validation or teleports a marker. |
| Invalid JSON | Trailing `,` before closing `]` | Parser throws. Entire landmark database fails, app boots empty. |
| Inconsistent type casing | `"Statue"`×10 + `"statue"`×1; `"Steps"`×11 + `"steps"`×3 | Filter chips silently drop 4 landmarks. Normalise to a `LandmarkType` enum on parse. |
| Filter chip mismatch | Mockup chips: All / Temple / Water / **Status** / Steps | "Status" is a typo for **Statue**. Also `Medical`×1 and `Shops`×1 have no chip — add a 6th or an "Other" bucket. |
| Placeholder audio | All 41 rows: `"audio": "Audio/temple1.mp3"` | File doesn't exist. Guard the player against missing clips. |
| Spur in route source | `way/365041854`, 116.6 m off chain | Naive concatenation produces a route that jumps sideways 116 m and back. Chain by endpoint proximity, reject non-joining ways. |
| Voice text to audit | Hand-authored, copy-pasted rows | Sibling project's copy had lines pasted onto wrong landmarks (Anjaneya text on Kurma Avataram, "1500 step" on Step 1600) and a duplicated Dorasani Mandapam. Read all 41 rows against the names. |
| No description field | Screen 3 shows description under the name | `voiceText` reads correctly as a description — bind it, but add an explicit `description` field so voice and display copy can diverge. |

---

## 05 · Screen specification, read off the mockups

`Demo_1.png` carries an explicit APP FLOW strip:
**AR Navigation → Mini Map → Landmarks → Progress → Settings** — a five-tab app that opens straight
into AR, with language living in Settings. No login screen appears in any of the 11 frames, so
**screen 0 is designed, not traced**: it borrows the mockups' exact ground, card, pill and button
treatments so it reads as part of the same set.

### Design tokens taken from the mockups

**Colour**
- Ground `#0B1520` → `#111C27` deep navy
- Card surface `#16202B`, translucent glass over camera
- Primary action `#2F7BFF` blue — chevrons, buttons, active tab
- Success `#3DBE6E` — progress bar, visited checks, arrival
- Warning `#F0A824` — low-GPS, unvisited rings
- Gold/bronze gopuram glyph — brand mark only

**Type & form**
- Google Sans throughout; SemiBold for values, Regular for labels
- Pill/stadium shapes for chips and top bars
- Circular buttons ~48 dp on the AR rails
- Cards ~14 dp radius, 1 px hairline border
- Label above value in stat columns, caption below

### Screens

| # | Screen | Composition | Backing services |
|---|---|---|---|
| 0 | **Login** (first launch, *designed*) | Navy ground, gold gopuram mark + wordmark. Pill inputs: Name (text), Age (numeric + −/+ stepper), Language (chips, each in its own script). Inline amber error under the offending field. Full-width blue Continue, disabled until valid. | ProfileService, ExcelLoginStore, LocalizationService |
| 1 | **AR Navigation** (tab, default) | Full-bleed camera. Blue chevron trail on ground. Top pill: gopuram + name + "250 m ahead" + speaker. Left rail: compass, `AR`, `Auto`, brightness. Right badge: step count. Bottom glass card: Distance / Next Landmark / ETA + green progress bar. | ARSession, DynamicArrowManager, GroundPlacementService, HybridLocalizationEngine, RouteProgressTracker, VoiceNavigationManager |
| 2 | **Mini Map** (tab) | Demo basemap (§08) under the mockup's chrome. Blue route polyline with vertex dots. Gopuram POI pins. Blinking user chevron. Top pill: "Alipiri to Tirumala · 2.1 km · ETA 42 min" + gear. Right rail: recentre, 2D/3D, north. Bottom "Next: X ›" card. | MapView, DemoBasemap, RouteOverlay, LocationProvider, LandmarkRepository |
| 3 | **Landmarks** (tab) | Title + search. Filter chips All/Temple/Water/Statue/Steps(/Other). Scroll list: thumbnail, numbered badge, name, right-aligned distance, description, green-check or amber-ring status. | LandmarkRepository, VisitedStore, LocationProvider |
| 4 | **Progress** (tab) | Circular % ring. Stat rows: Total Distance, Completed, Remaining, ETA, Steps Climbed. Elevation profile area chart with axis labels. | RouteProgressTracker, ElevationProfile *(needs DEM)*, StepEstimator *(needs data)* |
| 5 | **Settings** (tab) | Row list with leading icons: Voice Guidance (switch), Language (value + chevron), Units (value + chevron), Auto Brightness (switch), Haptic Feedback (switch), Offline Maps (chevron), About (chevron). | SettingsStore, LocalizationService |
| 6 | **Landmark popup** (modal) | Photo hero, ✕ close, name, category subtitle, description, full-width blue "🔊 Listen". | LandmarkRepository, VoiceNavigationManager |
| 7 | **Turn instruction** (overlay) | "Turn Left / in 50 m", large blue arrow glyph, "towards Mokallu Mettu", 5-dot pagination. | ManeuverDetector |
| 8 | **Arrival** (overlay) | Confetti burst, green check disc, "You have reached X", blue "Next Landmark". | LandmarkTriggerService, ConfettiBurst, VisitedStore |
| 9 | **Low GPS** (overlay) | Amber warning triangle, "GPS Signal Weak", "Move to open area for better accuracy.", amber OK. | LocationProvider watchdog |
| 10 | **Pause / Resume** (overlay) | "Navigation Paused", pause glyph in a glowing ring, blue Resume, ghost End Navigation. | NavigationSession |
| 11 | **Night mode** (variant) | Screen 1 with camera feed darkened and chevrons emissive-bright. Same chrome, same layout. | Post-exposure on ARCameraBackground |

### ⚠ Numbers in the mockups are placeholders

The frames say "2.1 km", "ETA 42 min", "945 m completed", "~1,250 steps". Your real route is
**7,288.6 m**. Bind every one of these to the live service and format through the units setting.

---

## 06 · Every control, and what makes it functional

50 interactive elements across 12 screens. Build this as a checklist — anything here without a
working handler is a bug, not a placeholder.

### Screen 0 · Login

| Control | Action | Requires |
|---|---|---|
| Name field | Trim on end-edit, 40 char cap, required. Error clears when valid. | TMP_InputField |
| Age field | Numeric-only keyboard, validated 1–120 | TMP_InputField |
| Age − / + | Step by 1, clamped, repeat-on-hold. Faster than a keyboard for older users. | — |
| Language chips | Single-select; calls `Loc.SetLocale` **immediately** so the screen retranslates live | LocalizationService |
| Continue | Validate → save profile → append row to `login.xlsx` → load AR tab. Never blocks entry if the write fails. | ProfileService, ExcelLoginStore |
| Hardware back | At login root, backgrounds the app rather than skipping the screen | BackStackManager |

### Screen 1 · AR Navigation

| Control | Action | Requires |
|---|---|---|
| ☰ Hamburger | Side drawer: pause, end navigation, jump to tab, language | DrawerController |
| Top landmark pill | Tap → landmark popup for current target | LandmarkRepository |
| 🔊 Speaker | Toggle voice mute; icon swaps; persists | VoiceNavigationManager, SettingsStore |
| Compass rose | Recentre heading / calibrate prompt if accuracy low | Input.compass |
| `AR` toggle | Switch AR ↔ Map without losing session state | UIRoot, NavigationSession |
| `Auto` toggle | Auto-advance to next landmark vs manual | LandmarkTriggerService |
| ☀ Brightness | Cycle Auto → Day → Night; drives `Screen.brightness` + night shading | SettingsStore |
| Step badge | Tap → Progress tab | StepEstimator |
| Bottom stat card | Tap → Progress tab; long-press → Pause overlay | RouteProgressTracker |
| Progress bar | Display only — completed ÷ 7,288.6 m | RouteProgressTracker |

### Screen 2 · Mini Map

| Control | Action | Requires |
|---|---|---|
| ← Back | Pop back stack (hardware back maps here too) | BackStackManager |
| ⚙ Gear | Push Settings | UIRoot |
| ⊙ Recentre | Centre on user, re-enable follow-me | MapView |
| `2D` | Toggle flat ↔ tilted/rotated; label swaps 2D/3D | MapView |
| ▲ North | Toggle north-locked ↔ rotate-with-heading; needle animates | MapView, compass |
| POI pins | Tap → landmark popup | LandmarkRepository |
| Bottom "Next ›" card | Tap → landmark popup for next target | LandmarkTriggerService |
| Pinch / drag / double-tap | Zoom z14–z18, pan, double-tap zoom-in; clamped to route bbox | MapView + Input System |

### Screens 3–5 · Landmarks, Progress, Settings

| Control | Action | Requires |
|---|---|---|
| 🔍 Search | Expand field, live case/diacritic-insensitive filter on name + description | LandmarkRepository |
| Filter chips ×6 | Single-select type filter; count updates; empty state if none | normalised LandmarkType enum |
| Landmark card | Tap → popup. Distance recomputes live from GPS. | LocationProvider |
| Status ring | Manually mark visited/unvisited (useful when a trigger is missed under canopy) | VisitedStore |
| 5 bottom tabs | Switch screen, preserve each tab's scroll position and map camera | UIRoot, BackStackManager |
| Elevation chart | Drag to scrub → tooltip with distance + elevation | ElevationProfile |
| Voice Guidance switch | Master mute for TTS + clips; persists | SettingsStore |
| Language row | Picker listing each language in its own script; applies `Loc.SetLocale` **immediately**, whole UI retranslates with no reload | LocalizationService |
| Units row | Metric ↔ Imperial; every distance/ETA label reformats live | SettingsStore, UnitFormatter |
| Auto Brightness switch | Ambient-driven brightness during AR | SettingsStore |
| Haptic Feedback switch | Gate all `Handheld.Vibrate` calls | HapticService |
| Offline Maps row | v1: basemap mode, route source, whether the 1,171.7 m bridge is still interpolated | BasemapSource, RouteBuilder |
| Edit profile row | Reopens login pre-filled; saving appends a new Excel row rather than mutating the old one | ProfileService |
| Export login sheet | Regenerates `login.xlsx`, opens Android share sheet. Editor builds reveal the `Assets/` path. | ExcelLoginStore |
| About row | Version, build, credits, licences, **"© OpenStreetMap contributors"** | — |

### Overlays 6–10

| Control | Action | Requires |
|---|---|---|
| ✕ Close | Dismiss with scale+fade; tap-outside and back also dismiss | UITween |
| 🔊 Listen | Play localised clip → TTS → on-screen caption. Shows playing/stop state. | VoiceNavigationManager, AndroidTextToSpeech |
| Turn card dots | Swipe through queued manoeuvres; auto-dismiss when passed | ManeuverDetector |
| Next Landmark | Mark visited, advance target, re-aim arrows, dismiss | LandmarkTriggerService |
| OK (low GPS) | Dismiss, suppress re-showing for 60 s | LocationProvider watchdog |
| Resume | Resume session, restart AR session and GPS updates | NavigationSession |
| End Navigation | Confirm → tear down AR, save progress, return to Landmarks tab | NavigationSession, VisitedStore |

### Android hardware back must be wired too

A `BackStackManager` handling back is not optional: it closes the top overlay, then pops the pushed
screen, then returns to the default tab, then shows a "leave navigation?" confirm at the root.
Without it, back kills the app mid-walk.

---

## 07 · Fitting every Android phone exactly

Portrait-locked, but Android portrait spans 16:9 to 21:9 plus notches, punch-holes, gesture bars
and foldables.

### Canvas scaling
- `CanvasScaler` → Scale With Screen Size
- Reference resolution **1080 × 1920**
- `Match Width Or Height = 0` (match **width**)
- Horizontal rhythm then reads identically on every device; extra height on tall phones flows into
  scroll areas and the gap between top pill and bottom card — never into stretched art.

### Safe area
- A `SafeAreaFitter` on a child of the root canvas, driven by `Screen.safeArea`
- Re-evaluate on resolution/orientation change, not just `Start()`
- Bottom tab bar anchors to the **safe-area** bottom, not screen bottom — otherwise the gesture pill
  sits on top of it
- Top pill and rails inset below the punch-hole

### Rules that keep layout intact
- **Anchors, never absolute offsets.** Every card stretches between anchors with padding.
- **Layout groups + ContentSizeFitter** for list cards and stat rows, so a longer translated string
  grows the card instead of clipping.
- **TMP auto-size with a floor** on every label taking translated text. Telugu and Tamil run 20–40 %
  longer than English.
- **48 dp minimum touch target** on every control, including the AR rail circles.
- **Critical HUD inside a 90 % central rect.** On 21:9 the extremes are hard to reach one-handed
  while climbing.
- **AR camera background is not a UI element.** `ARCameraBackground` fills at the sensor's aspect;
  overlays must not assume the feed matches the canvas aspect.
- **Test buckets:** 1080×1920 (16:9), 1080×2160 (18:9), 1080×2340 (19.5:9), 1080×2400 (20:9),
  1080×2520 (21:9), 1536×2048 tablet, foldable inner ~1:1. Add all seven as Game-view resolutions
  and screenshot-diff against the mockups.

### Why procedural C# UI, not prefabs

Build screens in code through a `UIFactory` + `UITheme` pair. Every card, chip and pill becomes a
function call with tokens for colour/radius/spacing — so a spacing change propagates everywhere at
once, translated labels re-measure automatically, and there is no prefab to drift out of sync with
the mockups. It also makes the 11 frames reviewable as code diffs.

---

## 08 · Demo map with live position as you climb

The core requirement stands regardless of basemap: as the user walks the steps, **the marker must
move**, offline, with no network at any point. That comes from the positioning pipeline — not the
imagery — which is why a demo basemap costs nothing real.

### What the demo basemap is

Not a stretched screenshot. A stretched JPG cannot reproject — zoom or pan and the marker slides
off the coordinate it belongs on, the one thing this screen must never do. Instead it is **drawn**,
in correct Web Mercator, at the same zoom levels real tiles would use:

- Deep navy ground matching the mockup, with a subtle contour-style hatch so the surface reads as
  terrain rather than a void
- A graticule that **densifies with zoom** — 0.01° at z14 down to 0.0005° at z18 — so pinch-zoom
  feels like a real map and scale stays legible
- A scale bar and live lat/lon readout, both driven by the actual projection
- Route polyline, vertex dots, 41 POI pins and user marker layered on top exactly as in v1.1

Everything sits behind an `IBasemapSource` interface with one method: give me the texture for tile
`{z,x,y}`. `DemoBasemap` generates it procedurally; `TileBasemap` will read
`StreamingAssets/Tiles/{z}/{x}/{y}.png`. **Swapping in real imagery later is one new class and one
line in `AppConfig`** — no rework of projection, gestures, markers or tracking. For reference: your
bbox needs only ~480 tiles across z14–z18, about 15–20 MB.

### How to test the whole thing without leaving your desk

Build a **trace replay** mode into `LocationProvider`: instead of `Input.location`, walk a synthetic
fix along the 7,288.6 m polyline at ~1.2 m/s with injectable noise and accuracy values. That
exercises the Kalman filter, route snapping, progress, ETA, landmark triggers, turn cards and the
low-GPS warning end to end in the editor. Highest-leverage test harness in the project — and it
stays useful for reproducing whatever goes wrong on the mountain.

### Live position pipeline

1. **Acquire.** `Input.location.Start(5f, 1f)` — 5 m desired accuracy, update every 1 m of movement.
   Request `ACCESS_FINE_LOCATION` at runtime with a localised rationale.
2. **Smooth.** Feed each fix through a `GpsKalmanFilter`. Raw Android fixes under tree canopy jitter
   10–30 m; unfiltered, the marker teleports and the ETA flickers.
3. **Snap to route.** `PolylineUtility.ClosestPointOnPolyline` projects the filtered fix onto the
   7,288.6 m polyline, returning cumulative distance along it. The stairway is a corridor with no
   alternative path, so snapping is both safe and a massive accuracy win — lateral error collapses.
4. **Derive everything from that one scalar.** Completed distance, remaining, progress %, next
   landmark, distance-to-landmark, ETA (rolling pace estimate, not a constant), and elevation/step
   figures once baked. One source of truth feeds screens 1, 2, 3 and 4 — so they cannot disagree.
5. **Project to screen.** The marker converts lat/lon → Web Mercator world pixels → tile + pixel
   offset using the *same* transform that positions the tiles. Sharing the transform is what
   guarantees the marker never drifts from the basemap at any zoom.
6. **Heading.** `Input.compass.trueHeading`, smoothed; above ~1.5 m/s prefer GPS course, steadier
   than a magnetometer near iron handrails.
7. **Follow-me.** Camera follows the marker until the user pans, which releases follow and lights up
   the recentre button.
8. **Degradation.** An accuracy watchdog raises screen 9 above ~25 m, and the marker's confidence
   halo grows with reported accuracy rather than lying about precision.

### Attribution is a licence obligation

The route data is OpenStreetMap, ODbL — so **"© OpenStreetMap contributors"** must be visible on the
map screen and in About *in v1*, demo basemap or not. The obligation comes from the geometry you're
already using, not the imagery. One thing the demo basemap does buy: procedurally-drawn ground has
no imagery licence at all.

---

## 09 · The AR engine — arrows that stay on the steps

Outdoor AR on a covered, tree-shaded stone staircase is close to the hardest case ARCore faces. The
design must assume plane detection will frequently fail.

### Rejected: the ARCore Geospatial API

The obvious 2026 choice, and genuinely more accurate than GPS where it works — but it **requires
internet on every localisation** to reach Google's VPS service, which contradicts the entire premise
of this app. Its coverage also derives from Street View, and a tree-canopied covered stairway has
essentially none.

### Pose: fuse four weak signals into one good one

- **ARCore VIO** — excellent short-term relative motion, drifts over minutes
- **Filtered GPS** — bounded absolute error, noisy, poor under canopy
- **Route snap** — the corridor constraint; collapses lateral error
- **Compass** — coarse absolute heading to align the AR frame to true north

A `GeoAnchorFrame` holds the transform between the AR session's local space and geographic space; the
engine absorbs drift by nudging that frame rather than teleporting arrows, so corrections are
invisible to the walker.

### Ground placement: three tiers, so an arrow always exists

| Tier | Method | When it works |
|---|---|---|
| 1 — best | Raycast against `ARPlaneManager` horizontal planes | Open landings, wide flat sections |
| 2 — good | Raycast against ARCore feature points | Textured stone treads where planes never converge |
| 3 — always | Fixed **1.4 m below camera**, on the route bearing | Deep shade, motion blur, phone pitched down. Never leaves the user arrow-less. |

Expose the tier thresholds on an `AppConfig` asset so they are tunable on-device, and surface the
*active tier* in a debug overlay — you will need to know which tier is carrying each section.

**Upgrade after tiers 1–3 work:** the ARCore Depth API via `AROcclusionManager` gives per-pixel
depth, which outperforms planes on stairs and lets the handrail occlude arrows for real depth
perception.

### Arrow rendering

- Chevrons instanced along the next ~40 m of route, spaced ~2.5 m, re-pooled as the user advances —
  matching the mockup's receding trail
- Route interpolated to ~5 m spacing first; 44 m mean vertex spacing is far too coarse to look like
  a path
- URP unlit, additive-ish blue with distance fade so far chevrons don't stack into a solid bar
- Amber tint automatically across the §01 gap section — honest signalling
- Night mode raises emissive intensity rather than adding light

---

## 10 · Files to create

~66 scripts, **all written fresh in this project**, nothing ported.
Plain = in v1; *italic* = deferred until content or your KML arrives.

```
Assets/Scripts/
├─ Core/
│   ├─ ServiceLocator.cs          service registry, no scattered singletons
│   ├─ AppConfig.cs               ScriptableObject: all tunables, device-editable
│   ├─ AppBootstrap.cs            boot order, DontDestroyOnLoad, data validation
│   └─ UnitFormatter.cs           metric/imperial, locale-aware number+distance
├─ Route/
│   ├─ GeoJsonParser.cs           features → LineStrings + point nodes
│   ├─ KmlImporter.cs             your KML → waypoints; reads lon,lat,alt triples
│   └─ RouteBuilder.cs            chain by endpoint proximity, reject way/365041854,
│                                 reverse mis-digitised ways, flag the 1,171.7 m bridge
├─ Utilities/
│   ├─ GeoMath.cs                 haversine, bearing, destination, Web Mercator
│   ├─ GpsKalmanFilter.cs         constant-velocity filter on lat/lon
│   ├─ PolylineUtility.cs         closest-point, cumulative distance, interpolate to 5 m
│   └─ SafeAreaFitter.cs          notch/gesture-bar insets, re-evaluates on change
├─ Localization/
│   ├─ LocalizationService.cs     loads StreamingAssets/Localization/{code}.json
│   ├─ Loc.cs                     static T(key, args), en → raw-key fallback
│   └─ LocalizedLabel.cs          holds key, re-resolves on OnLocaleChanged
├─ Positioning/
│   ├─ LocationProvider.cs        Input.location + permissions + accuracy watchdog
│   ├─ TraceReplaySource.cs       synthetic walk along the polyline — the §08 harness
│   ├─ HybridLocalizationEngine.cs  VIO + GPS + route-snap + compass fusion
│   ├─ GeoAnchorFrame.cs          AR-local ↔ geographic transform, drift absorption
│   └─ HeadingProvider.cs         compass, GPS-course above walking speed
├─ Data/
│   ├─ LandmarkData.cs            + LandmarkType enum, normalises casing on parse
│   ├─ Waypoint.cs                lat, lon, elevation, cumulative distance, isBridged
│   ├─ SettingsModel.cs           voice, locale, units, brightness, haptics
│   └─ NavigationEvents.cs        typed event bus
├─ Database/
│   ├─ JsonDatabase.cs            loads + validates landmarks.json, bbox-checks all 41
│   ├─ LandmarkRepository.cs      query, filter, search, live distances
│   ├─ VisitedStore.cs            persisted visited set, survives app kill mid-walk
│   └─ SettingsStore.cs           PlayerPrefs-backed, change events
├─ Profile/
│   ├─ UserProfile.cs             name, age, locale, createdUtc
│   ├─ ProfileService.cs          HasProfile / Save / Clear, first-run gate
│   ├─ LoginLog.cs                append-only logins.json — the authoritative record
│   ├─ XlsxWriter.cs              minimal OOXML via ZipArchive, inlineStr cells, no deps
│   └─ ExcelLoginStore.cs         regenerates login.xlsx; Assets path in Editor,
│                                 persistentDataPath + share sheet on device
├─ Navigation/
│   ├─ NavigationSession.cs       state machine: idle/active/paused/ended
│   ├─ RouteProgressTracker.cs    the single scalar → all four screens' numbers
│   ├─ LandmarkTriggerService.cs  radius entry, dedupe, arrival events
│   ├─ ManeuverDetector.cs        bearing deltas → turn cards
│   ├─ EtaEstimator.cs            rolling pace, not a constant
│   ├─ ElevationProfile.cs        (needs altitude from KML or DEM)
│   └─ StepEstimator.cs           labelled estimate from distance
├─ AR/
│   ├─ ARSessionBootstrapper.cs   availability check, permissions, unsupported-device UI
│   ├─ GroundPlacementService.cs  the three tiers + active-tier reporting
│   ├─ DynamicArrowManager.cs     pooled chevrons along next 40 m
│   ├─ NavigationArrow.cs         per-chevron fade, amber-bridge tint
│   └─ ARAnchorService.cs         anchor lifetime, re-anchor on drift correction
├─ Audio/
│   ├─ VoiceNavigationManager.cs  clip → TTS → caption fallback chain
│   ├─ AndroidTextToSpeech.cs     JNI; isLanguageAvailable() BEFORE setting locale
│   └─ HapticService.cs           gated by the settings switch
├─ UI/Framework/
│   ├─ UITheme.cs                 the §05 tokens, one source of truth
│   ├─ UIFactory.cs               Card/Pill/Chip/Switch/Row/StatColumn/CircleButton
│   ├─ UIShapes.cs                rounded rects, rings, procedural sprites
│   ├─ UITween.cs                 fades, scale-ins, respects reduce-motion
│   ├─ IconGraphic.cs             procedural glyphs + atlas hook
│   ├─ ConfettiBurst.cs           screen 8
│   └─ ResponsiveUI.cs            canvas scaler + safe-area wiring (§07)
├─ UI/Screens/
│   ├─ UIRoot.cs                  tab host, screen lifecycle, state preservation
│   ├─ BackStackManager.cs        Android hardware back
│   ├─ UIScreen.cs                base: Build/Show/Hide/Refresh
│   ├─ BottomNavBar.cs            5 tabs, safe-area anchored
│   ├─ LoginScreen.cs             screen 0 — name, age stepper, language chips
│   ├─ ARNavigationScreen.cs      frame 1 + 11
│   ├─ MapScreen.cs               frame 2
│   ├─ LandmarksScreen.cs         frame 3
│   ├─ ProgressScreen.cs          frame 4
│   └─ SettingsScreen.cs          frame 5 + language picker + about
├─ UI/Overlays/
│   ├─ LandmarkPopup.cs           frame 6
│   ├─ TurnInstructionCard.cs     frame 7
│   ├─ ArrivalCard.cs             frame 8
│   ├─ LowGpsWarning.cs           frame 9
│   ├─ PauseResumeCard.cs         frame 10
│   └─ PermissionRationale.cs     pre-permission explainer
├─ UI/Map/
│   ├─ IBasemapSource.cs          one method: texture for tile {z,x,y}
│   ├─ DemoBasemap.cs             procedural navy + zoom-aware graticule (v1)
│   ├─ TileBasemap.cs             (reads StreamingAssets/Tiles — v1.1 swap-in)
│   ├─ MapView.cs                 Mercator grid, pinch/pan, LRU 64-texture cache
│   ├─ RouteOverlay.cs            polyline + vertex dots, amber bridge section
│   ├─ UserMarker.cs              chevron + accuracy halo + follow-me
│   └─ PoiMarkerLayer.cs          41 pins, culled + clustered by zoom
├─ Diagnostics/
│   ├─ DebugOverlay.cs            drift, GPS residual, active tier, fps, thermal
│   └─ GpsTraceRecorder.cs        1 Hz GeoJSON logger — survey fallback
└─ Editor/
    ├─ FontSetup.cs               dynamic TMP assets (+ Indic fallbacks when TTFs land)
    ├─ ImportKmlTool.cs           your KML → merged LineString in the route file
    ├─ ValidateData.cs            catches all §04 defects before they ship
    ├─ BakeElevation.cs           (only if your KML has no altitude)
    └─ ProjectSetup.cs            scene + settings scaffolding

Assets/
└─ login.xlsx                     generated — Editor Play mode only (§12)

Assets/StreamingAssets/
├─ Database/landmarks.json        41 landmarks, §04 defects fixed, localisable schema
├─ Route/alipiri_mettu.geojson    copied from Docs; your KML merges in here
├─ Localization/en.json           ~130 keys
├─ Localization/te|hi|ta|kn.json  (deferred — picker logs the choice meanwhile)
├─ Tiles/{z}/{x}/{y}.png          (deferred — demo basemap in v1)
├─ Images/Landmarks/lm01..41.jpg  (deferred — procedural category tiles in v1)
└─ Audio/{locale}/lm01..41.mp3    (deferred — TTS covers v1)

Assets/Fonts/
├─ static/GoogleSans-*.ttf        ✓ already present
└─ Noto/NotoSans{Telugu,Devanagari,Tamil,Kannada}-Regular.ttf   (deferred)
```

---

## 11 · Build phases

Ordered so everything cheap and verifiable comes before the one thing that needs a mountain.
Phases 3–9 need no AR hardware at all.

| # | Phase | Detail | Blockers | Est. |
|---|---|---|---|---|
| 01 | **Project hygiene** | `git init`. Delete `TutorialInfo/` + `Readme.asset` + empty `Docs/New folder`. Set `productName` → "Alipiri AR Navigation", real `companyName`, Android bundle id. Switch active build target to Android *now*, before any shader-touching code. | none | ~1 h |
| 02 | **AR packages + Android settings** | Install AR Foundation 6.5, ARCore XR Plugin 6.5, XR Plugin Management. Enable ARCore under XR Plug-in Management → Android. Min API 24, IL2CPP + ARM64, ARCore Required. Add CAMERA + ACCESS_FINE_LOCATION. Confirm an empty AR scene opens the camera on device. | device | ~2 h |
| 03 | **UI framework + theme** | `UITheme` with §05 tokens, `UIFactory`, `UIShapes`, `UITween`, `ResponsiveUI`, `SafeAreaFitter`. Prove one card renders correctly across all seven test resolutions before building any screen on it. | editor only | ~1 day |
| 04 | **Localisation core** | `LocalizationService` + `Loc` + `LocalizedLabel`, registered in `AppBootstrap` before any UI is built, `en.json` seeded from mockup copy. Every label goes through `Loc.T()` from the first screen — costs nothing with one locale, prevents a 130-string retrofit. `FontSetup` builds dynamic TMP assets from Google Sans; Indic fallbacks attach later with no code change. | none | ~1 day |
| 05 | **Route + landmark data** | `GeoJsonParser`, `RouteBuilder` (chain the 6 ways, reject `way/365041854`, flag `isBridged`), `GeoMath`, `PolylineUtility` incl. 5 m interpolation. Create `landmarks.json` with every §04 defect fixed and types normalised to an enum. `KmlImporter` written now, unused until your file arrives. `ValidateData` asserts 7,288.6 m total, 41 landmarks in bbox, valid JSON. | Docs data only | ~1–2 days |
| 06 | **Shell, login, Excel** | `UIRoot`, `BackStackManager`, `BottomNavBar`, five empty tabs, chrome matched to mockups, hardware back at every depth. Then `LoginScreen` as entry gate, `ProfileService`, `XlsxWriter` / `ExcelLoginStore` (§12). Verify by opening `Assets/login.xlsx` in Excel after a Play-mode run — **first demoable milestone**. | after 03, 04 | ~2 days |
| 07 | **Landmarks, Progress, Settings** | Frames 3, 4, 5 fully functional — search, all six filter chips, live distances, visited state, every switch and row from §06, language picker retranslating live, units reformatting live. Elevation chart and step counts render from data or show an honest empty state. | editor only | ~2–3 days |
| 08 | **Demo map + live position** | `IBasemapSource` + `DemoBasemap` + `MapView` + `RouteOverlay` + `UserMarker` + `PoiMarkerLayer`. Pinch, pan, recentre, 2D/3D, north all working, follow-me releasing on manual pan. Build `TraceReplaySource` here and watch the marker walk the full 7,288.6 m route in the editor. | none | ~3 days |
| 09 | **Positioning + session logic** | `LocationProvider`, `GpsKalmanFilter`, route snapping, `RouteProgressTracker`, `EtaEstimator`, `LandmarkTriggerService`, `ManeuverDetector`, `NavigationSession`. All four data-driven screens now share one scalar and cannot disagree. | trace-replay testable | ~2–3 days |
| **10** | **⚠ AR navigation on device — the hard gate** | `ARSessionBootstrapper`, `GroundPlacementService` (all three tiers), `DynamicArrowManager`, `HybridLocalizationEngine`, `GeoAnchorFrame`, frames 1, 7, 8, 9, 10, 11. Then build to a phone and **walk the first 200 steps**. Nothing downstream matters until arrows stick to real treads. Expect to spend most of this phase tuning tier thresholds outdoors, not writing new code. | **device required** | ~1 week, mostly field tuning |
| 11 | **Voice via TTS** | `VoiceNavigationManager` with clip → TTS → caption chain, speaking the `voiceText` already in Docs data — so Listen and landmark announcements are fully functional with no audio files. `AndroidTextToSpeech` calls `isLanguageAvailable` before committing to a locale, degrades to English + caption. | Docs data is enough | ~2 days |
| 12 | **Import your KML, close the gap** | Run `ImportKmlTool`, merge as real geometry, re-derive route length, clear `isBridged`, drop amber treatment. If the KML carries altitude, the elevation profile and a proper step estimate light up in the same pass. `GpsTraceRecorder` ships as a fallback. | waiting on your KML | ~1 day |
| 13 | **Content pass — deferred** | 41 photographs replacing category tiles, Noto TTFs + four translations, recorded audio replacing TTS, real tiles replacing demo basemap. Each slots into an interface that already exists — none of it is a refactor. | content, not code | as content arrives |
| 14 | **Full field test** | Walk all 7.29 km with the debug overlay recording: absorbed drift, GPS residual, landmark trigger hit rate, arrow tier distribution, plane-detection failure rate on covered sections, battery drain, thermal throttling. AR + GPS + screen for a 2–3 hour climb is brutal — expect to need a low-power mode where AR suspends and only the map runs. | the real acceptance test | 1 day |

### If time is short

Phases 1 → 3 → 4 → 5 → 6 give you the login screen writing to `login.xlsx` plus the tab shell in
about four days, entirely in the editor — the first thing worth showing anyone. Add 7 → 8 → 9 and
you have four of five screens genuinely working against real route data inside two weeks, still with
no device dependency and no content blockers.

Start Phase 2's device smoke test in parallel from day one regardless. AR is the only part that can
surprise you badly, and finding out a phone won't hold planes on those steps is information you want
in week one, not week four.

---

## 12 · Login screen and the Excel sheet

Name, age and language captured on a login page and written to
`D:\Unity Projects\AR_pages\Assets\login.xlsx`. Buildable, but two constraints shape how.

### ⚠ Constraint 1 — `Assets\login.xlsx` only exists on your PC

`Assets/` is an editor-time concept. When you build for Android, its contents are compiled into the
APK, and **the APK is read-only at runtime** — there is no `D:\Unity Projects\...` path on a phone,
and nothing can be written back into `Assets/` from a running app.

So the writer targets **both**, picking by platform:
- **Editor Play mode** → writes exactly the path you asked for, so you can open the sheet in Excel
  and watch rows appear while testing.
- **On device** → writes to `Application.persistentDataPath/login.xlsx`, and Settings gets an
  **"Export login sheet"** row that fires the Android share sheet (Drive, email, WhatsApp), plus
  `adb pull` for you.

Same code, same format, one honest branch.

### ⚠ Constraint 2 — `.xlsx` is a ZIP of XML, not a text format

Renaming a CSV to `.xlsx` makes Excel show a corruption warning and breaks on re-save, so that's
out. Unity ships no Excel writer, and the usual libraries are poor fits: **EPPlus** is no longer
freely licensed for commercial use, and **ClosedXML** drags a dependency chain that tends to break
under IL2CPP managed-code stripping.

**Recommendation: write the OOXML package directly.** `System.IO.Compression.ZipArchive` is
available in Unity's .NET Standard 2.1 profile, and a valid single-sheet workbook needs only five
small parts. ~200 lines, zero dependencies, cannot break on a package update.

| Part | Purpose |
|---|---|
| `[Content_Types].xml` | Declares workbook and worksheet MIME types |
| `_rels/.rels` | Package root → points at the workbook |
| `xl/workbook.xml` | One sheet, named **Logins** |
| `xl/_rels/workbook.xml.rels` | Workbook → worksheet relationship |
| `xl/worksheets/sheet1.xml` | The rows, as `<c t="inlineStr">` cells |

Using **inline strings** rather than a shared-string table drops a sixth part and removes all index
bookkeeping. It also closes a real hole for free: a name typed as `=HYPERLINK(...)` or `+1+1` is a
**formula-injection** payload in a CSV, but an `inlineStr` cell is typed as text and Excel will never
evaluate it.

### Append strategy

A ZIP cannot be appended to row-by-row — the sheet XML must be rewritten whole. So the authoritative
record is a plain `logins.json` in `persistentDataPath`, appended on every save and never rewritten;
the `.xlsx` is **regenerated from it** each time. That makes the Excel file a disposable export,
which means a mid-write crash, a locked file (Excel holds a lock while the sheet is open) or a
corrupted ZIP can never lose a single entry. If the file is locked, write `login (1).xlsx` and tell
the user why rather than failing silently.

### Columns

| Col | Header | Source |
|---|---|---|
| A | # | 1-based row index |
| B | Name | text input, trimmed, non-empty, 40 char cap |
| C | Age | numeric stepper + field, validated 1–120 |
| D | Language | picker, stored as both label and locale code |
| E | Saved (local) | device local time, `yyyy-MM-dd HH:mm:ss` |
| F | Saved (UTC) | ISO 8601 — the sortable one |
| G | Device | `SystemInfo.deviceModel` |
| H | App version | `Application.version` |

### Screen behaviour

- Shown on **first launch only**. A saved profile skips straight to the AR tab; Settings gets
  **Edit profile** and **Sign out** rows to get back to it.
- Visual language matched to the mockups: navy ground, gopuram mark, pill inputs, full-width blue
  primary button, inline amber error text under the offending field.
- Language taps apply `Loc.SetLocale` **immediately**, so the login screen itself retranslates under
  your finger — the most convincing possible demo that the localisation layer is real.
- Continue disabled until both fields validate; failures explain the fix ("Age must be between 1 and
  120"), never just "invalid".
- Every write is wrapped — a full disk, read-only folder or locked sheet surfaces a toast and still
  lets the user into the app. **Losing the log must never block the walk.**

### Worth being precise about what this is

There is no password, so this is **profile capture and a visit log**, not authentication — it
shouldn't be presented as securing anything. The file holds names and ages in plaintext on the
device, fine for a demo and a normal thing to log, but if this ever goes to real pilgrims that sheet
is personal data and wants a stated purpose and retention answer.

---

## 13 · Still open — none blocking

| Open item | Needed by | Why it matters |
|---|---|---|
| **Which languages in the picker?** | Phase 4 | The login screen needs a list on day one. Seeding English + Telugu + Hindi + Tamil + Kannada as *selectable and logged to Excel*, with UI text staying English until translations exist. It's one array — say if the list is wrong. |
| **Does your KML carry altitude?** | whenever it arrives | Unlocks the real elevation profile and a proper step estimate at zero extra cost (§03) |
| **What should age drive?** | Phase 7 | Currently logged and unused. Candidates: rest reminders every N steps for 60+, larger base type size, gentler pace in the ETA estimate. Any of those makes the field earn its place. |

Nothing else is required to begin. Phase 1 starts with `git init`, stripping the Unity template, and
configuring the Android target.

---

*Route figures computed from `Assets/Docs/alipiri_mettu.geojson` via haversine · 165 vertices ·
7 ways · 7,288.6 m total.*
*Route data © OpenStreetMap contributors, ODbL — attribution required in the shipped app.*
