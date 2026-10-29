using System.Globalization;

namespace AgriConnect.Models;

/// <summary>
/// Dates shown in the language being read.
///
/// The request culture is pinned to en-IN on purpose (see Program.cs): a browser
/// always posts <c>&lt;input type="date"&gt;</c> as ISO and decimals with a dot, and
/// model binding parses them with the request culture, so switching that to ta-IN
/// would start rejecting valid dates and prices on submit.
///
/// That decision is about <em>parsing</em>. It left every date on screen reading
/// "21 Nov 2026" even in Tamil. These format against CurrentUICulture instead, which
/// is the language the reader chose, and nothing here is ever parsed back.
/// </summary>
public static class Dates
{
    /// <summary>"21 Nov 2026" - a date with its year.</summary>
    public static string Full(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.CurrentUICulture);

    public static string Full(DateTime moment) =>
        moment.ToString("d MMM yyyy", CultureInfo.CurrentUICulture);

    /// <summary>"21 Nov" - the near end of a range, where the year comes once at the end.</summary>
    public static string Short(DateOnly date) => date.ToString("d MMM", CultureInfo.CurrentUICulture);

    /// <summary>"21 Nov, 14:30" - when something arrived.</summary>
    public static string Moment(DateTime moment) =>
        moment.ToString("d MMM, HH:mm", CultureInfo.CurrentUICulture);
}
