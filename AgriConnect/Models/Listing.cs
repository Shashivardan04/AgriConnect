using System.ComponentModel.DataAnnotations;

namespace AgriConnect.Models;

/// <summary>
/// Equipment for rent, produce for sale, and labour on offer are all a listing:
/// someone, somewhere, offering something at a price. Keeping them in one table
/// means one search, one overlap rule and one set of views instead of three.
/// </summary>
public class Listing
{
    public int Id { get; set; }

    public ListingType Type { get; set; }

    [Required]
    public string OwnerId { get; set; } = "";
    public ApplicationUser? Owner { get; set; }

    [Required, StringLength(120), Display(Name = "Title")]
    public string Title { get; set; } = "";

    // Every displayed property carries [Display], even where the text matches the
    // property name. Without it the label falls back to the raw property name, which
    // the data annotations localizer never sees - so it stayed English on a Tamil
    // page while its neighbours translated.
    [StringLength(2000), Display(Name = "Description")]
    public string? Description { get; set; }

    [Display(Name = "Category")]
    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    [Range(0, 9_999_999), Display(Name = "Price")]
    public decimal Price { get; set; }

    [Display(Name = "Price unit")]
    public PriceUnit PriceUnit { get; set; }

    /// <summary>Produce only: how much is on offer. Null for equipment and labour.</summary>
    [Range(0, 9_999_999), Display(Name = "Quantity")]
    public decimal? Quantity { get; set; }

    [StringLength(80), Display(Name = "Village")] public string? Village { get; set; }
    [StringLength(80), Display(Name = "Town or taluk")] public string? Town { get; set; }

    [Required, StringLength(80), Display(Name = "District")]
    public string District { get; set; } = "";

    // Labour listings only. Two nullable columns beat a second table and a join.
    [StringLength(300), Display(Name = "Skills")]
    public string? Skills { get; set; }

    [Range(0, 80), Display(Name = "Years of experience")]
    public int? ExperienceYears { get; set; }

    [Display(Name = "Status")]
    public ListingStatus Status { get; set; } = ListingStatus.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ListingImage> Images { get; set; } = [];
    public List<Booking> Bookings { get; set; } = [];

    /// <summary>Produce is bought by contacting the seller; only equipment and labour are booked.</summary>
    public bool IsBookable => Type is ListingType.Equipment or ListingType.Labour;

    /// <summary>
    /// What a booking actually reserves. Equipment reserves the machine, so an owner
    /// with three tractors can rent all three at once. Labour reserves the person, so
    /// every task the same worker registers for shares one calendar - a worker booked
    /// for weeding on Tuesday cannot also be harvesting that day.
    /// </summary>
    public string BookingResourceKey =>
        Type == ListingType.Labour ? $"worker-{OwnerId}" : $"listing-{Id}";
}
