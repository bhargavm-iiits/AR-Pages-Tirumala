using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AlipiriAR.AR;
using AlipiriAR.Positioning;
using UnityEngine;

namespace AlipiriAR.Diagnostics
{
    /// <summary>Field-test instrument (NewPlan.md §04 Phase B): once started, samples position
    /// and diagnostics once a second and writes them to disk — a live GeoJSON trace plus a
    /// diagnostics CSV. Two uses: tuning HybridLocalizationEngine's outlier-reject thresholds
    /// against what a real walk actually looked like, and — if no surveyed KML ever arrives —
    /// serving as the survey for the 1,171.7 m route gap itself (§04 Phase F), since it can be
    /// walked once with this running and the trace merged straight into alipiri_mettu.geojson.
    /// Independent of DebugOverlay's on-screen readout; DebugOverlay only starts/stops it and
    /// shows its live sample count.</summary>
    public class GpsTraceRecorder
    {
        private const float SampleIntervalSeconds = 1f;

        private readonly LocationProvider _location;
        private readonly NavigationSession _session;
        private ARSessionBootstrapper _bootstrapper;
        private HybridLocalizationEngine _localization;
        private GroundPlacementService _placement;

        private readonly List<(double lat, double lon)> _points = new();
        private StreamWriter _csv;
        private float _elapsed;
        private float _sinceLastSample;
        private double _lastLat, _lastLon;
        private float _lastAccuracy;
        private bool _hasFix;

        public bool IsRecording { get; private set; }
        public int SampleCount => _points.Count;
        public string GeoJsonPath { get; private set; }
        public string CsvPath { get; private set; }

        public GpsTraceRecorder(LocationProvider location, NavigationSession session)
        {
            _location = location;
            _session = session;
        }

        public void AttachBootstrapper(ARSessionBootstrapper bootstrapper) => _bootstrapper = bootstrapper;

        public void AttachAr(ARSessionBootstrapper bootstrapper, HybridLocalizationEngine localization, GroundPlacementService placement)
        {
            _bootstrapper = bootstrapper;
            _localization = localization;
            _placement = placement;
        }

        public void Start()
        {
            if (IsRecording) return;

            string dir = Path.Combine(Application.persistentDataPath, "Traces");
            Directory.CreateDirectory(dir);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            GeoJsonPath = Path.Combine(dir, $"{stamp}_gps.geojson");
            CsvPath = Path.Combine(dir, $"{stamp}_diagnostics.csv");

            _points.Clear();
            _elapsed = 0f;
            _sinceLastSample = 0f;
            _hasFix = false;

            _csv = new StreamWriter(CsvPath, false, Encoding.UTF8);
            _csv.WriteLine("elapsedSeconds,lat,lon,accuracyM,gpsResidualM,consecutiveGpsRejections,groundTier,arState,routeDistanceM,lateralOffsetM");

            _location.OnFixFiltered += OnFix;
            IsRecording = true;

            Debug.Log($"[GpsTraceRecorder] Recording started — {GeoJsonPath}");
        }

        public void Stop()
        {
            if (!IsRecording) return;

            _location.OnFixFiltered -= OnFix;
            IsRecording = false;

            _csv?.Flush();
            _csv?.Dispose();
            _csv = null;

            // Rewritten fully on every sample already (see WriteGeoJson) — this final call just
            // covers the case where Stop() is reached with a partial second buffered.
            WriteGeoJson();

            Debug.Log($"[GpsTraceRecorder] Recording stopped — {SampleCount} samples, {GeoJsonPath}");
        }

        private void OnFix(double lat, double lon, float headingDeg, float accuracyMeters)
        {
            _lastLat = lat;
            _lastLon = lon;
            _lastAccuracy = accuracyMeters;
            _hasFix = true;
        }

        /// <summary>Call every frame while the app is running — accumulates toward the 1 Hz
        /// sample interval. Safe to call whether or not recording is active.</summary>
        public void Tick(float deltaTime)
        {
            if (!IsRecording) return;

            _elapsed += deltaTime;
            _sinceLastSample += deltaTime;
            if (_sinceLastSample < SampleIntervalSeconds || !_hasFix) return;
            _sinceLastSample = 0f;

            _points.Add((_lastLat, _lastLon));
            WriteGeoJson();
            WriteCsvRow();
        }

        private void WriteCsvRow()
        {
            float residual = _localization?.LastResidualMeters ?? 0f;
            int rejections = _localization?.ConsecutiveGpsRejections ?? 0;
            string tier = _placement != null ? _placement.LastTier.ToString() : "—";
            string arState = _bootstrapper != null ? _bootstrapper.State.ToString() : "—";
            double routeDistance = _session?.Progress.CumulativeDistanceMeters ?? 0.0;
            double lateral = _session?.Progress.LateralDistanceMeters ?? 0.0;

            _csv.WriteLine(string.Join(",",
                _elapsed.ToString("F1", CultureInfo.InvariantCulture),
                _lastLat.ToString("F7", CultureInfo.InvariantCulture),
                _lastLon.ToString("F7", CultureInfo.InvariantCulture),
                _lastAccuracy.ToString("F1", CultureInfo.InvariantCulture),
                residual.ToString("F1", CultureInfo.InvariantCulture),
                rejections.ToString(CultureInfo.InvariantCulture),
                tier,
                arState,
                routeDistance.ToString("F1", CultureInfo.InvariantCulture),
                lateral.ToString("F1", CultureInfo.InvariantCulture)));

            // Flushed every row, not buffered until Stop() — a crash mid-walk should still leave
            // every second recorded so far, not lose the whole session (same reasoning as the
            // full GeoJSON rewrite below).
            _csv.Flush();
        }

        /// <summary>Rewrites the whole file every sample rather than appending a growing
        /// FeatureCollection — means a crash mid-walk still leaves a well-formed, immediately
        /// usable GeoJSON trace instead of a truncated, unparseable one.</summary>
        private void WriteGeoJson()
        {
            var sb = new StringBuilder(64 + _points.Count * 24);
            sb.Append("{\"type\":\"FeatureCollection\",\"features\":[{\"type\":\"Feature\",\"properties\":{},\"geometry\":{\"type\":\"LineString\",\"coordinates\":[");
            for (int i = 0; i < _points.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('[')
                  .Append(_points[i].lon.ToString("F7", CultureInfo.InvariantCulture)).Append(',')
                  .Append(_points[i].lat.ToString("F7", CultureInfo.InvariantCulture))
                  .Append(']');
            }
            sb.Append("]}}]}");
            File.WriteAllText(GeoJsonPath, sb.ToString());
        }
    }
}
