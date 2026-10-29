using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;

namespace AgriConnect.Models;

/// <summary>
/// Dropdowns whose option <em>text</em> must be translated while their <em>value</em>
/// must not. A district is stored in the database and validated against
/// <see cref="Districts.All"/> on submit, and a category is stored by id, so only the
/// label may change with the language.
///
/// Three controllers built these lists, so three of them showed English options on a
/// Tamil page. One helper, used by all of them.
/// </summary>
public static class Choices
{
    public static SelectList DistrictList(IStringLocalizer text, string? selected = null) =>
        new(Districts.All
                .Select(d => new { Value = d, Text = text[d].Value })
                // Ordered by what the reader sees. Sorting on the English and then
                // displaying Tamil gives a list that looks shuffled.
                .OrderBy(d => d.Text, StringComparer.CurrentCulture)
                .ToList(),
            "Value", "Text", selected);

    public static SelectList CategoryList(
        IStringLocalizer text, IEnumerable<Category> categories, int? selected = null) =>
        new(categories
                .Select(c => new { c.Id, Name = text[c.Name].Value })
                .OrderBy(c => c.Name, StringComparer.CurrentCulture)
                .ToList(),
            "Id", "Name", selected);
}
