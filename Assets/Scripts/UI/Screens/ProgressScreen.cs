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
    /// Frame 4 — completion ring, five stat rows, elevation profile. Completed distance is
    /// derived from the farthest landmark VisitedStore knows about (PLAN.md §06's manual
    /// visited toggle) rather than live GPS, since LocationProvider doesn't exist until Scene 5 —
    /// once it does, RouteProgressTracker becomes the source of truth here without changing the
    /// row layout. ETA and Steps Climbed are estimates (PLAN.md §04), never presented as measured.
    /// </summary>
    public class ProgressScreen : UIScreen
    {
        private Image _ringFill;
        private TMP_Text _percentLabel;
        private TMP_Text _totalLabel;
        private TMP_Text _completedLabel;
        private TMP_Text _remainingLabel;
        private TMP_Text _etaLabel;
        private TMP_Text _stepsLabel;
        private RectTransform _elevationChartRt;
        private float _lastFraction;

        protected override void Build(RectTransform root)
        {
            UIFactory.Panel(root, UITheme.Ground);
            var content = UIFactory.VerticalScroll(root, UITheme.SpaceM,
                new RectOffset((int)UITheme.SpaceM, (int)UITheme.SpaceM, (int)UITheme.SpaceM, (int)UITheme.SpaceL));

            BuildHeader(content);
            BuildRingSection(content);
            BuildStatCard(content);
            BuildElevationCard(content);

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

            _ringFill = UIFactory.ProgressRing(wrapRt, 380f, ringThickness, 0f, out var container, UITheme.Success);

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
            capRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
            var capLabel = UIFactory.Label(capRt, string.Empty, UITheme.BodyFontSize, FontStyles.Normal, TextAlignmentOptions.Center, UITheme.TextSecondary);
            LocalizedLabel.Bind(capLabel, "progress.completed");
        }

        private void BuildStatCard(Transform parent)
        {
            var wrapRt = UIFactory.CreateRect("StatCardWrap", parent);
            wrapRt.gameObject.AddComponent<LayoutElement>().minHeight = 380f;
            UIFactory.RoundedShadow(wrapRt, UITheme.RadiusCard);
            var card = UIFactory.Card(wrapRt, UITheme.Surface);
            var vlg = card.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.padding = new RectOffset(32, 32, 16, 22);

            _totalLabel = AddStatRow(card.transform, "progress.total_distance");
            AddSeparator(card.transform);
            _completedLabel = AddStatRow(card.transform, "progress.completed_distance");
            AddSeparator(card.transform);
            _remainingLabel = AddStatRow(card.transform, "progress.remaining");
            AddSeparator(card.transform);
            _etaLabel = AddStatRow(card.transform, "progress.eta");
            AddSeparator(card.transform);
            _stepsLabel = AddStatRow(card.transform, "progress.steps_climbed");

            var noteRt = UIFactory.CreateRect("Note", card.transform);
            noteRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;
            var noteLabel = UIFactory.Label(noteRt, string.Empty, UITheme.CaptionFontSize, FontStyles.Italic, TextAlignmentOptions.MidlineRight, UITheme.TextTertiary);
            LocalizedLabel.Bind(noteLabel, "progress.steps_estimate_note");
        }

        private TMP_Text AddStatRow(Transform parent, string labelKey)
        {
            var row = UIFactory.CreateRect($"Row_{labelKey}", parent);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 72f;
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandHeight = true;

            var labelRt = UIFactory.CreateRect("Label", row);
            labelRt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var label = UIFactory.Label(labelRt, string.Empty, UITheme.BodyFontSize, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, UITheme.TextSecondary);
            LocalizedLabel.Bind(label, labelKey);

            var valueRt = UIFactory.CreateRect("Value", row);
            valueRt.gameObject.AddComponent<LayoutElement>().preferredWidth = 240f;
            return UIFactory.Label(valueRt, string.Empty, UITheme.BodyFontSize, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
        }

        private static void AddSeparator(Transform parent)
        {
            var rt = UIFactory.CreateRect("Sep", parent);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 2f;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = UITheme.Rule;
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

            _totalLabel.text = DistanceFormatter.FormatMeters(totalMeters);
            _completedLabel.text = DistanceFormatter.FormatMeters(completedMeters);
            _remainingLabel.text = DistanceFormatter.FormatMeters(remainingMeters);

            int etaMinutes = EtaEstimate.Minutes(remainingMeters);
            _etaLabel.text = Loc.T("progress.eta_minutes_format", etaMinutes);

            int steps = Mathf.RoundToInt(db.Route.TotalStepsEstimate * fraction);
            _stepsLabel.text = "~" + steps.ToString("N0", CultureInfo.InvariantCulture);

            _percentLabel.text = Mathf.RoundToInt(fraction * 100f) + "%";
            UITween.SlideProgress(v => _ringFill.fillAmount = v, _lastFraction, fraction, 0.5f);
            _lastFraction = fraction;
        }
    }
}
