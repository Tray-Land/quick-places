namespace QuickPlaces.Models;

/// <summary>What the detail view shows for one place: contact info, rating and opening hours.</summary>
public sealed record PlaceDetails(
    string Id,
    string Name,
    string Address,
    string? NationalPhone,
    string? InternationalPhone,
    string? Website,
    string? MapsUri,
    IReadOnlyList<string> WeekdayHours,
    bool? OpenNow)
{
    /// <summary>When the place next closes, from Google's current opening hours.</summary>
    public DateTimeOffset? NextCloseTime { get; init; }

    public string? TypeLabel { get; init; }

    public PlaceCategory Category { get; init; }

    /// <summary>Average review rating, 1 to 5.</summary>
    public double? Rating { get; init; }

    public int? UserRatingCount { get; init; }

    /// <summary>0 (free) to 4 (very expensive), or null when Google doesn't say.</summary>
    public int? PriceLevel { get; init; }
}
