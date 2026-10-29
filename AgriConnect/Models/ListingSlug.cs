namespace AgriConnect.Models;

/// <summary>
/// The SOW describes three modules; the code has one. This maps the three public
/// URL prefixes onto the single ListingType so /equipment, /marketplace and
/// /labour read as separate modules while sharing one controller and one view set.
/// </summary>
public static class ListingSlug
{
    public const string RouteConstraint = "^(equipment|marketplace|labour)$";

    private static readonly Dictionary<string, ListingType> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["equipment"] = ListingType.Equipment,
        ["marketplace"] = ListingType.Produce,
        ["labour"] = ListingType.Labour
    };

    public static ListingType ToType(string slug) => Map[slug];

    /// <summary>
    /// Whether this is one of the three public prefixes. The route constraint is the
    /// usual guard; this is for the places that have to check a slug that did not
    /// come straight from the route. See ListingsController.OnActionExecuting.
    /// </summary>
    public static bool IsKnown(string? slug) => slug is not null && Map.ContainsKey(slug);

    public static string From(ListingType type) => type switch
    {
        ListingType.Equipment => "equipment",
        ListingType.Produce => "marketplace",
        ListingType.Labour => "labour",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static string Heading(ListingType type) => type switch
    {
        ListingType.Equipment => "Equipment rental",
        ListingType.Produce => "Marketplace",
        ListingType.Labour => "Agricultural labour",
        _ => ""
    };

    /// <summary>
    /// The whole "add something here" phrase, not the noun on its own. It used to be
    /// just the noun, dropped into "List {noun}" at four call sites - which cannot be
    /// translated, because a translator needs the finished sentence and Tamil does not
    /// keep the verb still while the noun changes around it.
    /// </summary>
    public static string AddAction(ListingType type) => type switch
    {
        ListingType.Equipment => "List equipment",
        ListingType.Produce => "List a product",
        ListingType.Labour => "Register for a task",
        _ => "Add a listing"
    };

    public static string UnitLabel(PriceUnit unit) => unit switch
    {
        PriceUnit.PerDay => "per day",
        PriceUnit.PerHour => "per hour",
        PriceUnit.PerKg => "per kg",
        PriceUnit.PerQuintal => "per quintal",
        PriceUnit.PerHead => "per head",
        PriceUnit.PerLitre => "per litre",
        _ => ""
    };

    /// <summary>
    /// What a quantity in this unit is counted in: "6 head", "250 kg". Its own method
    /// rather than trimming "per " off <see cref="UnitLabel"/>, which gives "litre"
    /// where "litres" is wanted and breaks the moment a label is reworded.
    /// </summary>
    public static string QuantityLabel(PriceUnit unit) => unit switch
    {
        PriceUnit.PerKg => "kg",
        PriceUnit.PerQuintal => "quintal",
        PriceUnit.PerHead => "head",
        PriceUnit.PerLitre => "litres",
        PriceUnit.PerDay => "days",
        PriceUnit.PerHour => "hours",
        _ => ""
    };
}
