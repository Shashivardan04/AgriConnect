using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace AgriConnect.Models;

public class ApplicationUser : IdentityUser
{
    [PersonalData, Display(Name = "Full name")]
    public string? FullName { get; set; }

    /// <summary>
    /// A property of the person, not of each task they register for: a worker is one
    /// person however many tasks they offer. Optional - null means not stated.
    /// </summary>
    [PersonalData]
    public Gender? Gender { get; set; }

    [PersonalData] public string? Village { get; set; }
    [PersonalData] public string? Town { get; set; }
    [PersonalData] public string? District { get; set; }
}
