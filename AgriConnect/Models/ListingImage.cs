using System.ComponentModel.DataAnnotations;

namespace AgriConnect.Models;

/// <summary>Image files live in wwwroot/uploads; only the name is stored.</summary>
public class ListingImage
{
    public int Id { get; set; }

    public int ListingId { get; set; }
    public Listing? Listing { get; set; }

    [Required, StringLength(120)]
    public string FileName { get; set; } = "";
}
