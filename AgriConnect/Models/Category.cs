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

    /// <summary>
    /// Milk and eggs come every morning; a sack of onions does not. For these the
    /// quantity on a listing is a DAILY capacity that renews rather than a stock that
    /// runs down, and a buyer may take some every day over a stretch of dates.
    ///
    /// It sits on the category rather than the listing so that an administrator settles
    /// it once per product, the same way Group and SuggestedUnit are settled. False for
    /// equipment and labour, which are not sold by quantity at all.
    /// </summary>
    [Display(Name = "Can be bought daily")]
    public bool SellsDaily { get; set; }
}
