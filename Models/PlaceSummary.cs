namespace QuickPlaces.Models;

/// <summary>One row of search results: just enough to pick a place and show how far away it is.</summary>
public sealed record PlaceSummary(string Id, string Name, string Address, double Latitude, double Longitude)
{
    public string DistanceText { get; init; } = "";
}
