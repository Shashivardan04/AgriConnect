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

    /// <summary>
    /// Produce only: how much is still on offer. Null for equipment and labour.
    /// This is the REMAINING amount, not the amount first listed - approving an order
    /// draws it down (BookingService.ApproveAsync) and cancelling one puts it back,
    /// which is what keeps the cards, the detail page and the category totals honest
    /// without any of them having to aggregate bookings. The original is still
    /// recoverable as this plus the quantity of every approved booking.
    /// </summary>
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

    /// <summary>
    /// Reserved by date range, exclusively: a booked tractor is nobody else's that week.
    /// Produce is deliberately NOT bookable - it is ordered by quantity instead, and
    /// widening this would hand it the availability calendar, which answers the wrong
    /// question. See <see cref="IsOrderable"/>.
    /// </summary>
    public bool IsBookable => Type is ListingType.Equipment or ListingType.Labour;

    /// <summary>
    /// Bought by quantity rather than reserved by date. Two buyers wanting 20 kg each
    /// of the same 100 kg do not clash, so ordering asks "is there enough left?" where
    /// booking asks "do the dates overlap?".
    /// </summary>
    public bool IsOrderable => Type == ListingType.Produce;

    /// <summary>
    /// Sold by the day: the quantity is how much there is EVERY day, not how much there
    /// is in total, so it is never drawn down and the listing never sells out. Ten
    /// households can each take two litres of a twenty-litre yield; they clash only on
    /// the day their combined order passes it.
    ///
    /// Reads through Category, so a listing loaded without it reports false and would be
    /// treated as a depleting stock. Everything in BookingService that asks this loads
    /// the category with the listing; see OrderAsync and ApproveAsync.
    /// </summary>
    public bool IsDailySale => Type == ListingType.Produce && Category?.SellsDaily == true;

    /// <summary>
    /// What a booking actually reserves. Equipment reserves the machine, so an owner
    /// with three tractors can rent all three at once. Labour reserves the person, so
    /// every task the same worker registers for shares one calendar - a worker booked
    /// for weeding on Tuesday cannot also be harvesting that day.
    /// </summary>
    public string BookingResourceKey =>
        Type == ListingType.Labour ? $"worker-{OwnerId}" : $"listing-{Id}";
}
