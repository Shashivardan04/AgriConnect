namespace AgriConnect;

/// <summary>
/// Marker type for the one shared translation file, Resources/SharedResource.ta.resx.
///
/// It lives in the root namespace deliberately. The resource lookup turns the type's
/// namespace below the root into a path under ResourcesPath, so a marker in
/// AgriConnect.Models would have to be paired with Resources/Models.SharedResource.ta.resx.
/// Keeping it at the root keeps the file where anyone would expect to find it.
///
/// Every string is keyed by its own English text rather than by a code like
/// "Nav_Equipment". Two reasons: a view reads as English prose instead of a list of
/// identifiers, and a key with no Tamil entry yet falls back to the key itself, which
/// is already the correct English. So there is no English resource file to keep in
/// step, and a half-finished translation shows Tamil where it exists and English
/// where it does not, rather than "Nav_Equipment" on the page.
///
/// Category and district names are localised through the same file. They live in the
/// database, but they are a fixed seed set that administrators pick from, not free
/// text, so a translation keyed on the English name works and needs no column.
/// Titles and descriptions that members type stay in whatever language they typed.
/// </summary>
public class SharedResource;
