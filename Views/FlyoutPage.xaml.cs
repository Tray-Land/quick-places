using System.Globalization;
using System.Net.Http;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using QuickPlaces.Controls;
using QuickPlaces.Models;
using QuickPlaces.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using DispatcherQueuePriority = Microsoft.UI.Dispatching.DispatcherQueuePriority;
using DispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;

namespace QuickPlaces.Views;

/// <summary>
/// Search nearby, pick a place, see its hours, rating, website and phone, and scan a QR code to
/// call it from a phone. Nothing runs in the background: a request happens only when the user
/// searches or opens a place, and a request still in flight is cancelled when the flyout hides
/// (and picked up again when it reopens).
/// </summary>
public sealed partial class FlyoutPage : Page, IDisposable
{
    private const string ForwardAnimationKey = "PlaceForward";
    private const string BackAnimationKey = "PlaceBack";

    private enum SearchState { Start, Loading, Results, Empty }

    private readonly PlacesClient _client = new();
    private readonly HashSet<UIElement> _springWired = [];
    private CancellationTokenSource? _inFlight;
    private Func<Task>? _currentRequest;
    private Func<Task>? _resumeOnShow;
    private SearchState _searchState = SearchState.Start;
    private bool _animateNewRows;
    private bool _hasShown;
    private PlaceSummary? _selected;
    private PlaceDetails? _details;
    private IReadOnlyList<PlaceSummary> _results = [];

    public FlyoutPage()
    {
        InitializeComponent();
        TitleText.Text = App.DisplayName;
        QuickRepeater.ItemsSource = PlaceCategories.Quick;
        RefreshRecent();
        ApplyResultsView();

        Motion.AddPointerSpring(SettingsButton, SettingsGlyph, 1.15f, 0.9f);
        foreach (Button action in new[] { DirectionsButton, WebsiteButton, CopyPhoneButton, CopyAddressButton })
        {
            Motion.AddPointerSpring(action, ((StackPanel)action.Content).Children[0]);
        }

        if (Secrets.GooglePlacesApiKey is null)
        {
            MissingKeyPanel.Visibility = Visibility.Visible;
        }
    }

    private bool IsShowingDetails => DetailView.Visibility == Visibility.Visible;

    /// <summary>The flyout opened: focus the search box and finish anything the last hide interrupted.</summary>
    public void OnShown()
    {
        if (MissingKeyPanel.Visibility == Visibility.Visible)
        {
            Motion.StaggerIn(MissingKeyPanel.Children);
            return;
        }

        if (!_hasShown)
        {
            _hasShown = true;
            _ = CheckLocationAsync();
        }
        else if (ChoicePanel.Visibility == Visibility.Visible && SettingsService.LocationMode is not null)
        {
            _ = CheckLocationAsync(); // chosen in Settings
        }
        else if (SearchView.Visibility == Visibility.Visible)
        {
            ShowSearchView(usingZip: SettingsService.LocationMode != "device"); // may have changed in Settings
        }

        if (_resumeOnShow is { } resume)
        {
            _resumeOnShow = null;
            _ = resume();
        }

        FocusDefault();
    }

    /// <summary>Puts focus where the user will act: the search box, or the back button on a place.</summary>
    public void FocusDefault()
    {
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (IsShowingDetails)
            {
                BackButton.Focus(FocusState.Programmatic);
            }
            else if (SearchView.Visibility == Visibility.Visible)
            {
                SearchBox.Focus(FocusState.Programmatic);
            }
            else if (LocationPanel.Visibility == Visibility.Visible)
            {
                ZipBox.Focus(FocusState.Programmatic);
            }
        });
    }

    // ---- Location ----------------------------------------------------------------------------

    /// <summary>
    /// First thing the flyout does: with a saved choice, go straight to search; otherwise let the user
    /// pick device location or a zip code, so Windows' permission prompt never appears unannounced.
    /// </summary>
    private async Task CheckLocationAsync()
    {
        string? mode = SettingsService.LocationMode;
        if (mode is null && LocationService.SavedPoint is not null)
        {
            mode = "zip";
        }

        if (mode == "device")
        {
            if (await LocationService.RequestAccessAsync(CancellationToken.None))
            {
                ShowSearchView(usingZip: false);
                return;
            }

            if (LocationService.SavedPoint is not null)
            {
                ShowSearchView(usingZip: true);
                return;
            }

            ShowLocationPanel();
        }
        else if (mode == "zip" && LocationService.SavedPoint is not null)
        {
            ShowSearchView(usingZip: true);
        }
        else if (mode == "zip")
        {
            ShowLocationPanel();
        }
        else
        {
            ShowChoicePanel();
        }
    }

    private void ShowSearchView(bool usingZip)
    {
        ChoicePanel.Visibility = Visibility.Collapsed;
        LocationPanel.Visibility = Visibility.Collapsed;
        SearchView.Visibility = Visibility.Visible;
        ZipNote.Visibility = usingZip ? Visibility.Visible : Visibility.Collapsed;
        ZipNoteText.Text = $"Searching near {SettingsService.ManualPostalCode}";
        Motion.StaggerIn([.. StartStack.Children], startDelayMs: 60, stepMs: 60);
        FocusDefault();
    }

    private void ShowChoicePanel()
    {
        CancelInFlight();
        SearchView.Visibility = Visibility.Collapsed;
        LocationPanel.Visibility = Visibility.Collapsed;
        ChoicePanel.Visibility = Visibility.Visible;
        ChoiceError.Visibility = Visibility.Collapsed;
        Motion.StaggerIn(ChoicePanel.Children, startDelayMs: 40, stepMs: 50);
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => UseLocationButton.Focus(FocusState.Programmatic));
    }

    private void ShowLocationPanel()
    {
        CancelInFlight();
        SearchView.Visibility = Visibility.Collapsed;
        ChoicePanel.Visibility = Visibility.Collapsed;
        LocationPanel.Visibility = Visibility.Visible;
        ZipBox.Text = SettingsService.ManualPostalCode ?? "";
        ZipError.Visibility = Visibility.Collapsed;
        Motion.StaggerIn(LocationPanel.Children, startDelayMs: 40, stepMs: 50);
        FocusDefault();
    }

    private void ChangeZip_Click(object sender, RoutedEventArgs e) => ShowLocationPanel();

    private void ChooseZip_Click(object sender, RoutedEventArgs e) => ShowLocationPanel();

    private async void UseLocation_Click(object sender, RoutedEventArgs e)
    {
        if (await LocationService.RequestAccessAsync(CancellationToken.None))
        {
            LocationService.UseDevice();
            ShowSearchView(usingZip: false);
        }
        else
        {
            ChoiceError.Text = "Location is off for this app. Turn it on in Windows Settings > Privacy > Location, or enter a zip code.";
            ChoiceError.Visibility = Visibility.Visible;
        }
    }

    private async void RetryLocation_Click(object sender, RoutedEventArgs e)
    {
        if (await LocationService.RequestAccessAsync(CancellationToken.None))
        {
            LocationService.UseDevice();
            ShowSearchView(usingZip: false);
        }
        else
        {
            ShowZipError("Location is off for this app. Turn it on in Windows Settings > Privacy > Location, or enter a zip code.");
        }
    }

    private void ZipBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            _ = SubmitZipAsync();
        }
    }

    private void ZipButton_Click(object sender, RoutedEventArgs e) => _ = SubmitZipAsync();

    private void ShowZipError(string message)
    {
        ZipError.Text = message;
        ZipError.Visibility = Visibility.Visible;
    }

    private async Task SubmitZipAsync()
    {
        string zip = ZipBox.Text.Trim();
        if (zip.Length < 3)
        {
            ShowZipError("Enter your zip or postal code.");
            return;
        }

        CancellationToken ct = BeginRequest(() => SubmitZipAsync());
        ZipButton.IsEnabled = false;
        ZipError.Visibility = Visibility.Collapsed;
        try
        {
            GeoPoint? point = await _client.GeocodeAsync(zip, ct);
            ct.ThrowIfCancellationRequested();
            if (point is not { } p)
            {
                ShowZipError($"Couldn't find “{zip}”. Check it and try again.");
                return;
            }

            LocationService.SaveManual(zip, p);
            ShowSearchView(usingZip: true);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ShowZipError(ex is PlacesApiException ? ex.Message : ConnectivityService.IsInternetAvailable ? "Try again in a moment." : "No internet connection.");
        }
        finally
        {
            ZipButton.IsEnabled = true;
            EndRequest(ct);
        }
    }

    /// <summary>The flyout hid: abandon any request in flight, and remember to retry it on the next show.</summary>
    public void OnHidden()
    {
        if (_inFlight is not null)
        {
            _resumeOnShow = _currentRequest;
        }

        CancelInFlight();
        Motion.StopShimmer(SkeletonPanel);
        Motion.StopShimmer(DetailSkeleton);
        MapHoverCard.Visibility = Visibility.Collapsed;
    }

    public void Dispose()
    {
        _resumeOnShow = null;
        CancelInFlight();
        DisposeMap();
    }

    /// <summary>Escape and the back button: leave the detail view. False when there's nowhere to go back to.</summary>
    public bool TryGoBack()
    {
        if (!IsShowingDetails)
        {
            return false;
        }

        _ = GoBackToResultsAsync();
        return true;
    }

    private void CancelInFlight()
    {
        _inFlight?.Cancel();
        _inFlight = null;
        _currentRequest = null;
    }

    private CancellationToken BeginRequest(Func<Task> request)
    {
        _inFlight?.Cancel();
        _inFlight = new CancellationTokenSource();
        _currentRequest = request;
        _resumeOnShow = null;
        StatusBar.IsOpen = false;
        return _inFlight.Token;
    }

    private void EndRequest(CancellationToken ct)
    {
        // A newer request owns the slot if this one was superseded.
        if (_inFlight is { } cts && cts.Token == ct)
        {
            _inFlight = null;
            _currentRequest = null;
        }
    }

    // ---- Search ------------------------------------------------------------------------------

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args) =>
        _ = SearchAsync(args.QueryText);

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        // Clearing the box (the X, or deleting everything) goes back to the start screen.
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && sender.Text.Length == 0 && _searchState != SearchState.Start)
        {
            CancelInFlight();
            _results = [];
            ResultsList.ItemsSource = null;
            SetSearchState(SearchState.Start);
        }
    }

    private void QuickTile_Click(object sender, RoutedEventArgs e) => RunSearchFrom(sender);

    private void RecentItem_Click(object sender, RoutedEventArgs e) => RunSearchFrom(sender);

    private void RunSearchFrom(object sender)
    {
        if ((sender as FrameworkElement)?.Tag is string query)
        {
            SearchBox.Text = query;
            _ = SearchAsync(query);
        }
    }

    private void QuickTile_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Content: StackPanel panel } button && _springWired.Add(button))
        {
            Motion.AddPointerSpring(button, panel.Children[0], 1.1f, 0.9f);
        }
    }

    private void ClearRecent_Click(object sender, RoutedEventArgs e)
    {
        SettingsService.RecentSearches = [];
        RefreshRecent();
        SearchBox.Focus(FocusState.Programmatic);
    }

    private void RefreshRecent()
    {
        IReadOnlyList<string> recent = SettingsService.RecentSearches;
        RecentRepeater.ItemsSource = recent;
        RecentPanel.Visibility = recent.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task SearchAsync(string text)
    {
        string query = text.Trim();
        if (query.Length == 0)
        {
            return;
        }

        CancellationToken ct = BeginRequest(() => SearchAsync(query));
        SettingsService.RecentSearches = PlaceFormatting.PushRecent(SettingsService.RecentSearches, query);
        RefreshRecent();
        SetSearchState(SearchState.Loading);

        try
        {
            GeoPoint origin = await LocationService.GetAsync(ct);
            IReadOnlyList<PlaceSummary> results = await _client.SearchAsync(query, origin, ct);
            ct.ThrowIfCancellationRequested();

            _results = results;
            if (results.Count == 0)
            {
                EmptyText.Text = $"Nothing found for “{query}” nearby";
                SetSearchState(SearchState.Empty);
            }
            else
            {
                ResultsHeader.Text = results.Count == 1 ? $"1 place for “{query}”" : $"{results.Count} places for “{query}”";
                _animateNewRows = Motion.IsEnabled;
                ResultsList.ItemsSource = results;
                ResultsList.ScrollIntoView(results[0]);
                SetSearchState(SearchState.Results);
                OnResultsChanged(origin);

                // Rows realized by this first layout get the staggered entrance; rows scrolled
                // into view later just appear.
                DispatcherQueueTimer stopAnimating = DispatcherQueue.CreateTimer();
                stopAnimating.Interval = TimeSpan.FromMilliseconds(400);
                stopAnimating.IsRepeating = false;
                stopAnimating.Tick += (_, _) => _animateNewRows = false;
                stopAnimating.Start();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (LocationUnavailableException)
        {
            SetSearchState(SearchState.Start);
            ShowLocationPanel();
        }
        catch (Exception ex)
        {
            ShowError(ex);
            SetSearchState(_results.Count > 0 ? SearchState.Results : SearchState.Start);
        }
        finally
        {
            EndRequest(ct);
        }
    }

    private void SetSearchState(SearchState state)
    {
        SearchState previous = _searchState;
        _searchState = state;

        StartPanel.Visibility = state == SearchState.Start ? Visibility.Visible : Visibility.Collapsed;
        SkeletonPanel.Visibility = state == SearchState.Loading ? Visibility.Visible : Visibility.Collapsed;
        ResultsPanel.Visibility = state == SearchState.Results ? Visibility.Visible : Visibility.Collapsed;
        EmptyPanel.Visibility = state == SearchState.Empty ? Visibility.Visible : Visibility.Collapsed;

        if (state == SearchState.Loading)
        {
            Motion.StaggerIn(SkeletonPanel.Children, stepMs: 30, distance: 8);
            Motion.StartShimmer(SkeletonPanel);
        }
        else
        {
            Motion.StopShimmer(SkeletonPanel);
        }

        if (state == previous)
        {
            return;
        }

        switch (state)
        {
            case SearchState.Start:
                Motion.StaggerIn([.. StartStack.Children], stepMs: 50);
                break;
            case SearchState.Results:
                Motion.SlideIn(ResultsHeader, TimeSpan.Zero, distance: 6);
                break;
            case SearchState.Empty:
                Motion.PopIn(EmptyBadge);
                Motion.StaggerIn(EmptyPanel.Children.Skip(1), startDelayMs: 80);
                break;
        }
    }

    private void ResultsList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue || args.Phase != 0)
        {
            return;
        }

        SelectorItem container = args.ItemContainer;
        if (_springWired.Add(container) && container.ContentTemplateRoot is Grid { Children: [UIElement tile, ..] })
        {
            Motion.AddPointerSpring(container, tile, 1.1f, 0.94f);
        }

        if (_animateNewRows)
        {
            Motion.SlideIn(container, TimeSpan.FromMilliseconds(Math.Min(args.ItemIndex, 10) * 35), distance: 20);
        }
        else
        {
            Motion.Reset(container);
        }
    }

    // ---- Details -----------------------------------------------------------------------------

    private void ResultsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PlaceSummary place)
        {
            OpenPlace(place);
        }
    }

    private void OpenPlace(PlaceSummary place)
    {
        _selected = place;
        _details = null;

        if (Motion.IsEnabled && !_showMap)
        {
            ResultsList.PrepareConnectedAnimation(ForwardAnimationKey, place, "IconTile");
        }

        // The hero fills in from the search row right away; the rest arrives with the details.
        HeroTile.Background = PlaceVisuals.CategoryBrush(place.Category);
        HeroGlyph.Glyph = place.Glyph;
        HeroName.Text = place.Name;
        HeroSubtitle.Text = JoinDot(place.TypeLabel, place.DistanceText);
        AddressText.Text = place.Address;
        SetDetailSectionsVisible(false);

        SearchView.Visibility = Visibility.Collapsed;
        DetailView.Visibility = Visibility.Visible;
        DetailView.ChangeView(0, 0, null, disableAnimation: true);
        ShowBackButton(true);

        ConnectedAnimation? forward = ConnectedAnimationService.GetForCurrentView().GetAnimation(ForwardAnimationKey);
        if (forward is not null)
        {
            forward.Configuration = new GravityConnectedAnimationConfiguration();
            forward.TryStart(HeroTile, [HeroText]);
        }
        else
        {
            Motion.StaggerIn([HeroTile, HeroText]);
        }

        _ = LoadDetailsAsync(place);
    }

    private async Task LoadDetailsAsync(PlaceSummary place)
    {
        CancellationToken ct = BeginRequest(() => LoadDetailsAsync(place));
        Task<PlaceDetails> load = _client.GetDetailsAsync(place.Id, ct);

        // Cached details are already done: skip the skeleton and let the sections follow the hero.
        if (!load.IsCompleted)
        {
            DetailSkeleton.Visibility = Visibility.Visible;
            Motion.SlideIn(DetailSkeleton, TimeSpan.FromMilliseconds(120), distance: 8);
            Motion.StartShimmer(DetailSkeleton);
        }

        try
        {
            PlaceDetails details = await load;
            if (ct.IsCancellationRequested || _selected != place)
            {
                return;
            }

            ShowDetails(details, place);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (_selected == place)
            {
                Motion.StopShimmer(DetailSkeleton);
                DetailSkeleton.Visibility = Visibility.Collapsed;
                ShowError(ex);
            }
        }
        finally
        {
            EndRequest(ct);
        }
    }

    private void ShowDetails(PlaceDetails d, PlaceSummary place)
    {
        _details = d;
        Motion.StopShimmer(DetailSkeleton);
        DetailSkeleton.Visibility = Visibility.Collapsed;

        HeroName.Text = d.Name.Length > 0 ? d.Name : place.Name;
        HeroSubtitle.Text = JoinDot(d.TypeLabel ?? place.TypeLabel, place.DistanceText);
        AddressText.Text = d.Address.Length > 0 ? d.Address : place.Address;

        // Open now + today's hours
        string? today = PlaceFormatting.TodayHours(d.WeekdayHours, DateTime.Now.DayOfWeek);
        bool closingSoon = PlaceFormatting.IsClosingSoon(d.OpenNow, d.NextCloseTime, DateTimeOffset.UtcNow);
        OpenPill.Visibility = d.OpenNow == true && !closingSoon ? Visibility.Visible : Visibility.Collapsed;
        ClosingSoonPill.Visibility = closingSoon ? Visibility.Visible : Visibility.Collapsed;
        ClosedPill.Visibility = d.OpenNow == false ? Visibility.Visible : Visibility.Collapsed;

        TodayHoursText.Text = today is null ? "" : $"Today {today}";
        StatusRow.Visibility = d.OpenNow is not null || today is not null ? Visibility.Visible : Visibility.Collapsed;

        // Rating and price
        StarsPanel.Children.Clear();
        if (d.Rating is { } rating)
        {
            RatingText.Text = rating.ToString("0.0", CultureInfo.CurrentCulture);
            foreach (int fill in PlaceFormatting.StarFills(rating))
            {
                StarsPanel.Children.Add(CreateStar(fill));
            }

            RatingCountText.Text = d.UserRatingCount is { } count ? $"({count.ToString("N0", CultureInfo.CurrentCulture)})" : "";
        }

        string? price = PlaceFormatting.PriceText(d.PriceLevel, RegionInfo.CurrentRegion.CurrencySymbol);
        PriceText.Text = price is null ? "" : d.Rating is null ? price : $"·  {price}";
        RatingText.Visibility = StarsPanel.Visibility = RatingCountText.Visibility = d.Rating is null ? Visibility.Collapsed : Visibility.Visible;
        RatingRow.Visibility = d.Rating is not null || price is not null ? Visibility.Visible : Visibility.Collapsed;

        // Actions
        DirectionsButton.IsEnabled = IsWebLink(d.MapsUri);
        WebsiteButton.IsEnabled = IsWebLink(d.Website);
        ToolTipService.SetToolTip(WebsiteButton, d.Website is null ? "No website listed" : PlaceFormatting.WebsiteDisplay(d.Website));
        CopyPhoneButton.IsEnabled = (d.NationalPhone ?? d.InternationalPhone) is not null;

        // Phone + QR
        string? tel = PlaceFormatting.TelUri(d.InternationalPhone, d.NationalPhone);
        PhoneText.Text = d.NationalPhone ?? d.InternationalPhone ?? "";
        QrImage.Source = tel is null ? null : QrCodeRenderer.Render(tel);

        // Hours table, today highlighted
        HoursPanel.Children.Clear();
        int todayIndex = PlaceFormatting.WeekdayIndex(DateTime.Now.DayOfWeek);
        for (int i = 0; i < d.WeekdayHours.Count; i++)
        {
            HoursPanel.Children.Add(CreateHoursRow(d.WeekdayHours[i], i == todayIndex));
        }

        NoHoursText.Visibility = d.WeekdayHours.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        SetDetailSectionsVisible(true);
        PhoneCard.Visibility = tel is null ? Visibility.Collapsed : Visibility.Visible;
        Motion.StaggerIn([StatusRow, RatingRow, ActionBar, PhoneCard, HoursCard, AddressCard], startDelayMs: 140, stepMs: 45);
        if (PhoneCard.Visibility == Visibility.Visible)
        {
            Motion.PopIn(QrFrame, TimeSpan.FromMilliseconds(320));
        }
    }

    private void SetDetailSectionsVisible(bool visible)
    {
        Visibility v = visible ? Visibility.Visible : Visibility.Collapsed;
        StatusRow.Visibility = RatingRow.Visibility = ActionBar.Visibility = PhoneCard.Visibility = HoursCard.Visibility = AddressCard.Visibility = v;
        if (!visible)
        {
            QrImage.Source = null;
            DetailSkeleton.Visibility = Visibility.Collapsed;
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) => TryGoBack();

    private async Task GoBackToResultsAsync()
    {
        CancelInFlight();
        Motion.StopShimmer(DetailSkeleton);
        StatusBar.IsOpen = false;
        PlaceSummary? place = _selected;
        _selected = null;
        _details = null;

        ConnectedAnimation? back = null;
        if (Motion.IsEnabled && !_showMap && place is not null && _results.Contains(place))
        {
            back = ConnectedAnimationService.GetForCurrentView().PrepareToAnimate(BackAnimationKey, HeroTile);
            back.Configuration = new DirectConnectedAnimationConfiguration();
        }

        DetailView.Visibility = Visibility.Collapsed;
        SetDetailSectionsVisible(false);
        SearchView.Visibility = Visibility.Visible;
        ShowBackButton(false);
        Motion.SlideIn(SearchBox, TimeSpan.Zero, distance: 0, fromX: 0);

        if (back is not null && place is not null)
        {
            ResultsList.ScrollIntoView(place);
            ResultsList.UpdateLayout();
            if (!await ResultsList.TryStartConnectedAnimationAsync(back, place, "IconTile"))
            {
                back.Cancel();
            }

            if (ResultsList.ContainerFromItem(place) is ListViewItem item)
            {
                item.Focus(FocusState.Programmatic);
            }
        }
    }

    private void ShowBackButton(bool show)
    {
        BackButton.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (show)
        {
            Motion.SlideIn(BackButton, TimeSpan.Zero, distance: 0, fromX: 12);
        }
    }

    // ---- Actions -----------------------------------------------------------------------------

    private void CopyPhoneButton_Click(object sender, RoutedEventArgs e)
    {
        if (_details is { } d && (d.NationalPhone ?? d.InternationalPhone) is { } phone)
        {
            CopyWithFeedback(phone, CopyPhoneGlyph, CopyPhoneLabel, "\uE717", "Phone");
        }
    }

    private void CopyAddressButton_Click(object sender, RoutedEventArgs e)
    {
        if (AddressText.Text is { Length: > 0 } address)
        {
            CopyWithFeedback(address, CopyAddressGlyph, CopyAddressLabel, "\uE8C8", "Address");
        }
    }

    /// <summary>Copies, then flips the button to a check mark and "Copied" for a moment.</summary>
    private void CopyWithFeedback(string text, FontIcon glyph, TextBlock label, string restoreGlyph, string restoreLabel)
    {
        DataPackage package = new();
        package.SetText(text);
        Clipboard.SetContent(package);

        glyph.Glyph = "\uE8FB";
        label.Text = "Copied";
        Motion.PopIn(glyph);

        DispatcherQueueTimer reset = DispatcherQueue.CreateTimer();
        reset.Interval = TimeSpan.FromSeconds(1.5);
        reset.IsRepeating = false;
        reset.Tick += (_, _) =>
        {
            glyph.Glyph = restoreGlyph;
            label.Text = restoreLabel;
            Motion.PopIn(glyph);
        };
        reset.Start();
    }

    // Only web links open from here: a place's website field is third-party data.
    private async void WebsiteButton_Click(object sender, RoutedEventArgs e) => await LaunchWebLinkAsync(_details?.Website);

    private async void DirectionsButton_Click(object sender, RoutedEventArgs e) => await LaunchWebLinkAsync(_details?.MapsUri);

    private static async Task LaunchWebLinkAsync(string? link)
    {
        if (IsWebLink(link))
        {
            await Launcher.LaunchUriAsync(new Uri(link!));
        }
    }

    private static bool IsWebLink(string? link) =>
        Uri.TryCreate(link, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https";

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => App.Current.ShowSettings();

    // ---- Building blocks ---------------------------------------------------------------------

    private UIElement CreateStar(int fill)
    {
        Grid star = new();
        star.Children.Add(new FontIcon { Style = PageStyle("StarOutlineStyle") });
        if (fill > 0)
        {
            star.Children.Add(new FontIcon { Glyph = fill == 2 ? "\uE735" : "\uE7C6", Style = PageStyle("StarFillStyle") });
        }

        return star;
    }

    private UIElement CreateHoursRow(string description, bool isToday)
    {
        (string day, string hours) = PlaceFormatting.SplitWeekday(description);
        Grid row = new() { Style = PageStyle(isToday ? "HoursTodayRowStyle" : "HoursRowStyle") };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        TextBlock dayText = new() { Text = day, Style = PageStyle(isToday ? "HoursTodayDayStyle" : "HoursDayStyle") };
        TextBlock hoursText = new() { Text = hours, Style = PageStyle(isToday ? "HoursTodayTextStyle" : "HoursTextStyle") };
        Grid.SetColumn(hoursText, 1);
        row.Children.Add(dayText);
        row.Children.Add(hoursText);
        return row;
    }

    private static string JoinDot(string? first, string? second) =>
        string.Join("  ·  ", new[] { first, second }.Where(s => !string.IsNullOrEmpty(s)));

    private Style PageStyle(string key) => (Style)Resources[key];

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
}
