namespace QuickPlaces.Models;

/// <summary>One row of search results: just enough to pick a place, recognize it, and see how far away it is.</summary>
public sealed record PlaceSummary(string Id, string Name, string Address, double Latitude, double Longitude)
{
    public string DistanceText { get; init; } = "";

    /// <summary>Google's localized label for the primary type ("Pizza restaurant"), when it has one.</summary>
    public string? TypeLabel { get; init; }

    public PlaceCategory Category { get; init; }

    public string Glyph => PlaceCategories.Glyph(Category);

    /// <summary>Open right now, closed, or null when Google has no hours for the place.</summary>
    public bool? OpenNow { get; init; }

    public bool IsOpenNow => OpenNow == true;

    public bool IsClosedNow => OpenNow == false;

    /// <summary>"Pizza restaurant · 1 Main St", or just the address.</summary>
    public string Subtitle => string.Join(" · ", new[] { TypeLabel, Address }.Where(s => !string.IsNullOrEmpty(s)));
}
