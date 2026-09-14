using System.Security.Claims;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

using Portal.Core;
using Portal.Store;
using Portal.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
    // Signed in by default, anonymous by exception.
    //
    // This one line is the structural half of the whole repository. In the
    // code this was rebuilt from, every controller carried its own
    // [Authorize] and one of them did not: the login controller, which then
    // read the signed-in patient out of the session and threw a
    // NullReferenceException when there was not one. The exception was caught
    // and returned as "Eccezione Login RequestCode", so the route was
    // protected by an accident, and would have stopped being protected the day
    // somebody added a null check to tidy the log up.
    //
    // Turned round, forgetting is safe: a new page is signed-in-only until
    // somebody writes its name below, and writing a name there is a thing a
    // reviewer notices.
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/SignIn");
    options.Conventions.AllowAnonymousToPage("/Error");
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/SignIn";
        options.AccessDeniedPath = "/SignIn";

        // Twenty minutes, and not sliding. A portal that renews the session on
        // every page view is a portal where a browser left open on a ward
        // computer stays signed in all afternoon.
        options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
        options.SlidingExpiration = false;

        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddAuthorization();

builder.Services.AddSingleton(_ =>
{
    var archive = Archive.Open();
    Ward.FillIn(archive).GetAwaiter().GetResult();
    return archive;
});

builder.Services.AddSingleton<IDocuments>(services => services.GetRequiredService<Archive>());
builder.Services.AddSingleton<ITrail, TrailInMemory>();
builder.Services.AddSingleton<ISendCodes, CodesOnTheScreen>();

builder.Services.AddSingleton(services => new SecondFactor(
    services.GetRequiredService<IDocuments>(),
    services.GetRequiredService<ISendCodes>(),
    SixDigits.Next));

var app = builder.Build();

app.UseStatusCodePagesWithReExecute("/Error", "?code={0}");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

/*
 * Open the portal on the address it actually bound.
 *
 * After it has started, not before: a browser sent to a port nothing is
 * listening on yet shows a connection refused, and whoever ran this reads that
 * as the program being broken rather than as being early.
 *
 * And the address it bound, asked of the server, rather than the one the README
 * quotes. They are the same until somebody passes --urls, and then they are
 * not -- and a browser opened on the wrong one of the two is worse than none,
 * because the page it shows belongs to whatever else is on that port.
 */
app.Lifetime.ApplicationStarted.Register(() =>
{
    var bound = app.Services
        .GetService<IServer>()?
        .Features.Get<IServerAddressesFeature>()?
        .Addresses.FirstOrDefault();

    var (opened, why) = OpenABrowser.Maybe(bound ?? "http://localhost:5000", args);

    app.Logger.LogInformation(
        opened ? "The portal is open in your browser: {At}" : "The portal is at {At}, not opened: {Why}",
        bound ?? "http://localhost:5000",
        why);
});

app.Run();

/// <summary>
/// Named so the tests can start this application in process and talk HTTP to it.
/// </summary>
/// <remarks>
/// A test that drives the pages through a real server is the only kind that can
/// catch a route being reachable without a cookie, because that is a property of
/// the pipeline and not of any class in it.
/// </remarks>
public partial class Program
{
    /// <summary>Which claim carries the patient's identifier.</summary>
    public const string PatientClaim = ClaimTypes.NameIdentifier;
}
