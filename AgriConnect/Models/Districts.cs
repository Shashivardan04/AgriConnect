namespace AgriConnect.Models;

/// <summary>
/// Every district of Tamil Nadu, as the state stands after the 2019-2020 round of
/// reorganisation that created Chengalpattu, Kallakurichi, Mayiladuthurai, Ranipet,
/// Tenkasi, Tirupathur and Tiruppur.
///
/// A fixed list, not a table. Nothing in the SOW asks administrators to manage
/// districts; the set changes once a decade, by government order, not by a member
/// adding one. Free text is what makes a location search useless - "Coimbatore",
/// "coimbatore" and "CBE" are three districts to a database and one to a farmer.
///
/// The English name is what is stored and what is validated on submit. The Tamil is
/// a label looked up from the translation file by that name, so adding a district
/// here means adding its Tamil to Resources/SharedResource.ta.resx as well.
///
/// Alphabetical: the dropdown is re-sorted by the translated name at render time
/// (see Choices.DistrictList), so this order is for whoever reads the code.
/// </summary>
public static class Districts
{
    public static readonly string[] All =
    [
        "Ariyalur",
        "Chengalpattu",
        "Chennai",
        "Coimbatore",
        "Cuddalore",
        "Dharmapuri",
        "Dindigul",
        "Erode",
        "Kallakurichi",
        "Kanchipuram",
        "Kanyakumari",
        "Karur",
        "Krishnagiri",
        "Madurai",
        "Mayiladuthurai",
        "Nagapattinam",
        "Namakkal",
        "Nilgiris",
        "Perambalur",
        "Pudukkottai",
        "Ramanathapuram",
        "Ranipet",
        "Salem",
        "Sivaganga",
        "Tenkasi",
        "Thanjavur",
        "Theni",
        "Thoothukudi",
        "Tiruchirappalli",
        "Tirunelveli",
        "Tirupathur",
        "Tiruppur",
        "Tiruvallur",
        "Tiruvannamalai",
        "Tiruvarur",
        "Vellore",
        "Villupuram",
        "Virudhunagar"
    ];
}
