using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
}
