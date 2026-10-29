using System.ComponentModel.DataAnnotations;

namespace AgriConnect.Models;

public class Category
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    public string Name { get; set; } = "";

    public ListingType ListingType { get; set; }

    /// <summary>
    /// Livestock, Vegetables, Fruits and so on - the heading a product sits under on
    /// the marketplace index. Null for labour and equipment, which stay a flat list.
    /// </summary>
    [StringLength(80)]
    public string? Group { get; set; }

    /// <summary>
    /// Pre-selects the unit on the listing form: sheep by the head, tomatoes by the
    /// kilo. A suggestion, not a constraint - a seller may still price sheep by weight.
    /// </summary>
    [Display(Name = "Usual unit")]
    public PriceUnit? SuggestedUnit { get; set; }
}
