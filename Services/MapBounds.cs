namespace QuickPlaces.Services;

/// <summary>
/// The box the results map zooms to: every place plus the user's own position. A box that's
/// nearly a point (one result next door) grows to a minimum size so the map stays at street level
/// instead of zooming in as far as it can.
/// </summary>
public readonly record struct MapBounds(double North, double West, double South, double East)
{
    /// <summary>About 300 m of latitude: roughly a few blocks across.</summary>
    public const double MinSpanDegrees = 0.003;

    public double CenterLatitude => (North + South) / 2;

    public double CenterLongitude => (West + East) / 2;

    public static MapBounds Of(IEnumerable<(double Latitude, double Longitude)> points)
    {
        double north = double.MinValue, south = double.MaxValue, west = double.MaxValue, east = double.MinValue;
        bool any = false;
        foreach ((double lat, double lng) in points)
        {
            any = true;
            north = Math.Max(north, lat);
            south = Math.Min(south, lat);
            east = Math.Max(east, lng);
            west = Math.Min(west, lng);
        }

        if (!any)
        {
            throw new ArgumentException("At least one point is needed.", nameof(points));
        }

        (south, north) = Widen(south, north, MinSpanDegrees);
        (west, east) = Widen(west, east, MinSpanDegrees);
        return new MapBounds(Math.Min(north, 85), west, Math.Max(south, -85), east);
    }

    private static (double Low, double High) Widen(double low, double high, double minSpan)
    {
        double grow = (minSpan - (high - low)) / 2;
        return grow > 0 ? (low - grow, high + grow) : (low, high);
    }
}
