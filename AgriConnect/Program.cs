using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.WebEncoders;
using AgriConnect;
using AgriConnect.Data;
using AgriConnect.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// RequireConfirmedAccount stays false: no email sender is configured (SOW §3.3 makes
// email optional), and with it on nobody could ever complete a registration.
builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddScoped<BookingService>();

// English and Tamil. One shared .resx keyed by the English text - see SharedResource.
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options => options.DataAnnotationLocalizerProvider =
        (_, factory) => factory.Create(typeof(SharedResource)));

var supportedCultures = new[] { "en", "ta" };

// Razor's default encoder escapes everything outside Basic Latin, so a Tamil page
// goes out as &#xBB5;&#xBBF;... - correct, and roughly six times the bytes of the
// text itself. Allowing the Tamil block emits it as Tamil. Safe here because every
// response declares UTF-8; the encoder still escapes the characters that matter for
// cross-site scripting.
builder.Services.Configure<WebEncoderOptions>(options =>
    options.TextEncoderSettings = new TextEncoderSettings(
        UnicodeRanges.BasicLatin, UnicodeRanges.Tamil));

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    // Formatting and parsing stay on en-IN in both languages, and only the UI culture
    // switches. That is deliberate. <input type="date"> always posts ISO yyyy-MM-dd
    // and the browser always posts decimals with a dot, but model binding parses them
    // with the request culture - so switching the whole culture to ta-IN would start
    // rejecting perfectly good dates and prices on submit. en-IN also already gives
    // the lakh/crore digit grouping that a rupee figure wants, in both languages.
    options.DefaultRequestCulture = new RequestCulture("en", "en");
    options.SupportedCultures = [new CultureInfo("en-IN")];
    options.SupportedUICultures = supportedCultures.Select(c => new CultureInfo(c)).ToList();
    options.ApplyCurrentCultureToResponseHeaders = true;

    // Cookie first: an explicit choice from the switcher outranks the browser's
    // Accept-Language, and survives the next visit.
    options.RequestCultureProviders =
    [
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    ];
});

// Data protection keys sign the authentication cookie and every antiforgery token.
// The default store is a folder inside the container's own filesystem, so rebuilding
// the image throws them away: everyone is signed out, and any form already open in a
// browser fails its antiforgery check on submit. Point it at a mounted volume.
// Leaving the setting empty - running from source - keeps the default, which already
// persists in the user profile.
if (builder.Configuration["DataProtection:KeyPath"] is { Length: > 0 } keyPath)
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(Directory.CreateDirectory(keyPath))
        // Without a fixed name the key ring is scoped to the content root path, so a
        // move or a rename would silently invalidate every existing cookie.
        .SetApplicationName("AgriConnect");
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await services.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
    await DbSeeder.SeedAsync(services, app.Configuration, app.Environment);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// A status code with no body of its own - 404 from a mistyped URL, 403 from opening
// somebody else's listing, 400 from a malformed request - otherwise renders as a
// blank white page. Re-execute into the error page so the visitor is told what
// happened and given somewhere to go. This sits outside the environment check on
// purpose: an empty 404 is just as unhelpful while developing.
app.UseStatusCodePagesWithReExecute("/Home/Error", "?code={0}");

app.UseHttpsRedirection();
app.UseStaticFiles();

// Before routing, so the culture is already set when a controller or view runs.
app.UseRequestLocalization(
    app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();

/// <summary>
/// Top-level statements compile into an internal Program, which WebApplicationFactory
/// cannot name. Declaring it here lets the HTTP tests start the real application -
/// real routing, real filters, real views - instead of a stand-in that would not have
/// caught the 500 the listing pages used to return.
/// </summary>
public partial class Program;
