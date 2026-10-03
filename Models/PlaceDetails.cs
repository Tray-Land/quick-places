namespace QuickPlaces.Models;

/// <summary>What the detail view shows for one place: contact info and opening hours.</summary>
public sealed record PlaceDetails(
    string Id,
    string Name,
    string Address,
    string? NationalPhone,
    string? InternationalPhone,
    string? Website,
    string? MapsUri,
    IReadOnlyList<string> WeekdayHours,
    bool? OpenNow);
