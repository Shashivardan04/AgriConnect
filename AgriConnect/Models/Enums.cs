namespace AgriConnect.Models;

public enum ListingType
{
    Equipment = 1,
    Produce = 2,
    Labour = 3
}

public enum ListingStatus
{
    Active = 1,
    Sold = 2,
    Removed = 3
}

public enum PriceUnit
{
    PerDay = 1,
    PerHour = 2,
    PerKg = 3,
    PerQuintal = 4,
    PerHead = 5,
    // Appended, not inserted: these are stored as int, so renumbering would silently
    // reinterpret every existing row.
    PerLitre = 6
}

/// <summary>
/// Self-declared on the member's own profile, and optional - a null means not stated
/// rather than unknown. Shown on labour profiles because farmers hiring field crews
/// routinely ask, and workers advertise it themselves.
/// </summary>
public enum Gender
{
    Male = 1,
    Female = 2,
    Other = 3
}

public enum BookingStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
    Completed = 5
}
