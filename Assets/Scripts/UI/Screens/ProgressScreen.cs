using System.Globalization;
using System.Linq;
using AlipiriAR.Core;
using AlipiriAR.Data;
using AlipiriAR.Database;
using AlipiriAR.Localization;
using AlipiriAR.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlipiriAR.UI
{
    /// <summary>
    /// Frame 4 — completion ring, 2x2 metrics grid, elevation profile, achievement badges
    /// (Docs/Images/New UI redesign). Completed distance is derived from the farthest landmark
    /// VisitedStore knows about (PLAN.md §06's manual visited toggle) rather than live GPS, since
    /// LocationProvider doesn't exist until Scene 5 — once it does, RouteProgressTracker becomes
    /// the source of truth here without changing the layout. ETA, Calories and Steps Climbed are
    /// estimates (PLAN.md §04), never presented as measured.
    /// </summary>
    public class ProgressScreen : UIScreen
    {
        private Image _ringFill;
        private TMP_Text _percentLabel;
        private TMP_Text _remainingLabel;
        private TMP_Text _etaLabel;
        private TMP_Text _caloriesLabel;
        private TMP_Text _elevationLabel;
        private RectTransform _elevationChartRt;
        private TMP_Text _achievementWaterLabel;
        private TMP_Text _achievementTempleLabel;
        private TMP_Text _achievementStepsLabel;
        private TMP_Text _completedCaptionLabel;
        private float _lastFraction;

        protected override void Build(RectTransform root)
        {
            UIFactory.Panel(root, UITheme.Ground);
            var content = UIFactory.VerticalScroll(root, UITheme.SpaceM,
                new RectOffset((int)UITheme.SpaceM, (int)UITheme.SpaceM, (int)UITheme.SpaceM, (int)UITheme.SpaceL));

            BuildHeader(content);
            BuildRingSection(content);
            BuildMetricsGrid(content);
            BuildElevationCard(content);
            BuildAchievementsRow(content);

            RefreshStats();
        }

        protected override void OnShown() => RefreshStats();

        private void BuildHeader(Transform parent)
        {
            var headerRt = UIFactory.CreateRect("Header", parent);
            headerRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 80f;
            var hlg = headerRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = UITheme.SpaceS;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandHeight = true;

            var markRt = UIFactory.CreateRect("Mark", headerRt);
            markRt.gameObject.AddComponent<LayoutElement>().preferredWidth = 56f;
            UIFactory.CenteredIcon(markRt, IconType.Gopuram, 48f, UITheme.Gold);

            var titleRt = UIFactory.CreateRect("Title", headerRt);
            titleRt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var titleLabel = UIFactory.Label(titleRt, string.Empty, UITheme.TitleFontSize, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            LocalizedLabel.Bind(titleLabel, "progress.title");
        }

        private void BuildRingSection(Transform parent)
        {
            var wrapRt = UIFactory.CreateRect("RingWrap", parent);
            wrapRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 420f;

            // Ring stroke and the percentage type both read heavier in the mockup than the
            // original 28px/DisplayFontSize pairing — this is the screen's single hero number, so
            // it gets its own literal rather than stretching the shared Display step for everyone
            // else (Docs/UIplan.md §03 Phase 2).
            const float ringThickness = 36f;
            const float percentFontSize = 76f;

            _ringFill = UIFactory.ProgressRing(wrapRt, 380f, ringThickness, 0f, out var container, UITheme.Accent);

            var textRt = UIFactory.CreateRect("Text", container);
            UIFactory.StretchFill(textRt, 40f);
            var vlg = textRt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth = true;
            vlg.spacing = 4f;

            var pctRt = UIFactory.CreateRect("Percent", textRt);
            pctRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 88f;
            _percentLabel = UIFactory.Label(pctRt, "0%", percentFontSize, FontStyles.Bold, TextAlignmentOptions.Center);

            var capRt = UIFactory.CreateRect("Caption", textRt);
            capRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;
            _completedCaptionLabel = UIFactory.Label(capRt, string.Empty, UITheme.BodyFontSize, FontStyles.Normal, TextAlignmentOptions.Center, UITheme.TextSecondary);
            _completedCaptionLabel.enableWordWrapping = true;
        }

        /// <summary>Redesign's 2x2 metrics grid (Docs/Images/New UI #4) — replaces the earlier
        /// 5-row stat list. Total/Completed distance dropped from here since the ring above
        /// already states completed-of-total; Calories/Elevation are new (CalorieEstimate mirrors
        /// EtaEstimate's own "honest placeholder pace" pattern — Elevation stays an em dash until
        /// real elevation data exists, same honesty as the chart below).</summary>
        private void BuildMetricsGrid(Transform parent)
        {
            var gridRt = UIFactory.CreateRect("MetricsGrid", parent);
            gridRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 220f;
            var grid = gridRt.gameObject.AddComponent<GridLayoutGroup>();
            grid.spacing = new Vector2(UITheme.SpaceS, UITheme.SpaceS);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperLeft;
            // cellSize is fixed pixels, not stretch-to-fill — recomputed against the reference
            // width the rest of this screen's literals assume (1080), same two-column math as
            // LandmarksScreen's chip row.
            const float refWidth = 1080f - (int)UITheme.SpaceM * 2;
            float cellWidth = (refWidth - UITheme.SpaceS) / 2f;
            grid.cellSize = new Vector2(cellWidth, 104f);

            _remainingLabel = AddMetricTile(gridRt, "progress.remaining");
            _etaLabel = AddMetricTile(gridRt, "progress.eta");
            _caloriesLabel = AddMetricTile(gridRt, "progress.calories");
            _elevationLabel = AddMetricTile(gridRt, "progress.elevation_gain");
        }

        private static TMP_Text AddMetricTile(Transform parent, string labelKey)
        {
            var tile = UIFactory.CreateRect($"Tile_{labelKey}", parent);
            var img = tile.gameObject.AddComponent<Image>();
            img.sprite = UIShapes.RoundedRect(20);
            img.type = Image.Type.Sliced;
            img.color = UITheme.Surface;

            var vlg = tile.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleLeft;
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.padding = new RectOffset(24, 24, 16, 16);
            vlg.spacing = 4f;

            var labelRt = UIFactory.CreateRect("Label", tile);
            labelRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;
            var label = UIFactory.Label(labelRt, string.Empty, UITheme.LabelFontSize, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, UITheme.TextSecondary);
            LocalizedLabel.Bind(label, labelKey);

            var valueRt = UIFactory.CreateRect("Value", tile);
            valueRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
            return UIFactory.Label(valueRt, string.Empty, UITheme.BodyFontSize, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        }

        /// <summary>Redesign's devotional achievement badges (Docs/Images/New UI #4) — derived
        /// from the same VisitedStore/LandmarkType data LandmarksScreen's filter chips already use,
        /// not new tracked state.</summary>
        private void BuildAchievementsRow(Transform parent)
        {
            var titleRt = UIFactory.CreateRect("AchievementsTitle", parent);
            titleRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
            var titleLabel = UIFactory.Label(titleRt, string.Empty, UITheme.BodyFontSize, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            LocalizedLabel.Bind(titleLabel, "progress.achievements_title");

            var rowRt = UIFactory.CreateRect("AchievementsRow", parent);
            rowRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 140f;
            var hlg = rowRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = UITheme.SpaceS;
            hlg.childForceExpandWidth = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            _achievementWaterLabel = AddAchievementBadge(rowRt, IconType.Droplet, "progress.achievement_water");
            _achievementTempleLabel = AddAchievementBadge(rowRt, IconType.Gopuram, "progress.achievement_temple");
            _achievementStepsLabel = AddAchievementBadge(rowRt, IconType.Steps, "progress.achievement_steps");
        }

        private static TMP_Text AddAchievementBadge(Transform parent, IconType icon, string captionKey)
        {
            var tile = UIFactory.CreateRect($"Badge_{captionKey}", parent);
            var img = tile.gameObject.AddComponent<Image>();
            img.sprite = UIShapes.RoundedRect(20);
            img.type = Image.Type.Sliced;
            img.color = UITheme.Surface;

            var vlg = tile.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.padding = new RectOffset(8, 8, 12, 12);
            vlg.spacing = 4f;

            var iconRt = UIFactory.CreateRect("Icon", tile);
            iconRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            UIFactory.CenteredIcon(iconRt, icon, 32f, UITheme.Gold);

            var valueRt = UIFactory.CreateRect("Value", tile);
            valueRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            var value = UIFactory.Label(valueRt, string.Empty, UITheme.BodyFontSize, FontStyles.Bold, TextAlignmentOptions.Center);

            var captionRt = UIFactory.CreateRect("Caption", tile);
            captionRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;
            var caption = UIFactory.Label(captionRt, string.Empty, UITheme.CaptionFontSize, FontStyles.Normal, TextAlignmentOptions.Center, UITheme.TextSecondary);
            LocalizedLabel.Bind(caption, captionKey);

            return value;
        }

        private void BuildElevationCard(Transform parent)
        {
            var wrapRt = UIFactory.CreateRect("ElevationCardWrap", parent);
            wrapRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 400f;
            UIFactory.RoundedShadow(wrapRt, UITheme.RadiusCard);
            var card = UIFactory.Card(wrapRt, UITheme.Surface);
            var vlg = card.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.padding = new RectOffset(28, 28, 24, 24);
            vlg.spacing = UITheme.SpaceS;

            var titleRt = UIFactory.CreateRect("Title", card.transform);
            titleRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
            var titleLabel = UIFactory.Label(titleRt, string.Empty, UITheme.BodyFontSize, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            LocalizedLabel.Bind(titleLabel, "progress.elevation_profile");

            _elevationChartRt = UIFactory.CreateRect("Chart", card.transform);
            _elevationChartRt.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

            RenderElevationChart();
        }

        private void RenderElevationChart()
        {
            var db = ServiceLocator.Get<JsonDatabase>();
            bool hasElevation = db.Route.Waypoints.Count > 0 && db.Route.Waypoints.Any(w => w.HasElevation);

            // No source in Assets/Docs carries elevation (PLAN.md §01/§04) — the honest empty
            // state below is the only branch that runs today. A future KML import with real
            // elevation data would draw an area chart here; not built now against data that
            // doesn't exist, so it can't be verified (see project guidance on half-finished work).
            if (!hasElevation)
            {
                var label = UIFactory.Label(_elevationChartRt, string.Empty, UITheme.BodyFontSize, FontStyles.Normal, TextAlignmentOptions.Center, UITheme.TextTertiary);
                LocalizedLabel.Bind(label, "progress.elevation_unavailable");
            }
        }

        private void RefreshStats()
        {
            var db = ServiceLocator.Get<JsonDatabase>();
            var visited = VisitedStore.Resolve();

            double totalMeters = db.Route.TotalDistanceMeters;
            double completedMeters = 0.0;
            foreach (var lm in db.Landmarks)
            {
                if (visited.IsVisited(lm.Id) && lm.CumulativeDistanceMeters > completedMeters)
                    completedMeters = lm.CumulativeDistanceMeters;
            }
            completedMeters = System.Math.Min(completedMeters, totalMeters);
            double remainingMeters = System.Math.Max(totalMeters - completedMeters, 0.0);
            float fraction = totalMeters > 0 ? (float)(completedMeters / totalMeters) : 0f;

            _remainingLabel.text = DistanceFormatter.FormatMeters(remainingMeters);

            int etaMinutes = EtaEstimate.Minutes(remainingMeters);
            _etaLabel.text = Loc.T("progress.eta_minutes_format", etaMinutes);

            _caloriesLabel.text = Loc.T("progress.calories_kcal_format", CalorieEstimate.Kcal(completedMeters));

            // Same honesty as RenderElevationChart's own empty state below — no source in
            // Assets/Docs carries real elevation (PLAN.md §01/§04), so this stays an em dash
            // rather than a fabricated number until that data exists.
            bool hasElevation = db.Route.Waypoints.Count > 0 && db.Route.Waypoints.Any(w => w.HasElevation);
            _elevationLabel.text = hasElevation
                ? Loc.T("progress.elevation_gain_format", 0)
                : "—";

            int steps = Mathf.RoundToInt(db.Route.TotalStepsEstimate * fraction);
            _achievementStepsLabel.text = "~" + steps.ToString("N0", CultureInfo.InvariantCulture);

            int waterVisited = 0, templeVisited = 0;
            foreach (var lm in db.Landmarks)
            {
                if (!visited.IsVisited(lm.Id)) continue;
                if (lm.Type == LandmarkType.WaterPoint) waterVisited++;
                else if (lm.Type == LandmarkType.Temple) templeVisited++;
            }
            _achievementWaterLabel.text = waterVisited.ToString(CultureInfo.InvariantCulture);
            _achievementTempleLabel.text = templeVisited.ToString(CultureInfo.InvariantCulture);

            _percentLabel.text = Mathf.RoundToInt(fraction * 100f) + "%";
            _completedCaptionLabel.text = Loc.T("progress.completed_of_format",
                DistanceFormatter.FormatMeters(completedMeters), DistanceFormatter.FormatMeters(totalMeters));
            UITween.SlideProgress(v => _ringFill.fillAmount = v, _lastFraction, fraction, 0.5f);
            _lastFraction = fraction;
        }
    }
}
