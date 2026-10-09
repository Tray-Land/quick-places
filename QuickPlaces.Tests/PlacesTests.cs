using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using QuickPlaces.Models;
using QuickPlaces.Services;

namespace QuickPlaces.Tests;

[TestClass]
public class PlacesTests
{
    [TestMethod]
    public void ParseSearch_ReadsPlaces_AndSkipsIncompleteOnes()
    {
        const string json = """
            {"places":[
              {"id":"a","displayName":{"text":"Joe's Pizza"},"shortFormattedAddress":"1 Main St","location":{"latitude":43.0,"longitude":-87.9}},
              {"displayName":{"text":"No id"}}
            ]}
            """;
        var results = PlacesParser.ParseSearch(json);
        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Joe's Pizza", results[0].Name);
        Assert.AreEqual(43.0, results[0].Latitude);
    }

    [TestMethod]
    public void ParseSearch_EmptyResponse_ReturnsNothing() =>
        Assert.AreEqual(0, PlacesParser.ParseSearch("{}").Count);

    [TestMethod]
    public void ParseDetails_ReadsContactAndHours()
    {
        const string json = """
            {"id":"a","displayName":{"text":"Joe's"},"formattedAddress":"1 Main St",
             "nationalPhoneNumber":"(414) 555-0100","internationalPhoneNumber":"+1 414-555-0100",
             "websiteUri":"https://www.joes.example/","googleMapsUri":"https://maps.google.com/?cid=1",
             "currentOpeningHours":{"openNow":false},
             "regularOpeningHours":{"openNow":true,"weekdayDescriptions":["Monday: 9 AM – 5 PM","Tuesday: Closed"]}}
            """;
        var d = PlacesParser.ParseDetails(json);
        Assert.AreEqual("(414) 555-0100", d.NationalPhone);
        Assert.AreEqual(2, d.WeekdayHours.Count);
        Assert.AreEqual(false, d.OpenNow, "current hours win over regular hours");
    }

    [TestMethod]
    public void ParseDetails_MissingOptionalFields_AreNull()
    {
        var d = PlacesParser.ParseDetails("""{"id":"a","displayName":{"text":"X"}}""");
        Assert.IsNull(d.NationalPhone);
        Assert.IsNull(d.Website);
        Assert.IsNull(d.OpenNow);
        Assert.AreEqual(0, d.WeekdayHours.Count);
    }

    [TestMethod]
    public void ParseErrorMessage_ReadsGoogleErrorBody()
    {
        Assert.AreEqual("API key not valid.", PlacesParser.ParseErrorMessage("""{"error":{"code":400,"message":"API key not valid."}}"""));
        Assert.IsNull(PlacesParser.ParseErrorMessage("not json"));
    }

    [TestMethod]
    public void TelUri_PrefersInternational_AndStripsFormatting()
    {
        Assert.AreEqual("tel:+14145550100", PlaceFormatting.TelUri("+1 414-555-0100", "(414) 555-0100"));
        Assert.AreEqual("tel:4145550100", PlaceFormatting.TelUri(null, "(414) 555-0100"));
        Assert.IsNull(PlaceFormatting.TelUri(null, null));
        Assert.IsNull(PlaceFormatting.TelUri("  ", "n/a"));
    }

    [TestMethod]
    public void FormatDistance_UsesFeetMilesMetersKilometers()
    {
        CultureInfo c = CultureInfo.InvariantCulture;
        Assert.AreEqual("0.5 mi", PlaceFormatting.FormatDistance(805, metric: false, c));
        Assert.AreEqual("150 ft", PlaceFormatting.FormatDistance(45, metric: false, c));
        Assert.AreEqual("1.2 km", PlaceFormatting.FormatDistance(1234, metric: true, c));
        Assert.AreEqual("450 m", PlaceFormatting.FormatDistance(447, metric: true, c));
    }

    [TestMethod]
    public void FormatZipDistance_SaysInZipNearbyAndFromZipFarther()
    {
        CultureInfo c = CultureInfo.InvariantCulture;
        Assert.AreEqual("In 98052", PlaceFormatting.FormatZipDistance(400, metric: false, "98052", c));
        Assert.AreEqual("In 98052", PlaceFormatting.FormatZipDistance(1500, metric: true, "98052", c));
        Assert.AreEqual("3.4 mi from 98052", PlaceFormatting.FormatZipDistance(5472, metric: false, "98052", c));
        Assert.AreEqual("5.5 km from SW1A 1AA", PlaceFormatting.FormatZipDistance(5472, metric: true, "SW1A 1AA", c));
    }

    [TestMethod]
    public void DistanceMeters_OneDegreeOfLatitudeIsAbout111Km()
    {
        double m = PlaceFormatting.DistanceMeters(43, -88, 44, -88);
        Assert.IsTrue(m is > 110_000 and < 112_000, m.ToString(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void WeekdayIndex_IsMondayFirst()
    {
        Assert.AreEqual(0, PlaceFormatting.WeekdayIndex(DayOfWeek.Monday));
        Assert.AreEqual(6, PlaceFormatting.WeekdayIndex(DayOfWeek.Sunday));
    }

    [TestMethod]
    public void WebsiteDisplay_DropsWww() =>
        Assert.AreEqual("joes.example", PlaceFormatting.WebsiteDisplay("https://www.joes.example/menu"));

    [TestMethod]
    public void ParseSearch_ReadsTypeLabelAndCategory()
    {
        const string json = """
            {"places":[{"id":"a","displayName":{"text":"Joe's"},"primaryType":"pizza_restaurant",
              "primaryTypeDisplayName":{"text":"Pizza restaurant"},"types":["pizza_restaurant","restaurant","food"]}]}
            """;
        var p = PlacesParser.ParseSearch(json)[0];
        Assert.AreEqual("Pizza restaurant", p.TypeLabel);
        Assert.AreEqual(PlaceCategory.Food, p.Category);
        Assert.AreEqual("Pizza restaurant", p.Subtitle);
    }

    [TestMethod]
    public void ParseSearch_ReadsOpenNow_NullWhenMissing()
    {
        const string json = """
            {"places":[
              {"id":"a","displayName":{"text":"Open"},"currentOpeningHours":{"openNow":true}},
              {"id":"b","displayName":{"text":"Closed"},"currentOpeningHours":{"openNow":false}},
              {"id":"c","displayName":{"text":"Unknown"}}
            ]}
            """;
        var r = PlacesParser.ParseSearch(json);
        Assert.IsTrue(r[0].IsOpenNow);
        Assert.IsTrue(r[1].IsClosedNow);
        Assert.IsNull(r[2].OpenNow);
        Assert.IsFalse(r[2].IsOpenNow || r[2].IsClosedNow);
    }

    [TestMethod]
    public void ParseDetails_ReadsRatingCountAndPrice()
    {
        const string json = """
            {"id":"a","displayName":{"text":"X"},"rating":4.6,"userRatingCount":1234,"priceLevel":"PRICE_LEVEL_MODERATE","primaryType":"cafe"}
            """;
        var d = PlacesParser.ParseDetails(json);
        Assert.AreEqual(4.6, d.Rating);
        Assert.AreEqual(1234, d.UserRatingCount);
        Assert.AreEqual(2, d.PriceLevel);
        Assert.AreEqual(PlaceCategory.Cafe, d.Category);
    }

    [TestMethod]
    public void ParsePriceLevel_UnspecifiedIsNull()
    {
        Assert.IsNull(PlacesParser.ParsePriceLevel("PRICE_LEVEL_UNSPECIFIED"));
        Assert.AreEqual(0, PlacesParser.ParsePriceLevel("PRICE_LEVEL_FREE"));
        Assert.AreEqual(4, PlacesParser.ParsePriceLevel("PRICE_LEVEL_VERY_EXPENSIVE"));
    }

    [TestMethod]
    public void Categories_PrimaryTypeWins_ThenTypes_ThenSuffixRules()
    {
        Assert.AreEqual(PlaceCategory.Cafe, PlaceCategories.FromTypes("coffee_shop", ["restaurant"]));
        Assert.AreEqual(PlaceCategory.Health, PlaceCategories.FromTypes(null, ["point_of_interest", "pharmacy"]));
        Assert.AreEqual(PlaceCategory.Food, PlaceCategories.FromTypes("thai_restaurant", null));
        Assert.AreEqual(PlaceCategory.Shopping, PlaceCategories.FromTypes("hardware_store", null));
        Assert.AreEqual(PlaceCategory.Grocery, PlaceCategories.FromTypes("supermarket", ["store"]));
        Assert.AreEqual(PlaceCategory.Other, PlaceCategories.FromTypes("point_of_interest", ["establishment"]));
        Assert.AreEqual(PlaceCategory.Other, PlaceCategories.FromTypes(null, null));
    }

    [TestMethod]
    public void Categories_EveryCategoryHasAGlyph()
    {
        foreach (PlaceCategory c in Enum.GetValues<PlaceCategory>())
        {
            Assert.AreEqual(1, PlaceCategories.Glyph(c).Length, c.ToString());
        }
    }

    [TestMethod]
    public void SplitWeekday_SeparatesDayAndHours()
    {
        Assert.AreEqual(("Monday", "9:00 AM – 5:00 PM"), PlaceFormatting.SplitWeekday("Monday: 9:00 AM – 5:00 PM"));
        Assert.AreEqual(("", "Open 24 hours"), PlaceFormatting.SplitWeekday("Open 24 hours"));
    }

    [TestMethod]
    public void TodayHours_PicksTodaysRow()
    {
        string[] week = ["Monday: 9 AM – 5 PM", "Tuesday: Closed"];
        Assert.AreEqual("Closed", PlaceFormatting.TodayHours(week, DayOfWeek.Tuesday));
        Assert.IsNull(PlaceFormatting.TodayHours(week, DayOfWeek.Sunday));
    }

    [TestMethod]
    public void StarFills_RoundToNearestHalf()
    {
        CollectionAssert.AreEqual(new[] { 2, 2, 2, 2, 1 }, PlaceFormatting.StarFills(4.6));
        CollectionAssert.AreEqual(new[] { 2, 2, 2, 2, 0 }, PlaceFormatting.StarFills(4.2));
        CollectionAssert.AreEqual(new[] { 2, 2, 2, 2, 2 }, PlaceFormatting.StarFills(4.8));
        CollectionAssert.AreEqual(new[] { 0, 0, 0, 0, 0 }, PlaceFormatting.StarFills(0));
    }

    [TestMethod]
    public void PriceText_RepeatsSymbol()
    {
        Assert.AreEqual("$$", PlaceFormatting.PriceText(2, "$"));
        Assert.AreEqual("Free", PlaceFormatting.PriceText(0, "$"));
        Assert.IsNull(PlaceFormatting.PriceText(null, "$"));
    }

    [TestMethod]
    public void PushRecent_MovesToFront_Dedupes_AndCaps()
    {
        var list = PlaceFormatting.PushRecent(["pizza", "Coffee", "atm"], "coffee", max: 3);
        CollectionAssert.AreEqual(new[] { "coffee", "pizza", "atm" }, list);
        CollectionAssert.AreEqual(new[] { "a", "b" }, PlaceFormatting.PushRecent(["a", "b", "c"], "  ", max: 2));
    }

    [TestMethod]
    public void MapBounds_CoversEveryPoint()
    {
        var b = MapBounds.Of([(43.04, -87.91), (43.05, -87.92), (43.03, -87.89)]);
        Assert.AreEqual(43.05, b.North, 1e-9);
        Assert.AreEqual(43.03, b.South, 1e-9);
        Assert.AreEqual(-87.92, b.West, 1e-9);
        Assert.AreEqual(-87.89, b.East, 1e-9);
        Assert.AreEqual(43.04, b.CenterLatitude, 1e-9);
    }

    [TestMethod]
    public void MapBounds_TinyBox_GrowsAroundItsCenter()
    {
        var b = MapBounds.Of([(43.0, -87.9), (43.0001, -87.9)]);
        Assert.AreEqual(MapBounds.MinSpanDegrees, b.North - b.South, 1e-9);
        Assert.AreEqual(MapBounds.MinSpanDegrees, b.East - b.West, 1e-9);
        Assert.AreEqual(43.00005, b.CenterLatitude, 1e-9);
        Assert.AreEqual(-87.9, b.CenterLongitude, 1e-9);
    }

    [TestMethod]
    public void MapBounds_NoPoints_Throws() =>
        Assert.ThrowsExactly<ArgumentException>(() => MapBounds.Of([]));

    [TestMethod]
    public void IsClosingSoon_OnlyWhenOpenAndClosingWithinAnHour()
    {
        DateTimeOffset now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        Assert.IsTrue(PlaceFormatting.IsClosingSoon(true, now.AddMinutes(30), now));
        Assert.IsTrue(PlaceFormatting.IsClosingSoon(true, now.AddHours(1), now));
        Assert.IsFalse(PlaceFormatting.IsClosingSoon(true, now.AddMinutes(61), now));
        Assert.IsFalse(PlaceFormatting.IsClosingSoon(true, null, now));
        Assert.IsFalse(PlaceFormatting.IsClosingSoon(false, now.AddMinutes(30), now));
        Assert.IsFalse(PlaceFormatting.IsClosingSoon(true, now.AddMinutes(-5), now));
    }
}
