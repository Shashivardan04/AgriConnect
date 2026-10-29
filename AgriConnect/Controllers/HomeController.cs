using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AgriConnect.Models;

namespace AgriConnect.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    /// <summary>
    /// Both the unhandled-exception page and every status code without a body of its
    /// own, which is what UseStatusCodePagesWithReExecute sends here. Without that,
    /// a 404 or a 403 is a blank white page: the status is correct but the visitor is
    /// told nothing and given no way back.
    /// </summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? code)
    {
        var reExecute = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();

        if (code is int status)
        {
            // Re-executing resets the response, so put the real code back. Otherwise a
            // missing page answers 200 with "page not found" written on it, which
            // search engines and scripts both take at face value.
            Response.StatusCode = status;

            if (status >= 500)
                _logger.LogError("Status {Status} for {Path}", status, reExecute?.OriginalPath);
        }

        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            StatusCode = code,
            OriginalPath = reExecute?.OriginalPath
        });
    }
}
