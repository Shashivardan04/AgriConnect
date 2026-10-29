using System.ComponentModel.DataAnnotations;

namespace AgriConnect.Models;

public class Notification
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }

    [Required, StringLength(300)]
    public string Message { get; set; } = "";

    [StringLength(300)]
    public string? LinkUrl { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
