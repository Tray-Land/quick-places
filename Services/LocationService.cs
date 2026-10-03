using Windows.Devices.Geolocation;

namespace QuickPlaces.Services;

internal readonly record struct GeoPoint(double Latitude, double Longitude);

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

        GeolocationAccessStatus access = await Geolocator.RequestAccessAsync().AsTask(ct);
        if (access != GeolocationAccessStatus.Allowed)
        {
            throw new LocationUnavailableException("Location access is off. Turn it on in Windows Settings > Privacy > Location.");
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
            throw new LocationUnavailableException("Couldn't determine your location.");
        }
    }
}
