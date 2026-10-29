using System.ComponentModel.DataAnnotations;

namespace AgriConnect.Models;

/// <summary>
/// A reservation of a listing over a half-open date range [FromDate, ToDate):
/// a booking ending on the 10th and one starting on the 10th do not conflict.
/// Owner downtime is stored as an Approved booking the owner made against their
/// own listing, so "blocked" and "booked" need only one overlap check.
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

    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    [StringLength(500)]
    public string? Message { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>True when the owner booked their own listing to mark it unavailable.</summary>
    public bool IsOwnerBlock => Listing is not null && Listing.OwnerId == RequesterId;
}
