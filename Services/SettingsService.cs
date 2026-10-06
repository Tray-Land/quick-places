using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Windows.Storage;

namespace QuickPlaces.Services;

/// <summary>
/// Typed wrapper over <see cref="ApplicationData.LocalSettings"/>. Every read falls back to a
/// default so a missing or corrupt value never crashes the app. Not for secrets: see
/// <see cref="Secrets"/> (build-time app keys) and <see cref="CredentialStore"/> (user tokens).
/// </summary>
/// <remarks>
/// LocalSettings values must be small (8 KB each) WinRT primitives. Store enums as int, and lists
/// or records as JSON through a source-generated <c>JsonSerializerContext</c> (trim-safe) with
/// <see cref="GetJson"/> / <see cref="SetJson"/>. Anything larger belongs in a file under
/// <c>ApplicationData.Current.LocalFolder</c>.
/// </remarks>
internal static class SettingsService
{
    /// <summary>False until the first launch has shown the flyout.</summary>
    public static bool HasLaunchedBefore
    {
        get => Get(nameof(HasLaunchedBefore), false);
        set => Set(nameof(HasLaunchedBefore), value);
    }

    /// <summary>The last few searches, newest first, offered as chips on the start screen.</summary>
    public static IReadOnlyList<string> RecentSearches
    {
        get => GetJson(nameof(RecentSearches), SettingsJsonContext.Default.ListString) ?? [];
        set => SetJson(nameof(RecentSearches), value.ToList(), SettingsJsonContext.Default.ListString);
    }

    /// <summary>Show search results on a map instead of a list.</summary>
    public static bool ShowResultsOnMap
    {
        get => Get(nameof(ShowResultsOnMap), false);
        set => Set(nameof(ShowResultsOnMap), value);
    }

    /// <summary>The postal code the user entered when Windows location is off, or null.</summary>
    public static string? ManualPostalCode
    {
        get => Get<string?>(nameof(ManualPostalCode), null);
        set => Set(nameof(ManualPostalCode), value);
    }

    /// <summary>How the user chose to be located: "device" (Windows location), "zip", or null if not chosen yet.</summary>
    public static string? LocationMode
    {
        get => Get<string?>(nameof(LocationMode), null);
        set => Set(nameof(LocationMode), value);
    }

    private static double? GetDouble(string key) => Get<double?>(key, null) is { } v ? v : null;

    /// <summary>Where <see cref="ManualPostalCode"/> was resolved to, or null if none is saved.</summary>
    public static (double Latitude, double Longitude)? ManualLocation
    {
        get => GetDouble("ManualLatitude") is { } lat && GetDouble("ManualLongitude") is { } lon ? (lat, lon) : null;
        set
        {
            Set("ManualLatitude", value?.Latitude);
            Set("ManualLongitude", value?.Longitude);
        }
    }

    public static T? GetJson<T>(string key, JsonTypeInfo<T> typeInfo)
    {
        if (Get<string?>(key, null) is not { } json)
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize(json, typeInfo);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    public static void SetJson<T>(string key, T? value, JsonTypeInfo<T> typeInfo) =>
        Set(key, value is null ? null : JsonSerializer.Serialize(value, typeInfo));

    private static T Get<T>(string key, T defaultValue)
    {
        try
        {
            return ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out object? value) && value is T typed
                ? typed
                : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    private static void Set(string key, object? value)
    {
        try
        {
            if (value is null)
            {
                ApplicationData.Current.LocalSettings.Values.Remove(key);
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values[key] = value;
            }
        }
        catch
        {
            // Settings are best-effort; ignore storage failures.
        }
    }
}

[JsonSerializable(typeof(List<string>))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
