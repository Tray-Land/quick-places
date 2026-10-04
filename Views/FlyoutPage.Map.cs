using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using QuickPlaces.Controls;
using QuickPlaces.Models;
using QuickPlaces.Services;
using Windows.ApplicationModel;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using WinUIEx.Maps;
using MapControl = WinUIEx.Maps.MapControl;
using MapElement = WinUIEx.Maps.MapElement;
using MapElementsLayer = WinUIEx.Maps.MapElementsLayer;
using MapIcon = WinUIEx.Maps.MapIcon;
using MapStyle = WinUIEx.Maps.MapStyle;

namespace QuickPlaces.Views;

/// <summary>
/// Results on a map: an Azure Maps basemap (OpenStreetMap tiles when the build has no Azure Maps
/// key) under a pin per place, colored and marked like its row
/// in the list. Tapping a pin opens the place; hovering names it. The list stays the accessible
/// equivalent (map pins aren't separate UI Automation elements), so the switch is a view option.
/// </summary>
/// <remarks>
/// The MapControl and its tile layer are created the first time the map is shown, so someone who
/// only uses the list never loads the renderer or fetches a tile. Tiles load only while the map is
/// on screen, which keeps it inside the "no network while hidden" rule.
/// </remarks>
public sealed partial class FlyoutPage
{
    private const double PinRingSize = 34;
    private const double PinFillSize = 29;

    private readonly Dictionary<MapElement, PlaceSummary> _pinPlaces = [];
    private readonly Dictionary<PlaceCategory, (IconElement Fill, IconElement Glyph)> _pinVisuals = [];
    private readonly IconElement _pinRing = Disc(PinRingSize, new SolidColorBrush(Colors.White));
    private MapControl? _map;
    private MapElementsLayer? _pinLayer;
    private GeoPoint? _origin;
    private bool _showMap = SettingsService.ShowResultsOnMap;
    private bool _pinsStale = true;
    private bool _hasFitOnce;
    private int _mapRevealGeneration;

    private void ListModeButton_Click(object sender, RoutedEventArgs e) => SetResultsOnMap(false);

    private void MapModeButton_Click(object sender, RoutedEventArgs e) => SetResultsOnMap(true);

    private void SetResultsOnMap(bool map)
    {
        bool changed = map != _showMap;
        _showMap = map;
        SettingsService.ShowResultsOnMap = map;
        ApplyResultsView();

        if (changed)
        {
            Motion.SlideIn(map ? MapPanel : ResultsList, TimeSpan.Zero, distance: 8);
        }
    }

    /// <summary>Shows the list or the map to match the switch, building the map's pins if they're out of date.</summary>
    private void ApplyResultsView()
    {
        ListModeButton.IsChecked = !_showMap;
        MapModeButton.IsChecked = _showMap;
        ResultsList.Visibility = _showMap ? Visibility.Collapsed : Visibility.Visible;
        MapPanel.Visibility = _showMap ? Visibility.Visible : Visibility.Collapsed;
        MapHoverCard.Visibility = Visibility.Collapsed;

        if (_showMap && _searchState == SearchState.Results)
        {
            EnsureMap();
            if (_pinsStale)
            {
                ShowPins();
            }
        }
    }

    /// <summary>New results arrived: the map shows them now if it's up, or the next time it is.</summary>
    private void OnResultsChanged(GeoPoint origin)
    {
        _origin = origin;
        _pinsStale = true;
        ApplyResultsView();
    }

    private void EnsureMap()
    {
        if (_map is not null)
        {
            return;
        }

        _map = new MapControl();
        AutomationProperties.SetName(_map, "Results map");
        AutomationProperties.SetHelpText(_map, "Use arrow keys to move and plus or minus to zoom. The same places are in the list view.");

        if (Secrets.AzureMapsKey is { } key)
        {
            _map.MapServiceToken = key;
        }
        else
        {
            // No Azure Maps key in this build: OpenStreetMap's public tiles are free with no key,
            // under its tile usage policy (an identifying User-Agent and visible attribution).
            // https://operations.osmfoundation.org/policies/tiles/
            _map.MapStyle = MapStyle.Blank;
            _map.Layers.Add(new TileLayer(
                new TileLayerOptions
                {
                    TileUrl = "https://tile.openstreetmap.org/{z}/{x}/{y}.png",
                    TileSize = 256,
                    MaxSourceZoom = 19,
                    RequestHeaders = new Dictionary<string, string> { ["User-Agent"] = UserAgent() },
                },
                id: "openstreetmap")
            {
                Attribution = "© OpenStreetMap contributors",
                AttributionLink = new Uri("https://www.openstreetmap.org/copyright"),
            });
        }

        _pinLayer = new MapElementsLayer();
        _pinLayer.Tapped += PinLayer_Tapped;
        _pinLayer.PointerEntered += PinLayer_PointerEntered;
        _pinLayer.PointerExited += PinLayer_PointerExited;
        _map.Layers.Add(_pinLayer);

        ActualThemeChanged += Page_ActualThemeChanged;
        ApplyMapStyle();
        MapHost.Child = _map;
        RevealMap();
    }

    /// <summary>Azure's Road basemap on a light taskbar, Night on a dark one (the flyout follows the taskbar).</summary>
    private void ApplyMapStyle()
    {
        if (_map is null || Secrets.AzureMapsKey is null)
        {
            return;
        }

        bool dark = ActualTheme == ElementTheme.Dark;
        MapStyle style = dark ? MapStyle.Night : MapStyle.Road;
        if (_map.MapStyle != style)
        {
            _map.MapStyle = style;
            RevealMap();
        }
    }

    private void Page_ActualThemeChanged(FrameworkElement sender, object args) => ApplyMapStyle();

    /// <summary>
    /// Covers the map until its tiles have drawn, then fades the cover out. WinUIEx.Maps clears to
    /// light gray until tiles arrive (a flash on a dark flyout) and has no public tile-loaded
    /// signal; its CaptureRenderedFrameAsync waits for the current tile wave and a finished GPU
    /// frame, so use it when it's there and fall back to a short delay. Same approach as tray-radar.
    /// </summary>
    private async void RevealMap()
    {
        if (_map is not { } map)
        {
            return;
        }

        int generation = ++_mapRevealGeneration;
        MapCover.Opacity = 1;
        MapCover.Visibility = Visibility.Visible;

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(3));
        try
        {
            System.Reflection.MethodInfo? capture = typeof(MapControl).GetMethod(
                "CaptureRenderedFrameAsync",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (capture?.Invoke(map, [timeout.Token]) is Task frame)
            {
                await frame.WaitAsync(timeout.Token);
            }
            else
            {
                await Task.Delay(600);
            }
        }
        catch (Exception)
        {
            // Timed out (offline, say) or the internal API changed: show whatever is there.
        }

        if (generation != _mapRevealGeneration || map != _map)
        {
            return;
        }

        if (!Motion.IsEnabled)
        {
            MapCover.Visibility = Visibility.Collapsed;
            return;
        }

        DoubleAnimation fade = new() { To = 0, Duration = TimeSpan.FromMilliseconds(250) };
        Storyboard.SetTarget(fade, MapCover);
        Storyboard.SetTargetProperty(fade, nameof(Opacity));
        Storyboard fadeOut = new() { Children = { fade } };
        fadeOut.Completed += (_, _) =>
        {
            if (generation == _mapRevealGeneration)
            {
                MapCover.Visibility = Visibility.Collapsed;
            }
        };
        fadeOut.Begin();
    }

    private void ShowPins()
    {
        if (_map is null || _pinLayer is null || _origin is not { } origin)
        {
            return;
        }

        _pinsStale = false;
        _pinLayer.MapElements.Clear();
        _pinPlaces.Clear();

        List<MapElement> elements = [];

        // You, under the places: an accent dot in a white ring. Not hit-testable.
        Geopoint here = ToGeopoint(origin.Latitude, origin.Longitude);
        elements.Add(new MapIcon(Disc(20, new SolidColorBrush(Colors.White)), here) { ZIndex = 0, IsEnabled = false });
        elements.Add(new MapIcon(Disc(14, new SolidColorBrush((Windows.UI.Color)Application.Current.Resources["SystemAccentColor"])), here) { ZIndex = 1, IsEnabled = false });

        // Nearest result drawn on top, so where pins overlap the closest one is the one you see and tap.
        for (int i = _results.Count - 1; i >= 0; i--)
        {
            PlaceSummary place = _results[i];
            (IconElement fill, IconElement glyph) = PinVisuals(place.Category);
            Geopoint at = ToGeopoint(place.Latitude, place.Longitude);
            int z = 10 + (_results.Count - i) * 3;
            foreach (MapIcon icon in new[]
            {
                new MapIcon(_pinRing, at) { ZIndex = z },
                new MapIcon(fill, at) { ZIndex = z + 1 },
                new MapIcon(glyph, at) { ZIndex = z + 2 },
            })
            {
                _pinPlaces[icon] = place;
                elements.Add(icon);
            }
        }

        _pinLayer.MapElements.AddRange(elements);
        FitMapToResults();
    }

    /// <summary>Frames every result and you. Waits for the map's first layout, since fitting needs its size.</summary>
    private void FitMapToResults()
    {
        if (_map is null || _origin is not { } origin)
        {
            return;
        }

        MapBounds bounds = MapBounds.Of(_results
            .Select(p => (p.Latitude, p.Longitude))
            .Append((origin.Latitude, origin.Longitude)));

        if (!_hasFitOnce)
        {
            // Until the renderer is up, a bounds fit is refused: start near the right place so the
            // first frame is never the whole world, then fit exactly once the map has loaded.
            _map.Center = ToGeopoint(bounds.CenterLatitude, bounds.CenterLongitude);
            _map.ZoomLevel = 13;
        }

        if (!_map.IsLoaded || _map.ActualWidth < 1)
        {
            _map.Loaded -= Map_Loaded;
            _map.Loaded += Map_Loaded;
            return;
        }

        _ = FitAsync(_map, bounds);
    }

    private async Task FitAsync(MapControl map, MapBounds bounds)
    {
        GeoboundingBox box = new(
            new BasicGeoposition { Latitude = bounds.North, Longitude = bounds.West },
            new BasicGeoposition { Latitude = bounds.South, Longitude = bounds.East });
        Thickness margin = new(36, 48, 36, 36);

        // The first fit jumps straight there; later searches glide from wherever the map was.
        MapAnimationKind animation = _hasFitOnce ? MapAnimationKind.Default : MapAnimationKind.None;
        _hasFitOnce = true;
        if (!await map.TrySetViewBoundsAsync(box, margin, animation) && map == _map && !_pinsStale)
        {
            // Refused while the renderer was still starting: try once more after layout settles.
            await Task.Delay(150);
            if (map == _map && !_pinsStale)
            {
                await map.TrySetViewBoundsAsync(box, margin, MapAnimationKind.None);
            }
        }
    }

    private void Map_Loaded(object sender, RoutedEventArgs e)
    {
        if (_map is not null)
        {
            _map.Loaded -= Map_Loaded;
            FitMapToResults();
        }
    }

    private void PinLayer_Tapped(object? sender, MapElementTappedEventArgs e)
    {
        if (_pinPlaces.TryGetValue(e.MapElement, out PlaceSummary? place))
        {
            e.Handled = true;
            MapHoverCard.Visibility = Visibility.Collapsed;
            OpenPlace(place);
        }
    }

    private void PinLayer_PointerEntered(object? sender, MapElementPointerEventArgs e)
    {
        if (_pinPlaces.TryGetValue(e.MapElement, out PlaceSummary? place))
        {
            MapHoverName.Text = place.Name;
            MapHoverSubtitle.Text = JoinDot(place.TypeLabel, place.DistanceText);
            if (MapHoverCard.Visibility != Visibility.Visible)
            {
                MapHoverCard.Visibility = Visibility.Visible;
                Motion.SlideIn(MapHoverCard, TimeSpan.Zero, distance: 4);
            }
        }
    }

    private void PinLayer_PointerExited(object? sender, MapElementPointerEventArgs e) =>
        MapHoverCard.Visibility = Visibility.Collapsed;

    /// <summary>Lets the MapControl and its pins go with the page.</summary>
    private void DisposeMap()
    {
        if (_pinLayer is not null)
        {
            _pinLayer.Tapped -= PinLayer_Tapped;
            _pinLayer.PointerEntered -= PinLayer_PointerEntered;
            _pinLayer.PointerExited -= PinLayer_PointerExited;
            _pinLayer = null;
        }

        ActualThemeChanged -= Page_ActualThemeChanged;
        _mapRevealGeneration++;
        if (_map is not null)
        {
            _map.Loaded -= Map_Loaded;
            MapHost.Child = null;
            _map = null;
        }

        _pinPlaces.Clear();
    }

    // One fill and one glyph per category, shared by every pin of that kind so they share a raster.
    private (IconElement Fill, IconElement Glyph) PinVisuals(PlaceCategory category)
    {
        if (!_pinVisuals.TryGetValue(category, out (IconElement Fill, IconElement Glyph) visuals))
        {
            visuals = (
                Disc(PinFillSize, PlaceVisuals.CategoryBrush(category)),
                new FontIcon { Glyph = PlaceCategories.Glyph(category), FontSize = 14, Foreground = new SolidColorBrush(Colors.White) });
            _pinVisuals[category] = visuals;
        }

        return visuals;
    }

    private static IconElement Disc(double size, Brush brush) => new PathIcon
    {
        Width = size,
        Height = size,
        Foreground = brush,
        Data = new EllipseGeometry { Center = new Point(size / 2, size / 2), RadiusX = size / 2, RadiusY = size / 2 },
    };

    private static Geopoint ToGeopoint(double latitude, double longitude) =>
        new(new BasicGeoposition { Latitude = latitude, Longitude = longitude });

    private static string UserAgent()
    {
        string version = "1.0";
        try
        {
            PackageVersion v = Package.Current.Id.Version;
            version = $"{v.Major}.{v.Minor}.{v.Build}";
        }
        catch (InvalidOperationException)
        {
            // Unpackaged: no package identity to read.
        }

        return $"QuickPlaces/{version} (Windows tray app)";
    }
}
