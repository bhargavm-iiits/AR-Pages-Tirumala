using System;

namespace AlipiriAR.Positioning
{
    /// <summary>Which source produced a PositionMeasurement — lets a consumer apply
    /// source-specific handling (Docs/update1.md §03: "fixes F-04: lets trustExactly apply
    /// correctly").</summary>
    public enum SourceKind { Gps, StepCounter, Barometer, ImageAnchor, ManualAnchor, TraceReplay, Geospatial }

    /// <summary>The one interface every positioning input implements — §03. A GPS fix, a
    /// step-counter delta, a barometric reading and an image-target hit are all just
    /// PositionMeasurements to whatever's driving AlongTrackEstimator.</summary>
    public readonly struct PositionMeasurement
    {
        /// <summary>Along-track metres from the route start.</summary>
        public readonly double S;

        /// <summary>1-sigma uncertainty on S, in metres.</summary>
        public readonly float Sigma;

        /// <summary>Null for GPS — it never gives trustworthy heading (§03).</summary>
        public readonly float? HeadingDeg;

        public readonly float? HeadingSigma;

        public readonly double Timestamp;

        public readonly SourceKind Provenance;

        public PositionMeasurement(double s, float sigma, float? headingDeg, float? headingSigma, double timestamp, SourceKind provenance)
        {
            S = s;
            Sigma = sigma;
            HeadingDeg = headingDeg;
            HeadingSigma = headingSigma;
            Timestamp = timestamp;
            Provenance = provenance;
        }
    }

    /// <summary>A along-track measurement source, owned and polled/pushed into
    /// PositionFusionService. Sources register conditionally on their own hardware/data
    /// availability (§04 failure matrix: "Sensor absent — never assume a sensor exists") —
    /// IsAvailable reflects that, and PositionFusionService only wires up sources that report
    /// true.</summary>
    public interface IPositionSource
    {
        SourceKind Kind { get; }
        bool IsAvailable { get; }
        event Action<PositionMeasurement> OnMeasurement;
        void Start();
        void Stop();
    }
}
