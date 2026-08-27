# UI Plan — Alipiri AR Navigation

**Mockup comparison and redesign plan.** Written 17 August 2026, against `UI.png` (the 11-screen
mockup) and the code in `Assets/Scripts/`. Companion to `Docs/NewPlan.md` (Rev 3), which stays the
plan-of-record for the app as a whole; this document covers the UI question only.

Every claim below was verified by reading the file named. Counts at time of writing: **77** C#
files across `Assets/Scripts/`, **8** overlay screens, **40** landmark records, **0** image assets.

---

## §00 · Headline

| | |
|---|---|
| **14** | claims in the supplied review that describe features **already built** |
| **6** | genuine gaps confirmed in the code |
| **4** | of those 6 are **content**, not engineering |

The review that accompanied the mockup was substantially inaccurate. Nine items were marked *high
priority*, four of them as entirely missing screens — all four exist as real files with real
trigger logic. Rebuilding them would have thrown away working code.

**Structurally, very little has to change.** All eleven mockup screens exist, are wired, and are
reachable. What separates the build from the mockup is overwhelmingly **photographic content** —
satellite ground texture and forty landmark thumbnails. The remaining distance cannot be closed by
writing more C#.

---

## §01 · The corrected record

### Marked "missing" — all four exist and are called

| Claim | Reality |
|---|---|
| "Landmark Popup (Missing)" | `UI/Overlays/LandmarkPopup.cs` — 210 lines, referenced from five call sites including the Map screen and the landmark list |
| "Arrival Screen (Missing)" | `UI/Overlays/ArrivalCard.cs` — 99 lines, shown from `ARNavigationScreen.cs:630`. `ConfettiBurst.cs` exists too |
| "GPS Weak Screen (Missing)" | `UI/Overlays/LowGpsWarning.cs` — 89 lines, shown from `ARNavigationScreen.cs:653` |
| "Pause Screen (Missing)" | `UI/Overlays/PauseResumeCard.cs` — 144 lines, shown from `ARNavigationScreen.cs:594` |

### Marked as absent features — also already built

| Claim | Reality |
|---|---|
| "No category filters. Need chips: All / Temple / Water / Status / Steps" | Six chips already defined at `LandmarksScreen.cs:22–29` — All, Temple, Water, Statue, Steps, Other |
| "Search: floating button → need header icon" | A search row with a real `TMP_InputField` is built at `LandmarksScreen.cs:60`. Placement is a tweak, not a build |
| "No compass widget" | Compass sits in the AR left rail at `ARNavigationScreen.cs:369` |
| "Bottom panel shows Distance / Next / ETA only — needs a progress bar" | Progress bar is already there: `_progressFill`, `ARNavigationScreen.cs:525–527` |
| "Landmark cards: only icon" | Each card already lays out a 136 px rounded thumbnail slot, sequence badge, title, description, distance and a completion status ring. The slot renders a tinted icon **because no photo files exist to put in it** |
| "Turn instruction card" | `UI/Overlays/TurnInstructionCard.cs` — 128 lines, shown from `ARNavigationScreen.cs:694` |

### "General UI improvements" — the token system already covers these

| Claim | Reality |
|---|---|
| "Corner radius inconsistent — target ~18–20 px" | `UITheme.RadiusCard = 26`, plus stadium tokens for chips and pills. One value, one place. Changing it is a one-line edit if 26 reads too round |
| "Typography: mostly same size — need hierarchy" | A five-step scale is already defined: Caption 23, Label 27, Body 34, Title 44, Display 60 |
| "Colors: use a consistent palette" | `UITheme.cs` already defines exactly the palette described — dark navy ground, translucent glass cards, blue accent, green success, gold temple, white/grey text pair |
| "Elevation: shows 'No data' — need an actual chart" | The chart renderer exists at `ProgressScreen.cs:152–177`. It shows the empty state because the route data genuinely has no elevation — see §02 |

---

## §02 · The gaps that are real

Six items survive the check. Two need engineering. Four need someone to produce content or source
imagery — no amount of code closes them.

| Gap | Nature | Detail |
|---|---|---|
| Satellite imagery under the map | **Content + decision** | Real, and the biggest single visual difference. ⚠ Nuance the review missed: the mockup shows **satellite** imagery, but the tile pipeline built into `TileBasemap.cs` targets **OpenStreetMap street-style** tiles — a different look and a different licence. Standard OSM tiles will not produce that green aerial texture. Satellite basemaps essentially all come from commercial providers with terms that restrict offline bundling. Decide explicitly before more work goes in |
| 40 landmark photographs | **Content** | Zero image files exist in the project. The mockup's warmth comes almost entirely from photography — every list row, the popup, the map cards. The slots are built and correctly sized; they are waiting on files. Highest-leverage item on the list, and it needs a camera, not a compiler |
| A `description` field in `landmarks.json` | **Content** | The data has `voiceText` only, so display copy and spoken copy are forced to be the same sentence. Already tracked as defect **D6** in `NewPlan.md`. Adding the field is trivial; writing 40 good descriptions is the actual work |
| Real elevation data | ⚠ **Blocked** | Every one of the 657 points in `Docs/Alipir.kml` carries elevation `0` — Google My Maps' placeholder, not surveyed altitude. Until real altitude arrives, the honest empty state is correct behaviour and the chart stays blank. Guard this before importing, or the app will draw a convincing flat line that is pure fiction |
| Night mode | **Build** | Genuinely absent — zero references anywhere in the codebase. Needs a darkened camera feed, dimmed HUD chrome and brighter chevrons, driven off either ambient light or local sunset time |
| Proportions, spacing, shadow depth | **Tune** | The fair part of the review. Cards do sit tighter than the mockup, some controls are undersized, and elevation shadows are sparse. But these are numeric adjustments to existing components, not redesigns — and several route through single shared tokens |

The mockup's premium feel comes from three things, in order of impact: **photography, generous
spacing, depth**. The components already exist. Supplying real images would move the app closer to
that reference than every other item on the review list combined.

---

## §03 · Plan

Ordered by what unblocks what, and by leverage per hour. Phases 1 and 2 are implemented (17 August
2026); phase 3 needs a decision; phases 4 and 5 need content.

| # | Phase | Cost | Status |
|---|---|---|---|
| **1** | **Guard the elevation import** | ~15 min | ✅ Done |
| **2** | **Proportion and depth pass** | ~1 day | ✅ Done |
| **3** | ⚠ **Decide the basemap, then bake it** | decision + ~20 min | Blocked on decision |
| **4** | **Photograph the route** | one field trip | Blocked on content |
| **5** | **Write the descriptions, then night mode** | ~2 days + writing | Blocked on content (descriptions); night mode not started |

### Phase 1 · Guard the elevation import — ✅ done

`KmlImporter.HasAltitude()` (`Assets/Scripts/Route/KmlImporter.cs`) now requires at least two
*distinct* elevation values across the whole route before it reports altitude as present, instead
of treating any non-`NaN` number as real data. Google My Maps' placeholder — `0` on every
coordinate — used to pass that old check and would have fabricated a flat elevation profile; it now
correctly reads as "no data" and the Progress screen's honest empty state stays in effect until real
survey altitude arrives.

### Phase 2 · Proportion and depth pass — ✅ done

- **Shadows** — new `UIFactory.CircleShadow` / `UIFactory.RoundedShadow` helpers (same low-alpha,
  offset-down idiom `PoiMarkerLayer` already used for map pins — procedural sprites have no blur,
  so softness comes from alpha + offset only). Applied to every floating circular button (AR HUD
  hamburger/speaker/left rail, Map right rail, Landmarks search, Settings back), the AR landmark
  pill, the AR bottom stat card, the Map top pill and bottom "Next" card, the Landmarks list cards,
  and the Progress screen's stat and elevation cards. Left out: the four full-screen popups
  (`LandmarkPopup`, `ArrivalCard`, `TurnInstructionCard`, `LowGpsWarning`, `PauseResumeCard`) — their
  height is driven by `ContentSizeFitter` at runtime, so a same-size shadow can't be sized to match
  ahead of time without a larger structural change, and the existing 55–75%-alpha scrim already
  separates them from the background clearly.
- **Spacing** — Landmarks list inter-card gap widened (`SpaceM` → `SpaceM + SpaceXS`, 20→26px);
  Progress screen's stat card padding opened up (28/8/16 → 32/16/22).
- **Settings enlarged** — back button 72→88px, header 96→108px, row height 84→104px (scoped to this
  screen via local constants, not the shared `MinTouchTarget`), switch track 92×52→108×60 with a
  proportionally larger 48px knob.
  `UIFactory.SettingsRow` gained an optional `rowHeight` parameter (default unchanged) so this
  didn't have to touch its other caller, `NavigationDrawer`.
- **Progress ring** — stroke 28→36px, percentage label 60→76px (its own literal, not the shared
  `Display` step, since nothing else on the app needs to move with this screen's one hero number).
- **Chevron alignment** — checked, not changed: `SettingsRow` already places the chevron as a fixed
  28px-wide trailing element on every row, so it was already flush to a consistent right edge.

Not touched: the four popups' own internal padding (already generous) and the AR/Map top-pill icon
sizes (already matched to the mockup).

### Phase 3 · ⚠ Decide the basemap, then bake it — decision + ~20 min

`TileBasemap.cs` is built and wired into `MapScreen`; only the imagery is missing. Street-style OSM
tiles are ready to go — run `Tools\FetchMapTiles.ps1` locally, then rebuild. The script rate-limits
itself, sends a descriptive User-Agent, is resumable, and aborts on detecting OSM's block-notice
tile (exactly 6,987 bytes, served with HTTP 200 — the failure mode that silently filled the last
tile bake with "403 Access blocked" images).

If the satellite look from the mockup is what's wanted instead, that needs working through provider
terms first — offline bundling is the sticking point.

### Phase 4 · Photograph the route — one field trip

Forty landmark photographs, roughly square, shot on the walk up. The single highest-impact item on
the entire list. Once the files exist, wiring them into the thumbnail slot, the popup and the map
cards is a short task — the layouts are already built around them.

### Phase 5 · Write the descriptions, then night mode — ~2 days + writing

Add the `description` field to all forty entries so display copy stops doubling as narration
(`JsonDatabase.cs:92` already falls back to `voiceText`, so the field can land incrementally). Then
build night mode as the last piece — it is the only genuinely missing feature, and it is easier to
tune once the rest of the palette has settled.

---

## §04 · Related documents

- `Docs/NewPlan.md` — Rev 3, plan-of-record for the app. D6 (description field) and the phase-status
  table overlap with this document.
- `PLAN.md` — Rev 2, superseded, kept as historical record.
- Published version of this audit:
  https://claude.ai/code/artifact/1e9b2a02-32c1-40be-9155-b2fc937b4176
