using Windows.Devices.Geolocation;

namespace QuickPlaces.Services;

/// <summary>A point; <see cref="PostalCode"/> is set when it's a zip code's center rather than the device's position.</summary>
internal readonly record struct GeoPoint(double Latitude, double Longitude, string? PostalCode = null);

internal sealed class LocationUnavailableException(string message) : Exception(message);

/// <summary>
/// The device's location through Windows' location service, cached for a few minutes so repeat
/// searches don't wake the GPS or Wi-Fi positioning. Call from the UI thread (permission prompt).
/// </summary>
internal static class LocationService
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);

    private static GeoPoint? s_last;
    private static DateTimeOffset s_lastAt;

    public static async Task<GeoPoint> GetAsync(CancellationToken ct)
    {
        if (s_last is { } cached && DateTimeOffset.UtcNow - s_lastAt < CacheFor)
        {
            return cached;
        }

        if (SettingsService.LocationMode != "device")
        {
            return SavedPoint ?? throw new LocationUnavailableException("Choose how to find your location: use your device or enter a zip code.");
        }

        if (!await RequestAccessAsync(ct))
        {
            return SavedPoint ?? throw new LocationUnavailableException("Location access is off. Turn it on in Windows Settings > Privacy > Location, or enter a zip code.");
        }

        try
        {
            Geoposition pos = await new Geolocator { DesiredAccuracyInMeters = 100 }
                .GetGeopositionAsync(CacheFor, TimeSpan.FromSeconds(10))
                .AsTask(ct);
            BasicGeoposition p = pos.Coordinate.Point.Position;
            s_last = new GeoPoint(p.Latitude, p.Longitude);
            s_lastAt = DateTimeOffset.UtcNow;
            return s_last.Value;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return SavedPoint ?? throw new LocationUnavailableException("Couldn't determine your location.");
        }
    }

    /// <summary>Checks Windows location access, asking for it if it hasn't been decided. True when allowed.</summary>
    public static async Task<bool> RequestAccessAsync(CancellationToken ct)
    {
        try
        {
            return await Geolocator.RequestAccessAsync().AsTask(ct) == GeolocationAccessStatus.Allowed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>The location resolved from the zip code the user entered, if any.</summary>
    public static GeoPoint? SavedPoint =>
        SettingsService.ManualLocation is var (lat, lon) ? new GeoPoint(lat, lon, SettingsService.ManualPostalCode) : null;

    /// <summary>Records that the user chose Windows location (after access was granted).</summary>
    public static void UseDevice()
    {
        SettingsService.LocationMode = "device";
        s_last = null;
    }

    public static void SaveManual(string postalCode, GeoPoint point)
    {
        SettingsService.LocationMode = "zip";
        SettingsService.ManualPostalCode = postalCode;
        SettingsService.ManualLocation = (point.Latitude, point.Longitude);
        s_last = null;
    }
}
