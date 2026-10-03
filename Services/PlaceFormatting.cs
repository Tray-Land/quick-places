using System.Globalization;

namespace QuickPlaces.Services;

/// <summary>Pure display helpers for places: distance, phone links, website text, today's row.</summary>
internal static class PlaceFormatting
{
    private const double EarthRadiusMeters = 6_371_000;

    public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = ToRadians(lat2 - lat1);
        double dLon = ToRadians(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return EarthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    public static string FormatDistance(double meters, bool metric, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        if (metric)
        {
            return meters < 1000
                ? string.Create(culture, $"{Math.Max(10, Math.Round(meters / 10) * 10):0} m")
                : string.Create(culture, $"{meters / 1000:0.#} km");
        }

        double feet = meters * 3.28084;
        return feet < 528
            ? string.Create(culture, $"{Math.Max(50, Math.Round(feet / 50) * 50):0} ft")
            : string.Create(culture, $"{meters / 1609.344:0.#} mi");
    }

    /// <summary>
    /// A <c>tel:</c> URI a phone's camera app offers to dial. Prefers the international number
    /// (which carries the country code), keeping only digits and a leading plus.
    /// </summary>
    public static string? TelUri(string? internationalPhone, string? nationalPhone)
    {
        string? source = !string.IsNullOrWhiteSpace(internationalPhone) ? internationalPhone : nationalPhone;
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        string digits = new(source.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 0)
        {
            return null;
        }

        return source.TrimStart().StartsWith('+') ? $"tel:+{digits}" : $"tel:{digits}";
    }

    /// <summary>Host name without "www.", for showing a website link compactly.</summary>
    public static string WebsiteDisplay(string website)
    {
        if (!Uri.TryCreate(website, UriKind.Absolute, out Uri? uri))
        {
            return website;
        }

        string host = uri.Host;
        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
    }

    /// <summary>Index of today in Google's Monday-first <c>weekdayDescriptions</c>.</summary>
    public static int WeekdayIndex(DayOfWeek day) => ((int)day + 6) % 7;

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
