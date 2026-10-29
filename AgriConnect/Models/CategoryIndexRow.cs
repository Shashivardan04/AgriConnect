namespace AgriConnect.Models;

/// <summary>
/// Totals for one unit. Sellers choose their own unit, so 6 head and 300 kg cannot be
/// added together and a single "from ₹8,500" across mixed units is true of nothing.
/// Everything is therefore aggregated per unit and labelled with it.
/// </summary>
public class UnitTotal
{
    public PriceUnit Unit { get; set; }
    public decimal LowestPrice { get; set; }

    /// <summary>Null for labour, where a task has no quantity.</summary>
    public decimal? TotalQuantity { get; set; }
}

/// <summary>
/// One row on a category index: a labour task with the people who do it, or a
/// marketplace product with the people selling it.
/// </summary>
public class CategoryIndexRow
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = "";

    /// <summary>Heading this sits under. Null for labour, which renders flat.</summary>
    public string? Group { get; set; }

    /// <summary>Distinct people offering it, not the number of listings.</summary>
    public int Sellers { get; set; }

    /// <summary>
    /// Individual listings. For equipment this is the number that matters: each
    /// machine is its own bookable resource, so one owner with three tractors is
    /// three tractors you could hire, not one owner.
    /// </summary>
    public int Listings { get; set; }

    public List<UnitTotal> ByUnit { get; set; } = [];

    public bool HasAny => Listings > 0;

    /// <summary>
    /// The facts the index needs from one active listing. Kept narrow so the
    /// aggregation can be tested without a database.
    /// </summary>
    public record Facts(int CategoryId, string OwnerId, PriceUnit Unit, decimal Price, decimal? Quantity);

    /// <summary>
    /// Builds a row per category. Every category appears, including ones nobody has
    /// listed against yet, so the index shows what the site covers and not merely what
    /// happens to be on sale today.
    /// </summary>
    public static List<CategoryIndexRow> Build(IEnumerable<Category> categories, IEnumerable<Facts> listings)
    {
        var byCategory = listings.GroupBy(l => l.CategoryId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return categories.Select(category =>
        {
            byCategory.TryGetValue(category.Id, out var rows);
            rows ??= [];

            return new CategoryIndexRow
            {
                CategoryId = category.Id,
                Name = category.Name,
                Group = category.Group,
                // Distinct people: one farmer with three pens of sheep is one seller.
                Sellers = rows.Select(r => r.OwnerId).Distinct().Count(),
                Listings = rows.Count,
                ByUnit = rows
                    .GroupBy(r => r.Unit)
                    .Select(g => new UnitTotal
                    {
                        Unit = g.Key,
                        LowestPrice = g.Min(r => r.Price),
                        // Null, not zero, when nothing in this unit states a quantity -
                        // "0 kg available" would be a lie about an unspecified amount.
                        TotalQuantity = g.Any(r => r.Quantity is not null)
                            ? g.Sum(r => r.Quantity ?? 0)
                            : null
                    })
                    .OrderBy(u => u.Unit)
                    .ToList()
            };
        }).ToList();
    }
}
