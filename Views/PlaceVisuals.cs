using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using QuickPlaces.Models;
using Windows.Foundation;
using Windows.UI;

namespace QuickPlaces.Views;

/// <summary>
/// The colored tile behind each place's symbol, used from x:Bind. Each category gets its own hue
/// as a soft top-to-bottom gradient, carrying a white glyph. These are the one place the app uses
/// fixed colors instead of theme brushes: they're mid-tone, so the white glyph reads on light and
/// dark backdrops alike, and the tile sits on its own rather than tinting text.
/// </summary>
internal static class PlaceVisuals
{
    public static Brush CategoryBrush(PlaceCategory category)
    {
        (uint top, uint bottom) = category switch
        {
            PlaceCategory.Food => (0xFFFB8C3Cu, 0xFFEF6C00u),
            PlaceCategory.Cafe => (0xFFA67C5Bu, 0xFF7B5440u),
            PlaceCategory.Bar => (0xFF8C6CF0u, 0xFF6A4AD6u),
            PlaceCategory.Grocery => (0xFF43C46Bu, 0xFF23A04Cu),
            PlaceCategory.Shopping => (0xFFF06292u, 0xFFD81B60u),
            PlaceCategory.Health => (0xFFEF5350u, 0xFFC62828u),
            PlaceCategory.Lodging => (0xFF6C74F0u, 0xFF4A50D0u),
            PlaceCategory.Fuel => (0xFF42A5F5u, 0xFF1E7FD8u),
            PlaceCategory.EvCharging => (0xFF26C6B0u, 0xFF00998Au),
            PlaceCategory.Automotive => (0xFF6A8CC8u, 0xFF4A6AA8u),
            PlaceCategory.Parking => (0xFF5C9CE6u, 0xFF3A78C4u),
            PlaceCategory.Finance => (0xFF2BB58Au, 0xFF138A66u),
            PlaceCategory.Education => (0xFFF5A524u, 0xFFD9820Au),
            PlaceCategory.Library => (0xFFC49A5Au, 0xFFA0763Au),
            PlaceCategory.Entertainment => (0xFFD466D8u, 0xFFAE3FB4u),
            PlaceCategory.Outdoors => (0xFF8CC152u, 0xFF5E9A2Cu),
            PlaceCategory.Fitness => (0xFF29B6D6u, 0xFF0A90B0u),
            PlaceCategory.Beauty => (0xFFF48AB0u, 0xFFD9608Au),
            PlaceCategory.Transit => (0xFF5778C9u, 0xFF3A57A8u),
            PlaceCategory.Airport => (0xFF4FB3F0u, 0xFF2489CFu),
            PlaceCategory.Services => (0xFF78909Cu, 0xFF546E7Au),
            PlaceCategory.Worship => (0xFF9575CDu, 0xFF6F4FB0u),
            _ => (0xFF8A95A8u, 0xFF5E6878u),
        };

        return new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0.4, 1),
            GradientStops =
            {
                new GradientStop { Color = ToColor(top), Offset = 0 },
                new GradientStop { Color = ToColor(bottom), Offset = 1 },
            },
        };
    }

    public static Visibility VisibleIf(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility VisibleIfText(string? value) => string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;

    private static Color ToColor(uint argb) =>
        ColorHelper.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
}
