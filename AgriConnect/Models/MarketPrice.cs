using System.ComponentModel.DataAnnotations;

namespace AgriConnect.Models;

/// <summary>
/// Administrator-maintained reference price for a category in a district (SOW §2.3.7).
/// Stored figures only - nothing is predicted.
/// </summary>
public class MarketPrice
{
    public int Id { get; set; }

    [Display(Name = "Category")]
    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    // [Display] on each, even where it repeats the property name: a label without one
    // falls back to the raw property name, which never reaches the translator.
    [Required, StringLength(80), Display(Name = "District")]
    public string District { get; set; } = "";

    [Range(0, 9_999_999), Display(Name = "Price")]
    public decimal Price { get; set; }

    [Display(Name = "Price unit")]
    public PriceUnit Unit { get; set; }

    [StringLength(200), Display(Name = "Source")]
    public string? Source { get; set; }

    [Display(Name = "As of")]
    public DateOnly AsOfDate { get; set; }
}
