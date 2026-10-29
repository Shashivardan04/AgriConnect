using System.ComponentModel.DataAnnotations;

namespace AgriConnect.Models;

/// <summary>
/// SOW §2.3.1 asks administrators to review reported listings but defines no way
/// to report one. This is that missing half.
/// </summary>
public class ListingReport
{
    public int Id { get; set; }

    public int ListingId { get; set; }
    public Listing? Listing { get; set; }

    [Required]
    public string ReportedById { get; set; } = "";
    public ApplicationUser? ReportedBy { get; set; }

    [Required, StringLength(500), Display(Name = "Reason")]
    public string Reason { get; set; } = "";

    public bool Resolved { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
