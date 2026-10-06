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

    /// <summary>
    /// Splits a weekday description ("Monday: 9:00 AM – 5:00 PM") into its day and hours, for a
    /// two-column table. A line without the separator comes back whole as the hours.
    /// </summary>
    public static (string Day, string Hours) SplitWeekday(string description)
    {
        int colon = description.IndexOf(": ", StringComparison.Ordinal);
        return colon > 0
            ? (description[..colon].Trim(), description[(colon + 2)..].Trim())
            : ("", description.Trim());
    }

    /// <summary>True when the place is open and its next closing time is within the coming hour.</summary>
    public static bool IsClosingSoon(bool? openNow, DateTimeOffset? nextClose, DateTimeOffset now) =>
        openNow == true && nextClose is { } t && t > now && t - now <= TimeSpan.FromHours(1);

    /// <summary>Today's hours ("9:00 AM – 5:00 PM", "Closed"), or null when there are none.</summary>
    public static string? TodayHours(IReadOnlyList<string> weekdayHours, DayOfWeek today)
    {
        int index = WeekdayIndex(today);
        return index < weekdayHours.Count ? SplitWeekday(weekdayHours[index]).Hours : null;
    }

    /// <summary>
    /// Five star fills for a 1–5 rating, rounded to the nearest half: 2 = full, 1 = half, 0 = empty.
    /// </summary>
    public static int[] StarFills(double rating)
    {
        double halves = Math.Round(Math.Clamp(rating, 0, 5) * 2, MidpointRounding.AwayFromZero);
        int[] fills = new int[5];
        for (int i = 0; i < 5; i++)
        {
            fills[i] = (int)Math.Clamp(halves - (i * 2), 0, 2);
        }

        return fills;
    }

    /// <summary>"$$" for a 0–4 price level in the given currency symbol; "Free" for 0.</summary>
    public static string? PriceText(int? level, string currencySymbol) => level switch
    {
        null => null,
        0 => "Free",
        _ => string.Concat(Enumerable.Repeat(currencySymbol, Math.Clamp(level.Value, 1, 4))),
    };

    /// <summary>
    /// The recent-search list with <paramref name="query"/> moved to the front: case-insensitive
    /// de-duplication, blanks ignored, at most <paramref name="max"/> entries.
    /// </summary>
    public static List<string> PushRecent(IEnumerable<string>? recent, string query, int max = 5)
    {
        List<string> list = [];
        string trimmed = query.Trim();
        if (trimmed.Length > 0)
        {
            list.Add(trimmed);
        }

        foreach (string item in recent ?? [])
        {
            if (list.Count >= max)
            {
                break;
            }

            if (!string.IsNullOrWhiteSpace(item) && !list.Contains(item, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(item);
            }
        }

        return list;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
