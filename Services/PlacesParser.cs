using System.Text.Json;
using QuickPlaces.Models;

namespace QuickPlaces.Services;

/// <summary>Turns Places API (New) JSON into models. No WinRT dependency, so it is unit tested.</summary>
internal static class PlacesParser
{
    public static IReadOnlyList<PlaceSummary> ParseSearch(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        List<PlaceSummary> results = [];
        if (!doc.RootElement.TryGetProperty("places", out JsonElement places) || places.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (JsonElement place in places.EnumerateArray())
        {
            string? id = GetString(place, "id");
            string? name = place.TryGetProperty("displayName", out JsonElement dn) ? GetString(dn, "text") : null;
            if (id is null || name is null)
            {
                continue;
            }

            double lat = 0, lon = 0;
            if (place.TryGetProperty("location", out JsonElement loc))
            {
                lat = GetDouble(loc, "latitude");
                lon = GetDouble(loc, "longitude");
            }

            results.Add(new PlaceSummary(id, name, GetString(place, "shortFormattedAddress") ?? GetString(place, "formattedAddress") ?? "", lat, lon)
            {
                TypeLabel = GetTypeLabel(place),
                Category = GetCategory(place),
                OpenNow = place.TryGetProperty("currentOpeningHours", out JsonElement oh)
                    && oh.ValueKind == JsonValueKind.Object
                    && oh.TryGetProperty("openNow", out JsonElement on)
                    && on.ValueKind is JsonValueKind.True or JsonValueKind.False
                        ? on.GetBoolean()
                        : null,
            });
        }

        return results;
    }

    public static PlaceDetails ParseDetails(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        string name = root.TryGetProperty("displayName", out JsonElement dn) ? GetString(dn, "text") ?? "" : "";
        List<string> hours = [];
        bool? openNow = null;

        // Current hours include holiday and special-day changes, so they win for "open now".
        foreach (string key in new[] { "currentOpeningHours", "regularOpeningHours" })
        {
            if (!root.TryGetProperty(key, out JsonElement oh))
            {
                continue;
            }

            if (openNow is null && oh.TryGetProperty("openNow", out JsonElement on) && on.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                openNow = on.GetBoolean();
            }

            if (hours.Count == 0 && key == "regularOpeningHours" && oh.TryGetProperty("weekdayDescriptions", out JsonElement wd) && wd.ValueKind == JsonValueKind.Array)
            {
                hours.AddRange(wd.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s.Length > 0));
            }
        }

        return new PlaceDetails(
            GetString(root, "id") ?? "",
            name,
            GetString(root, "formattedAddress") ?? "",
            GetString(root, "nationalPhoneNumber"),
            GetString(root, "internationalPhoneNumber"),
            GetString(root, "websiteUri"),
            GetString(root, "googleMapsUri"),
            hours,
            openNow)
        {
            TypeLabel = GetTypeLabel(root),
            Category = GetCategory(root),
            Rating = root.TryGetProperty("rating", out JsonElement r) && r.ValueKind == JsonValueKind.Number ? r.GetDouble() : null,
            UserRatingCount = root.TryGetProperty("userRatingCount", out JsonElement c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : null,
            PriceLevel = ParsePriceLevel(GetString(root, "priceLevel")),
        };
    }

    /// <summary>Places (New) price levels are enum names; null for unspecified or unknown.</summary>
    public static int? ParsePriceLevel(string? value) => value switch
    {
        "PRICE_LEVEL_FREE" => 0,
        "PRICE_LEVEL_INEXPENSIVE" => 1,
        "PRICE_LEVEL_MODERATE" => 2,
        "PRICE_LEVEL_EXPENSIVE" => 3,
        "PRICE_LEVEL_VERY_EXPENSIVE" => 4,
        _ => null,
    };

    private static string? GetTypeLabel(JsonElement place) =>
        place.TryGetProperty("primaryTypeDisplayName", out JsonElement label) ? GetString(label, "text") : null;

    private static PlaceCategory GetCategory(JsonElement place)
    {
        IEnumerable<string>? types = place.TryGetProperty("types", out JsonElement t) && t.ValueKind == JsonValueKind.Array
            ? t.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList()
            : null;
        return PlaceCategories.FromTypes(GetString(place, "primaryType"), types);
    }

    /// <summary>The <c>error.message</c> of a failed response, or null when the body isn't one.</summary>
    public static string? ParseErrorMessage(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("error", out JsonElement err) ? GetString(err, "message") : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static double GetDouble(JsonElement e, string name) =>
        e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
}
