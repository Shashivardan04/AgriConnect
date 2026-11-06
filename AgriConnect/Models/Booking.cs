using System.ComponentModel.DataAnnotations;

namespace AgriConnect.Models;

/// <summary>
/// A reservation of a listing over a half-open date range [FromDate, ToDate):
/// a booking ending on the 10th and one starting on the 10th do not conflict.
/// Owner downtime is stored as an Approved booking the owner made against their
/// own listing, so "blocked" and "booked" need only one overlap check.
///
/// A produce order is the same row read differently: the range is the collection
/// day, [pickup, pickup+1), and Quantity carries how much was asked for. Equipment
/// and labour reserve the whole listing, so their Quantity is null. One row type
/// rather than two means one status machine, one notification path and one pair of
/// "my requests" / "requests for my listings" screens.
/// </summary>
public class Booking
{
    public int Id { get; set; }

    public int ListingId { get; set; }
    public Listing? Listing { get; set; }

    [Required]
    public string RequesterId { get; set; } = "";
    public ApplicationUser? Requester { get; set; }

    // "From date" rather than "From": translation keys are the English text itself and
    // are matched without case, so a bare "From" collides with the lowercase "from"
    // that sits between a quantity and a price elsewhere, and one of the two loses
    // its Tamil.
    [Display(Name = "From date")] public DateOnly FromDate { get; set; }
    [Display(Name = "To date")] public DateOnly ToDate { get; set; }

    /// <summary>
    /// Produce only: how much was ordered, counted in the listing's own PriceUnit.
    /// Null for equipment and labour, which take the whole listing rather than part
    /// of it. The lower bound is 0.01 and not 0 - an order for nothing is not an order.
    ///
    /// Always PER DAY of the range. A one-off order is a one-day range, so per day and
    /// in total are the same number; a daily order for two litres over three weeks is
    /// 2, not 42. One meaning, never conditional on what is being bought - otherwise
    /// the fork leaks out of the service and into the data.
    /// </summary>
    [Range(0.01, 9_999_999), Display(Name = "Quantity")]
    public decimal? Quantity { get; set; }

    /// <summary>
    /// The listing's price at the moment the order was placed. Stored rather than read
    /// back through Listing, because the seller may edit Price afterwards and this row
    /// is the only record of what the two of them agreed.
    /// </summary>
    [Range(0, 9_999_999), Display(Name = "Price")]
    public decimal? UnitPrice { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    [StringLength(500)]
    public string? Message { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>True when the owner booked their own listing to mark it unavailable.</summary>
    public bool IsOwnerBlock => Listing is not null && Listing.OwnerId == RequesterId;

    /// <summary>
    /// What the order comes to at the price agreed: the daily amount, times the price,
    /// times the number of days. Null for a date booking, which has no quantity, and
    /// for any row written before this column existed.
    /// </summary>
    public decimal? Total => Quantity is decimal q && UnitPrice is decimal p ? q * p * Days : null;

    /// <summary>The first collection day. A one-off order has only this one.</summary>
    public DateOnly PickupDate => FromDate;

    /// <summary>
    /// Days in the half-open range, so 1 for a one-off order and 7 for a week of
    /// deliveries. The range is the same column the other two types reserve over.
    /// </summary>
    public int Days => ToDate.DayNumber - FromDate.DayNumber;

    /// <summary>A standing order rather than a single collection.</summary>
    public bool IsRecurring => Quantity is not null && Days > 1;
}
