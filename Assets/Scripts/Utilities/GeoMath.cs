using UnityEngine;

namespace AlipiriAR.Utilities
{
    /// <summary>Geodesy and Web Mercator helpers shared by route building, the Kalman filter,
    /// polyline snapping and the demo map projection (PLAN.md §01, §08).</summary>
    public static class GeoMath
    {
        public const double EarthRadiusMeters = 6371000.0;

        /// <summary>Great-circle distance between two lat/lon points, in metres.</summary>
        public static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
        {
            double p1 = lat1 * Mathf.Deg2Rad, p2 = lat2 * Mathf.Deg2Rad;
            double dp = (lat2 - lat1) * Mathf.Deg2Rad;
            double dl = (lon2 - lon1) * Mathf.Deg2Rad;

            double a = System.Math.Sin(dp / 2) * System.Math.Sin(dp / 2)
                     + System.Math.Cos(p1) * System.Math.Cos(p2) * System.Math.Sin(dl / 2) * System.Math.Sin(dl / 2);
            double c = 2 * System.Math.Asin(System.Math.Sqrt(a));
            return EarthRadiusMeters * c;
        }

        /// <summary>Initial bearing from point 1 to point 2, in degrees, 0 = north, clockwise.</summary>
        public static double BearingDegrees(double lat1, double lon1, double lat2, double lon2)
        {
            double p1 = lat1 * Mathf.Deg2Rad, p2 = lat2 * Mathf.Deg2Rad;
            double dl = (lon2 - lon1) * Mathf.Deg2Rad;

            double y = System.Math.Sin(dl) * System.Math.Cos(p2);
            double x = System.Math.Cos(p1) * System.Math.Sin(p2) - System.Math.Sin(p1) * System.Math.Cos(p2) * System.Math.Cos(dl);
            double theta = System.Math.Atan2(y, x) * Mathf.Rad2Deg;
            return (theta + 360.0) % 360.0;
        }

        /// <summary>Destination point given a start, bearing (degrees) and distance (metres).</summary>
        public static (double lat, double lon) Destination(double lat, double lon, double bearingDeg, double distanceMeters)
        {
            double delta = distanceMeters / EarthRadiusMeters;
            double theta = bearingDeg * Mathf.Deg2Rad;
            double p1 = lat * Mathf.Deg2Rad, l1 = lon * Mathf.Deg2Rad;

            double p2 = System.Math.Asin(System.Math.Sin(p1) * System.Math.Cos(delta) + System.Math.Cos(p1) * System.Math.Sin(delta) * System.Math.Cos(theta));
            double l2 = l1 + System.Math.Atan2(
                System.Math.Sin(theta) * System.Math.Sin(delta) * System.Math.Cos(p1),
                System.Math.Cos(delta) - System.Math.Sin(p1) * System.Math.Sin(p2));

            return (p2 * Mathf.Rad2Deg, l2 * Mathf.Rad2Deg);
        }

        // ---- Web Mercator (used by the demo/tile basemap in Phase 8) ---------

        public static Vector2 LonLatToWorldPixel(double lon, double lat, int zoom)
        {
            var (x, y) = LonLatToWorldPixelD(lon, lat, zoom);
            return new Vector2((float)x, (float)y);
        }

        /// <summary>Double-precision Mercator world pixels. MapView subtracts an origin in this
        /// precision before ever touching a float — at z17 the world is ~33.5M px wide, well
        /// past float32's ~7-digit precision, so casting before the subtraction (as the float
        /// overload above does) would smear marker positions by several metres against the
        /// route's actual ~6,300 px span. Only the small post-subtraction offset becomes a Vector2.</summary>
        public static (double x, double y) LonLatToWorldPixelD(double lon, double lat, int zoom)
        {
            double tileSize = 256.0;
            double worldSize = tileSize * System.Math.Pow(2, zoom);

            double x = (lon + 180.0) / 360.0 * worldSize;
            double sinLat = System.Math.Sin(lat * System.Math.PI / 180.0);
            double y = (0.5 - System.Math.Log((1 + sinLat) / (1 - sinLat)) / (4 * System.Math.PI)) * worldSize;

            return (x, y);
        }

        public static (double lon, double lat) WorldPixelToLonLat(Vector2 pixel, int zoom)
        {
            double tileSize = 256.0;
            double worldSize = tileSize * System.Math.Pow(2, zoom);

            double lon = pixel.x / worldSize * 360.0 - 180.0;
            double n = System.Math.PI - 2.0 * System.Math.PI * pixel.y / worldSize;
            double lat = 180.0 / System.Math.PI * System.Math.Atan(0.5 * (System.Math.Exp(n) - System.Math.Exp(-n)));

            return (lon, lat);
        }

        // ---- Slippy-map (OSM/Google XYZ) tile addressing — TileBasemap's offline bake ----

        /// <summary>The tile that contains this lon/lat at the given integer zoom.</summary>
        public static (int x, int y) LonLatToTile(double lon, double lat, int zoom)
        {
            double n = System.Math.Pow(2, zoom);
            int x = (int)((lon + 180.0) / 360.0 * n);
            double latRad = lat * System.Math.PI / 180.0;
            int y = (int)((1.0 - System.Math.Log(System.Math.Tan(latRad) + 1.0 / System.Math.Cos(latRad)) / System.Math.PI) / 2.0 * n);
            return (x, y);
        }

        /// <summary>Lon/lat of tile (x, y)'s north-west corner. Tile (x+1, y+1)'s north-west
        /// corner is this same tile's south-east corner — the standard trick for getting a
        /// tile's full footprint without a separate "south-east" formula.</summary>
        public static (double lon, double lat) TileNorthWest(int x, int y, int zoom)
        {
            double n = System.Math.Pow(2, zoom);
            double lon = x / n * 360.0 - 180.0;
            double latRad = System.Math.Atan(System.Math.Sinh(System.Math.PI * (1 - 2 * y / n)));
            double lat = latRad * 180.0 / System.Math.PI;
            return (lon, lat);
        }
    }
}
