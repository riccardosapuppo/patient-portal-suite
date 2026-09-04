using System.Security.Claims;

using Microsoft.AspNetCore.Authentication.Cookies;

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
