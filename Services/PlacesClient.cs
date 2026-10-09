using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using QuickPlaces.Models;

namespace QuickPlaces.Services;

/// <summary>
/// Google Places API (New). Search asks only for the cheap identity fields; the contact and
/// hours fields (a pricier SKU) are requested once, for the one place the user opens.
/// </summary>
internal sealed class PlacesClient
{
    private const string Endpoint = "https://places.googleapis.com/v1/";
    private const double SearchRadiusMeters = 8000;

    private static readonly HttpClient s_http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private readonly Dictionary<string, PlaceDetails> _detailsCache = [];

    public async Task<IReadOnlyList<PlaceSummary>> SearchAsync(string query, GeoPoint origin, CancellationToken ct)
    {
        using MemoryStream body = new();
        using (Utf8JsonWriter w = new(body))
        {
            w.WriteStartObject();
            w.WriteString("textQuery", query);
            w.WriteNumber("maxResultCount", 10);
            w.WriteStartObject("locationBias");
            w.WriteStartObject("circle");
            w.WriteStartObject("center");
            w.WriteNumber("latitude", origin.Latitude);
            w.WriteNumber("longitude", origin.Longitude);
            w.WriteEndObject();
            w.WriteNumber("radius", SearchRadiusMeters);
            w.WriteEndObject();
            w.WriteEndObject();
            w.WriteEndObject();
        }

        ByteArrayContent content = new(body.ToArray());
        content.Headers.ContentType = new("application/json");
        using HttpRequestMessage request = new(HttpMethod.Post, Endpoint + "places:searchText") { Content = content };
        // The type fields pick each row's symbol. Open-now (only that subfield of the hours) puts
        // search in the Text Search Enterprise SKU: it's what lets each row say "Open" or "Closed".
        request.Headers.Add("X-Goog-FieldMask",
            "places.id,places.displayName,places.shortFormattedAddress,places.location,places.primaryType,places.primaryTypeDisplayName,places.types,"
            + "places.currentOpeningHours.openNow,places.currentOpeningHours.nextCloseTime");

        string json = await SendAsync(request, ct);
        bool metric = RegionInfo.CurrentRegion.IsMetric;
        return PlacesParser.ParseSearch(json)
            .Select(p =>
            {
                double meters = PlaceFormatting.DistanceMeters(origin.Latitude, origin.Longitude, p.Latitude, p.Longitude);
                return p with
                {
                    DistanceText = origin.PostalCode is { Length: > 0 } zip
                        ? PlaceFormatting.FormatZipDistance(meters, metric, zip)
                        : PlaceFormatting.FormatDistance(meters, metric),
                };
            })
            .ToList();
    }

    /// <summary>Resolves a zip or postal code to a point (the first match), or null if nothing matches.</summary>
    public async Task<GeoPoint?> GeocodeAsync(string postalCode, CancellationToken ct)
    {
        using MemoryStream body = new();
        using (Utf8JsonWriter w = new(body))
        {
            w.WriteStartObject();
            w.WriteString("textQuery", postalCode);
            w.WriteNumber("maxResultCount", 1);
            w.WriteEndObject();
        }

        ByteArrayContent content = new(body.ToArray());
        content.Headers.ContentType = new("application/json");
        using HttpRequestMessage request = new(HttpMethod.Post, Endpoint + "places:searchText") { Content = content };
        request.Headers.Add("X-Goog-FieldMask", "places.location");

        string json = await SendAsync(request, ct);
        using JsonDocument doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("places", out JsonElement places)
            && places.GetArrayLength() > 0
            && places[0].TryGetProperty("location", out JsonElement loc)
            && loc.TryGetProperty("latitude", out JsonElement lat)
            && loc.TryGetProperty("longitude", out JsonElement lon))
        {
            return new GeoPoint(lat.GetDouble(), lon.GetDouble());
        }

        return null;
    }

    public async Task<PlaceDetails> GetDetailsAsync(string placeId, CancellationToken ct)
    {
        if (_detailsCache.TryGetValue(placeId, out PlaceDetails? cached))
        {
            return cached;
        }

        using HttpRequestMessage request = new(HttpMethod.Get, Endpoint + "places/" + Uri.EscapeDataString(placeId));
        // Phone, website and hours already make this an Enterprise request; rating, review count and
        // price level are in the same SKU, so they cost nothing extra.
        request.Headers.Add("X-Goog-FieldMask",
            "id,displayName,formattedAddress,nationalPhoneNumber,internationalPhoneNumber,websiteUri,googleMapsUri,regularOpeningHours,currentOpeningHours,"
            + "primaryType,primaryTypeDisplayName,types,rating,userRatingCount,priceLevel");

        PlaceDetails details = PlacesParser.ParseDetails(await SendAsync(request, ct));
        _detailsCache[placeId] = details;
        return details;
    }

    private static async Task<string> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        string key = Secrets.GooglePlacesApiKey ?? throw new PlacesApiException("Add GooglePlacesApiKey to OAuth.resw and rebuild.");
        request.Headers.Add("X-Goog-Api-Key", key);

        using HttpResponseMessage response = await s_http.SendAsync(request, ct);
        string text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new PlacesApiException(PlacesParser.ParseErrorMessage(text) ?? $"Google Places returned {(int)response.StatusCode}.");
        }

        return text;
    }
}

internal sealed class PlacesApiException(string message) : Exception(message);
