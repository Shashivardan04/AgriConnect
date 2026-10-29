namespace AgriConnect.Models;

public class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    /// <summary>The status code being explained. Null for an unhandled exception.</summary>
    public int? StatusCode { get; set; }

    /// <summary>The path the visitor actually asked for, when the server knows it.</summary>
    public string? OriginalPath { get; set; }

    /// <summary>
    /// What to say about each status code. A visitor who mistypes a URL or follows a
    /// stale link should be told which of those happened and be given somewhere to go,
    /// rather than the blank page an unhandled status code produces by default.
    /// </summary>
    public (string Heading, string Detail) Explain() => StatusCode switch
    {
        404 => ("Page not found",
                "That address does not match anything on the site. The listing may have been "
                + "removed by its owner, or the link may be out of date."),
        403 => ("Not yours to open",
                "You are signed in, but this belongs to somebody else. You can only edit "
                + "listings and bookings on your own account."),
        401 => ("Please sign in",
                "This page is only available to members."),
        400 => ("That request did not make sense",
                "The browser sent something the server could not read. Reloading the page "
                + "usually clears it."),
        405 => ("Wrong kind of request",
                "That address does not accept this sort of request."),
        413 => ("That file is too large",
                "Photos must be 4 MB or smaller."),
        >= 500 => ("Something went wrong at our end",
                "The fault is logged. Nothing you did caused it, and trying again shortly "
                + "is usually enough."),
        _ => ("Something went wrong",
              "The page could not be shown.")
    };
}
