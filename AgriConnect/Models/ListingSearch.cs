using System.ComponentModel.DataAnnotations;

namespace AgriConnect.Models;

/// <summary>Everything the search form can ask for. All fields optional.</summary>
public class ListingSearch
{
    [Display(Name = "Search")] public string? Q { get; set; }
    [Display(Name = "Category")] public int? CategoryId { get; set; }
    [Display(Name = "District")] public string? District { get; set; }
    [Display(Name = "Town or village")] public string? Place { get; set; }
    [Display(Name = "Min price")] public decimal? MinPrice { get; set; }
    [Display(Name = "Max price")] public decimal? MaxPrice { get; set; }
    [Display(Name = "Available from")] public DateOnly? From { get; set; }
    [Display(Name = "Available to")] public DateOnly? To { get; set; }
    [Display(Name = "Sort by")] public string? Sort { get; set; }

    /// <summary>Labour only: the worker's own declared gender.</summary>
    // Explicit [Display] even though the name matches the property: without it the
    // label falls back to the raw property name, which the data annotations
    // localizer never sees and which therefore stays English on a Tamil page.
    [Display(Name = "Gender")] public Gender? Gender { get; set; }

    public bool AnyFilter =>
        !string.IsNullOrWhiteSpace(Q) || CategoryId is not null || !string.IsNullOrWhiteSpace(District)
        || !string.IsNullOrWhiteSpace(Place) || MinPrice is not null || MaxPrice is not null
        || From is not null || To is not null || Gender is not null;

    // These come straight off the query string, so anyone can put anything in them.
    // Every value below is compared against a column, and SQL Server rejects the
    // comparison outright if the value cannot fit the column - a long enough search
    // term or a price outside decimal(10,2) took the whole page down with a 500.
    // Clamping here keeps that at one place rather than at each use.
    private const int MaxTermLength = 120;      // Listing.Title
    private const int MaxPlaceLength = 80;      // Listing.District / Town / Village
    private const decimal MaxPriceValue = 9_999_999.99m;   // Listing.Price precision

    /// <summary>
    /// Trims every field to something the database can actually be asked about.
    /// Out-of-range values are clamped rather than rejected: a search is a
    /// convenience, and silently widening "minimum price -999999999" to "no minimum"
    /// is friendlier than an error page, where a mistyped filter would otherwise be
    /// a dead end.
    /// </summary>
    public ListingSearch Normalised()
    {
        // A term longer than the longest possible title cannot match one, so cutting
        // it loses no results that the shorter term would not already have found.
        Q = Clip(Q, MaxTermLength);
        District = Clip(District, MaxPlaceLength);
        Place = Clip(Place, MaxPlaceLength);

        MinPrice = ClampPrice(MinPrice);
        MaxPrice = ClampPrice(MaxPrice);

        // A reversed range would silently match nothing, which reads as "no results"
        // rather than "you typed it backwards".
        if (MinPrice is decimal low && MaxPrice is decimal high && low > high)
            (MinPrice, MaxPrice) = (high, low);

        if (From is not null && To is not null && To <= From)
            To = null;

        return this;
    }

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    private static decimal? ClampPrice(decimal? value) =>
        value is null ? null : Math.Clamp(value.Value, 0m, MaxPriceValue);
}
