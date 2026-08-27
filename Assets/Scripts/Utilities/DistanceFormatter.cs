using System.Globalization;
using AlipiriAR.Data;

namespace AlipiriAR.Utilities
{
    /// <summary>Reads SettingsStore.UnitsMetric directly (same static-facade pattern as Loc.T)
    /// so every call site reformats without needing the setting threaded through it.</summary>
    public static class DistanceFormatter
    {
        private const double MetersPerFoot = 0.3048;
        private const double MetersPerMile = 1609.344;

        public static string FormatMeters(double meters)
        {
            if (SettingsStore.Resolve().UnitsMetric)
            {
                if (meters < 1000.0)
                    return $"{(int)System.Math.Round(meters)} m";
                return (meters / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " km";
            }

            double feet = meters / MetersPerFoot;
            if (feet < 1000.0)
                return $"{(int)System.Math.Round(feet)} ft";
            return (meters / MetersPerMile).ToString("0.0", CultureInfo.InvariantCulture) + " mi";
        }
    }
}
