using System.Globalization;
using System.Net.Http;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using QuickPlaces.Models;
using QuickPlaces.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace QuickPlaces.Views;

/// <summary>
/// Search nearby, pick a place, see its hours, website and phone, and scan a QR code to call it
/// from a phone. Nothing runs in the background: a request happens only when the user searches or
/// opens a place, and any request still in flight is cancelled when the flyout hides.
/// </summary>
public sealed partial class FlyoutPage : Page, IDisposable
{
    private readonly PlacesClient _client = new();
    private CancellationTokenSource? _inFlight;
    private PlaceDetails? _details;

    public FlyoutPage()
    {
        InitializeComponent();
        TitleText.Text = App.DisplayName;

        if (Secrets.GooglePlacesApiKey is null)
        {
            SearchPanel.Visibility = Visibility.Collapsed;
            MissingKeyText.Visibility = Visibility.Visible;
        }
    }

    /// <summary>The flyout opened: put the cursor where the user will type.</summary>
    public void OnShown()
    {
        if (SearchPanel.Visibility == Visibility.Visible)
        {
            SearchBox.Focus(FocusState.Programmatic);
            SearchBox.SelectAll();
        }
    }

    /// <summary>The flyout hid: abandon any request in flight.</summary>
    public void OnHidden() => CancelInFlight();

    public void Dispose() => CancelInFlight();

    private void CancelInFlight()
    {
        _inFlight?.Cancel();
        _inFlight = null;
        LoadingRing.IsActive = false;
    }

    private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            _ = SearchAsync();
        }
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e) => _ = SearchAsync();

    private async Task SearchAsync()
    {
        string query = SearchBox.Text.Trim();
        if (query.Length == 0)
        {
            return;
        }

        CancellationToken ct = BeginRequest();
        try
        {
            GeoPoint origin = await LocationService.GetAsync(ct);
            IReadOnlyList<PlaceSummary> results = await _client.SearchAsync(query, origin, ct);

            ResultsList.ItemsSource = results;
            EmptyText.Text = $"No results for \"{query}\" nearby.";
            EmptyText.Visibility = results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            StatusBar.IsOpen = false;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            EndRequest(ct);
        }
    }

    private async void ResultsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not PlaceSummary place)
        {
            return;
        }

        CancellationToken ct = BeginRequest();
        try
        {
            ShowDetails(await _client.GetDetailsAsync(place.Id, ct));
            StatusBar.IsOpen = false;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            EndRequest(ct);
        }
    }

    private CancellationToken BeginRequest()
    {
        _inFlight?.Cancel();
        _inFlight = new CancellationTokenSource();
        LoadingRing.IsActive = true;
        return _inFlight.Token;
    }

    private void EndRequest(CancellationToken ct)
    {
        // A newer request owns the ring and the slot if this one was superseded.
        if (_inFlight is { } cts && cts.Token == ct)
        {
            _inFlight = null;
            LoadingRing.IsActive = false;
        }
    }

    private void ShowDetails(PlaceDetails d)
    {
        _details = d;
        TitleText.Text = d.Name;
        AddressText.Text = d.Address;

        if (d.OpenNow is { } open)
        {
            OpenNowText.Text = open ? "Open now" : "Closed now";
            OpenNowText.Foreground = (Brush)Application.Current.Resources[open ? "SystemFillColorSuccessBrush" : "SystemFillColorCriticalBrush"];
            OpenNowText.Visibility = Visibility.Visible;
        }
        else
        {
            OpenNowText.Visibility = Visibility.Collapsed;
        }

        string? tel = PlaceFormatting.TelUri(d.InternationalPhone, d.NationalPhone);
        PhoneText.Text = d.NationalPhone ?? d.InternationalPhone ?? "No phone number listed";
        QrPanel.Visibility = CopyPhoneButton.Visibility = tel is null ? Visibility.Collapsed : Visibility.Visible;
        QrImage.Source = tel is null ? null : QrCodeRenderer.Render(tel);

        WebsiteLink.Visibility = d.Website is null ? Visibility.Collapsed : Visibility.Visible;
        WebsiteLink.Content = d.Website is null ? null : PlaceFormatting.WebsiteDisplay(d.Website);
        MapsLink.Visibility = d.MapsUri is null ? Visibility.Collapsed : Visibility.Visible;

        HoursPanel.Children.Clear();
        int today = PlaceFormatting.WeekdayIndex(DateTime.Now.DayOfWeek);
        for (int i = 0; i < d.WeekdayHours.Count; i++)
        {
            HoursPanel.Children.Add(new TextBlock
            {
                Text = d.WeekdayHours[i],
                FontWeight = i == today ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = (Brush)Application.Current.Resources[i == today ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap,
            });
        }

        NoHoursText.Visibility = d.WeekdayHours.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        SearchPanel.Visibility = Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Visible;
        BackButton.Visibility = Visibility.Visible;
        DetailPanel.ChangeView(0, 0, null, disableAnimation: true);
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        CancelInFlight();
        _details = null;
        QrImage.Source = null;
        DetailPanel.Visibility = Visibility.Collapsed;
        BackButton.Visibility = Visibility.Collapsed;
        SearchPanel.Visibility = Visibility.Visible;
        TitleText.Text = App.DisplayName;
        StatusBar.IsOpen = false;
    }

    private void CopyPhoneButton_Click(object sender, RoutedEventArgs e)
    {
        if (_details is not { } d || (d.NationalPhone ?? d.InternationalPhone) is not { } phone)
        {
            return;
        }

        DataPackage package = new();
        package.SetText(phone);
        Clipboard.SetContent(package);
        CopyPhoneButton.Content = "Copied";
        var reset = DispatcherQueue.CreateTimer();
        reset.Interval = TimeSpan.FromSeconds(1.5);
        reset.IsRepeating = false;
        reset.Tick += (_, _) => CopyPhoneButton.Content = "Copy number";
        reset.Start();
    }

    // Only web links open from here: a place's website field is third-party data.
    private async void WebsiteLink_Click(object sender, RoutedEventArgs e)
    {
        if (Uri.TryCreate(_details?.Website, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https")
        {
            await Launcher.LaunchUriAsync(uri);
        }
    }

    private async void MapsLink_Click(object sender, RoutedEventArgs e)
    {
        if (Uri.TryCreate(_details?.MapsUri, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https")
        {
            await Launcher.LaunchUriAsync(uri);
        }
    }

    private void ShowError(Exception ex)
    {
        bool online = ConnectivityService.IsInternetAvailable;
        StatusBar.Title = ex switch
        {
            LocationUnavailableException => "Location unavailable",
            PlacesApiException => "Google Places error",
            _ => online ? "Can't reach Google Places" : "No internet connection",
        };
        StatusBar.Message = ex is HttpRequestException or TaskCanceledException
            ? online ? "Try again in a moment." : "Search again when you're back online."
            : ex.Message;
        StatusBar.IsOpen = true;
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => App.Current.ShowSettings();
}
