using System.ComponentModel.DataAnnotations;

namespace AgriConnect.Models;

/// <summary>
/// The member's own details. Separate from <see cref="ApplicationUser"/> so the form
/// cannot reach the password hash, the security stamp or anything else Identity owns.
/// </summary>
public class ProfileForm
{
    [Required, StringLength(120), Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [Phone, StringLength(20), Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    // [Display] on every one, even where it repeats the property name: the label only
    // goes through the translator when the attribute is there.
    [Display(Name = "Gender")]
    public Gender? Gender { get; set; }

    [StringLength(80), Display(Name = "Village")] public string? Village { get; set; }
    [StringLength(80), Display(Name = "Town or taluk")] public string? Town { get; set; }

    [Required, StringLength(80), Display(Name = "District")]
    public string District { get; set; } = "";
}
