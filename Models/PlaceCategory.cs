namespace QuickPlaces.Models;

/// <summary>A coarse grouping of Google place types, which picks a place's symbol and tile color.</summary>
public enum PlaceCategory
{
    Other,
    Food,
    Cafe,
    Bar,
    Grocery,
    Shopping,
    Health,
    Lodging,
    Fuel,
    EvCharging,
    Automotive,
    Parking,
    Finance,
    Education,
    Library,
    Entertainment,
    Outdoors,
    Fitness,
    Beauty,
    Transit,
    Airport,
    Services,
    Worship,
}

/// <summary>A one-tap search on the start screen.</summary>
public sealed record QuickCategory(string Label, string Query, PlaceCategory Category)
{
    public string Glyph => PlaceCategories.Glyph(Category);
}

/// <summary>
/// Maps Google Places (New) types to <see cref="PlaceCategory"/> and each category to a Segoe
/// Fluent Icons glyph. No WinRT dependency, so it is unit tested.
/// </summary>
public static class PlaceCategories
{
    private static readonly Dictionary<string, PlaceCategory> s_exact = new(StringComparer.Ordinal)
    {
        ["cafe"] = PlaceCategory.Cafe,
        ["coffee_shop"] = PlaceCategory.Cafe,
        ["tea_house"] = PlaceCategory.Cafe,
        ["bakery"] = PlaceCategory.Cafe,
        ["donut_shop"] = PlaceCategory.Cafe,
        ["juice_shop"] = PlaceCategory.Cafe,
        ["cafeteria"] = PlaceCategory.Food,
        ["restaurant"] = PlaceCategory.Food,
        ["bar"] = PlaceCategory.Bar,
        ["pub"] = PlaceCategory.Bar,
        ["wine_bar"] = PlaceCategory.Bar,
        ["night_club"] = PlaceCategory.Bar,
        ["brewery"] = PlaceCategory.Bar,
        ["meal_takeaway"] = PlaceCategory.Food,
        ["meal_delivery"] = PlaceCategory.Food,
        ["food"] = PlaceCategory.Food,
        ["food_court"] = PlaceCategory.Food,
        ["sandwich_shop"] = PlaceCategory.Food,
        ["deli"] = PlaceCategory.Food,
        ["diner"] = PlaceCategory.Food,
        ["steak_house"] = PlaceCategory.Food,
        ["bar_and_grill"] = PlaceCategory.Food,
        ["ice_cream_shop"] = PlaceCategory.Food,
        ["dessert_shop"] = PlaceCategory.Food,
        ["grocery_store"] = PlaceCategory.Grocery,
        ["supermarket"] = PlaceCategory.Grocery,
        ["convenience_store"] = PlaceCategory.Grocery,
        ["market"] = PlaceCategory.Grocery,
        ["food_store"] = PlaceCategory.Grocery,
        ["asian_grocery_store"] = PlaceCategory.Grocery,
        ["butcher_shop"] = PlaceCategory.Grocery,
        ["liquor_store"] = PlaceCategory.Grocery,
        ["shopping_mall"] = PlaceCategory.Shopping,
        ["store"] = PlaceCategory.Shopping,
        ["pharmacy"] = PlaceCategory.Health,
        ["drugstore"] = PlaceCategory.Health,
        ["hospital"] = PlaceCategory.Health,
        ["doctor"] = PlaceCategory.Health,
        ["dentist"] = PlaceCategory.Health,
        ["dental_clinic"] = PlaceCategory.Health,
        ["medical_lab"] = PlaceCategory.Health,
        ["medical_clinic"] = PlaceCategory.Health,
        ["physiotherapist"] = PlaceCategory.Health,
        ["chiropractor"] = PlaceCategory.Health,
        ["veterinary_care"] = PlaceCategory.Health,
        ["health"] = PlaceCategory.Health,
        ["lodging"] = PlaceCategory.Lodging,
        ["hotel"] = PlaceCategory.Lodging,
        ["motel"] = PlaceCategory.Lodging,
        ["hostel"] = PlaceCategory.Lodging,
        ["inn"] = PlaceCategory.Lodging,
        ["bed_and_breakfast"] = PlaceCategory.Lodging,
        ["guest_house"] = PlaceCategory.Lodging,
        ["resort_hotel"] = PlaceCategory.Lodging,
        ["extended_stay_hotel"] = PlaceCategory.Lodging,
        ["gas_station"] = PlaceCategory.Fuel,
        ["electric_vehicle_charging_station"] = PlaceCategory.EvCharging,
        ["car_repair"] = PlaceCategory.Automotive,
        ["car_wash"] = PlaceCategory.Automotive,
        ["car_dealer"] = PlaceCategory.Automotive,
        ["car_rental"] = PlaceCategory.Automotive,
        ["parking"] = PlaceCategory.Parking,
        ["bank"] = PlaceCategory.Finance,
        ["atm"] = PlaceCategory.Finance,
        ["accounting"] = PlaceCategory.Finance,
        ["finance"] = PlaceCategory.Finance,
        ["school"] = PlaceCategory.Education,
        ["primary_school"] = PlaceCategory.Education,
        ["secondary_school"] = PlaceCategory.Education,
        ["university"] = PlaceCategory.Education,
        ["preschool"] = PlaceCategory.Education,
        ["library"] = PlaceCategory.Library,
        ["book_store"] = PlaceCategory.Library,
        ["movie_theater"] = PlaceCategory.Entertainment,
        ["amusement_park"] = PlaceCategory.Entertainment,
        ["bowling_alley"] = PlaceCategory.Entertainment,
        ["casino"] = PlaceCategory.Entertainment,
        ["museum"] = PlaceCategory.Entertainment,
        ["art_gallery"] = PlaceCategory.Entertainment,
        ["tourist_attraction"] = PlaceCategory.Entertainment,
        ["aquarium"] = PlaceCategory.Entertainment,
        ["zoo"] = PlaceCategory.Entertainment,
        ["performing_arts_theater"] = PlaceCategory.Entertainment,
        ["concert_hall"] = PlaceCategory.Entertainment,
        ["event_venue"] = PlaceCategory.Entertainment,
        ["video_arcade"] = PlaceCategory.Entertainment,
        ["park"] = PlaceCategory.Outdoors,
        ["national_park"] = PlaceCategory.Outdoors,
        ["hiking_area"] = PlaceCategory.Outdoors,
        ["campground"] = PlaceCategory.Outdoors,
        ["dog_park"] = PlaceCategory.Outdoors,
        ["playground"] = PlaceCategory.Outdoors,
        ["garden"] = PlaceCategory.Outdoors,
        ["botanical_garden"] = PlaceCategory.Outdoors,
        ["beach"] = PlaceCategory.Outdoors,
        ["marina"] = PlaceCategory.Outdoors,
        ["gym"] = PlaceCategory.Fitness,
        ["fitness_center"] = PlaceCategory.Fitness,
        ["sports_club"] = PlaceCategory.Fitness,
        ["sports_complex"] = PlaceCategory.Fitness,
        ["yoga_studio"] = PlaceCategory.Fitness,
        ["swimming_pool"] = PlaceCategory.Fitness,
        ["stadium"] = PlaceCategory.Fitness,
        ["hair_salon"] = PlaceCategory.Beauty,
        ["hair_care"] = PlaceCategory.Beauty,
        ["beauty_salon"] = PlaceCategory.Beauty,
        ["barber_shop"] = PlaceCategory.Beauty,
        ["nail_salon"] = PlaceCategory.Beauty,
        ["spa"] = PlaceCategory.Beauty,
        ["beautician"] = PlaceCategory.Beauty,
        ["bus_station"] = PlaceCategory.Transit,
        ["bus_stop"] = PlaceCategory.Transit,
        ["train_station"] = PlaceCategory.Transit,
        ["subway_station"] = PlaceCategory.Transit,
        ["transit_station"] = PlaceCategory.Transit,
        ["light_rail_station"] = PlaceCategory.Transit,
        ["transit_depot"] = PlaceCategory.Transit,
        ["airport"] = PlaceCategory.Airport,
        ["international_airport"] = PlaceCategory.Airport,
        ["plumber"] = PlaceCategory.Services,
        ["electrician"] = PlaceCategory.Services,
        ["locksmith"] = PlaceCategory.Services,
        ["moving_company"] = PlaceCategory.Services,
        ["painter"] = PlaceCategory.Services,
        ["roofing_contractor"] = PlaceCategory.Services,
        ["general_contractor"] = PlaceCategory.Services,
        ["laundry"] = PlaceCategory.Services,
        ["storage"] = PlaceCategory.Services,
        ["post_office"] = PlaceCategory.Services,
        ["insurance_agency"] = PlaceCategory.Services,
        ["lawyer"] = PlaceCategory.Services,
        ["real_estate_agency"] = PlaceCategory.Services,
        ["travel_agency"] = PlaceCategory.Services,
        ["church"] = PlaceCategory.Worship,
        ["mosque"] = PlaceCategory.Worship,
        ["synagogue"] = PlaceCategory.Worship,
        ["hindu_temple"] = PlaceCategory.Worship,
        ["place_of_worship"] = PlaceCategory.Worship,
    };

    /// <summary>The start screen's one-tap searches.</summary>
    public static IReadOnlyList<QuickCategory> Quick { get; } =
    [
        new("Coffee", "coffee", PlaceCategory.Cafe),
        new("Food", "restaurants", PlaceCategory.Food),
        new("Groceries", "grocery store", PlaceCategory.Grocery),
        new("Gas", "gas station", PlaceCategory.Fuel),
        new("Pharmacy", "pharmacy", PlaceCategory.Health),
        new("ATM", "atm", PlaceCategory.Finance),
        new("Parks", "park", PlaceCategory.Outdoors),
        new("Hotels", "hotel", PlaceCategory.Lodging),
    ];

    /// <summary>
    /// The category for a place: its primary type wins; otherwise the first of its other types that
    /// maps to something; then suffix rules (<c>*_restaurant</c>, <c>*_store</c>, ...).
    /// </summary>
    public static PlaceCategory FromTypes(string? primaryType, IEnumerable<string>? types)
    {
        if (Match(primaryType) is { } primary)
        {
            return primary;
        }

        if (types is not null)
        {
            foreach (string type in types)
            {
                if (Match(type) is { } match)
                {
                    return match;
                }
            }
        }

        return PlaceCategory.Other;
    }

    private static PlaceCategory? Match(string? type)
    {
        if (string.IsNullOrEmpty(type))
        {
            return null;
        }

        if (s_exact.TryGetValue(type, out PlaceCategory exact))
        {
            return exact;
        }

        if (type.EndsWith("_restaurant", StringComparison.Ordinal) || type.StartsWith("pizza", StringComparison.Ordinal))
        {
            return PlaceCategory.Food;
        }

        if (type.EndsWith("_store", StringComparison.Ordinal) || type.EndsWith("_shop", StringComparison.Ordinal))
        {
            return PlaceCategory.Shopping;
        }

        if (type.EndsWith("_hotel", StringComparison.Ordinal))
        {
            return PlaceCategory.Lodging;
        }

        if (type.EndsWith("_school", StringComparison.Ordinal))
        {
            return PlaceCategory.Education;
        }

        if (type.EndsWith("_service", StringComparison.Ordinal) || type.EndsWith("_contractor", StringComparison.Ordinal))
        {
            return PlaceCategory.Services;
        }

        return null;
    }

    /// <summary>A Segoe Fluent Icons glyph for the category.</summary>
    public static string Glyph(PlaceCategory category) => category switch
    {
        PlaceCategory.Food => "\uED56",          // EmojiTabFoodPlants (pizza slice)
        PlaceCategory.Cafe => "\uEC32",          // Cafe
        PlaceCategory.Bar => "\uEC4F",           // MusicNote
        PlaceCategory.Grocery => "\uE7BF",       // ShoppingCart
        PlaceCategory.Shopping => "\uE719",      // Shop
        PlaceCategory.Health => "\uE95E",        // Health
        PlaceCategory.Lodging => "\uEC07",       // CityNext2
        PlaceCategory.Fuel => "\uE804",          // Car
        PlaceCategory.EvCharging => "\uE945",    // LightningBolt
        PlaceCategory.Automotive => "\uE90F",    // Repair
        PlaceCategory.Parking => "\uE811",       // ParkingLocation
        PlaceCategory.Finance => "\uE825",       // Bank
        PlaceCategory.Education => "\uE7BE",     // Education
        PlaceCategory.Library => "\uE8F1",       // Library
        PlaceCategory.Entertainment => "\uE8B2", // Movies
        PlaceCategory.Outdoors => "\uE8BE",      // Leaf
        PlaceCategory.Fitness => "\uE726",       // WalkSolid
        PlaceCategory.Beauty => "\uE8C6",        // Cut
        PlaceCategory.Transit => "\uE806",       // Bus
        PlaceCategory.Airport => "\uE709",       // Airplane
        PlaceCategory.Services => "\uE90F",      // Repair
        PlaceCategory.Worship => "\uE716",       // People
        _ => "\uE707",                           // MapPin
    };
}
