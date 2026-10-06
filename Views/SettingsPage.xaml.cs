using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuickPlaces.Services;
using Windows.ApplicationModel;

namespace QuickPlaces.Views;

/// <summary>Settings, shown inside the flyout in place of the search page.</summary>
public sealed partial class SettingsPage : Page
{
    private readonly Action _goBack;
    private readonly PlacesClient _client = new();
    private bool _loading = true;

    public SettingsPage(Action goBack)
    {
        _goBack = goBack;
        InitializeComponent();
        VersionText.Text = $"{App.DisplayName} {GetVersion()}";
    }

    /// <summary>Shown: re-read startup state, since it can change in Windows Settings.</summary>
    public void OnShown()
    {
        _ = LoadStartupStateAsync();
        ShowLocationState();
        BackButton.Focus(FocusState.Programmatic);
    }

    private static string GetVersion()
    {
        try
        {
            PackageVersion v = Package.Current.Id.Version;
            return $"{v.Major}.{v.Minor}.{v.Build}";
        }
        catch
        {
            return "(unpackaged)";
        }
    }

    private async Task LoadStartupStateAsync() => ShowStartupState(await StartupService.GetStateAsync());

    private void ShowStartupState(StartupTaskState? state)
    {
        _loading = true;
        StartupToggle.IsOn = state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;

        // The user (Task Manager, Settings > Apps > Startup) or policy has the final say; the app
        // can't override it, so say where to change it instead of offering a dead toggle.
        StartupToggle.IsEnabled = state is StartupTaskState.Enabled or StartupTaskState.Disabled;
        StartupDescription.Text = state switch
        {
            StartupTaskState.DisabledByUser => "Turned off in Settings > Apps > Startup. Turn it on there.",
            StartupTaskState.DisabledByPolicy or StartupTaskState.EnabledByPolicy => "Managed by your organization.",
            null => "Only available when the app is installed.",
            _ => "Keep the tray icon ready after you sign in.",
        };
        _loading = false;
    }

    private async void StartupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        ShowStartupState(await StartupService.SetEnabledAsync(StartupToggle.IsOn));
    }

    // ---- Location ----------------------------------------------------------------------------

    private void ShowLocationState()
    {
        _loading = true;
        string? mode = SettingsService.LocationMode ?? (LocationService.SavedPoint is not null ? "zip" : null);
        LocationChoice.SelectedItem = mode switch { "device" => DeviceOption, "zip" => ZipOption, _ => null };
        ZipEntry.Visibility = mode == "zip" ? Visibility.Visible : Visibility.Collapsed;
        ZipBox.Text = SettingsService.ManualPostalCode ?? "";
        LocationError.Visibility = Visibility.Collapsed;
        LocationDescription.Text = mode switch
        {
            "device" => "Searching near your device's location.",
            "zip" => $"Searching near {SettingsService.ManualPostalCode}.",
            _ => "Not set yet. Choose how to find places near you.",
        };
        _loading = false;
    }

    private void ShowLocationError(string message)
    {
        LocationError.Text = message;
        LocationError.Visibility = Visibility.Visible;
    }

    private async void LocationChoice_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        if (LocationChoice.SelectedItem == ZipOption)
        {
            ZipEntry.Visibility = Visibility.Visible;
            LocationError.Visibility = Visibility.Collapsed;
            ZipBox.Focus(FocusState.Programmatic);
        }
        else if (LocationChoice.SelectedItem == DeviceOption)
        {
            // Asking now is fine: the user just picked this, so the Windows prompt isn't a surprise.
            if (await LocationService.RequestAccessAsync(CancellationToken.None))
            {
                LocationService.UseDevice();
                ShowLocationState();
            }
            else
            {
                ShowLocationState();
                ShowLocationError("Location is off for this app. Turn it on in Windows Settings > Privacy > Location, or use a zip code.");
            }
        }
    }

    private void ZipBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            ZipApply_Click(sender, e);
        }
    }

    private async void ZipApply_Click(object sender, RoutedEventArgs e)
    {
        string zip = ZipBox.Text.Trim();
        if (zip.Length < 3)
        {
            ShowLocationError("Enter your zip or postal code.");
            return;
        }

        ZipApply.IsEnabled = false;
        LocationError.Visibility = Visibility.Collapsed;
        try
        {
            GeoPoint? point = await _client.GeocodeAsync(zip, CancellationToken.None);
            if (point is not { } p)
            {
                ShowLocationError($"Couldn't find “{zip}”. Check it and try again.");
                return;
            }

            LocationService.SaveManual(zip, p);
            ShowLocationState();
        }
        catch (Exception ex)
        {
            ShowLocationError(ex is PlacesApiException ? ex.Message : ConnectivityService.IsInternetAvailable ? "Try again in a moment." : "No internet connection.");
        }
        finally
        {
            ZipApply.IsEnabled = true;
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) => _goBack();
}
