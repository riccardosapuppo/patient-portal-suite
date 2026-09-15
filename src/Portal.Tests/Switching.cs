namespace Portal.Tests;

using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;

using Portal.Store;
using Portal.Web;
using Portal.Web.Pages;
using Xunit;

/// <summary>
/// Two pages open at once, and the browser that keeps them.
/// </summary>
/// <remarks>
/// <para>
/// A portal on a shared machine is a portal people sign out of and back into as
/// somebody else, and both halves of that go wrong in ways nothing was looking
/// at.
/// </para>
/// <para>
/// The first is the antiforgery token, which is tied to the identity it was
/// drawn for — rightly, since that is what stops one person's token being
/// replayed as another's. On the sign-in page, though, the identity is the
/// thing being changed: leave the portal open twice, sign in on one tab, press
/// a patient on the other, and the second form was drawn for nobody while the
/// request arrives as somebody. It was refused with a bare 400 and no body, so
/// the browser drew its own error page, in the language of the machine, with
/// nothing on it naming the portal and no way back.
/// </para>
/// <para>
/// The second is what the browser is allowed to keep. A page listing somebody's
/// reports must not be in a cache that the next person at the same machine can
/// reach with the back button, and "it seems not to be" is not a claim — so it
/// is asked for and read.
/// </para>
/// </remarks>
public class Switching : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> portal;

    public Switching(WebApplicationFactory<Program> portal) => this.portal = portal;

    [Fact]
    public async Task AFormDrawnBeforeSomebodySignedInSendsYouBackToTheForm()
    {
        // One browser, two pages of it: the token is taken from a sign-in page
        // fetched while nobody was signed in, and posted after somebody has.
        using var browser = portal.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        var older = await browser.GetStringAsync("/SignIn");

        var first = Ward.Patients[0];
        var then = Ward.Patients[1];

        var signedIn = await Post(browser, await browser.GetStringAsync("/SignIn"), "/SignIn", first);
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);

        // And now the page that was already on the screen.
        using var stale = await Post(browser, older, "/SignIn", then);

        // Refused — the token is not waived and nothing opts out of the check.
        // What changed is where the refusal lands.
        Assert.Equal(HttpStatusCode.Redirect, stale.StatusCode);

        var sentTo = stale.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains("/SignIn", sentTo, StringComparison.Ordinal);
        Assert.Contains(WhenTheFormWentStale.Stale, sentTo, StringComparison.Ordinal);

        // And the page it lands on has to say what happened, or the redirect
        // has only moved the confusion somewhere prettier.
        var landed = await browser.GetStringAsync(sentTo);

        Assert.Contains("while that page was open", landed, StringComparison.Ordinal);
        Assert.Contains("pick a patient again", landed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThePagesAPatientSeesAreNotKeptByTheBrowser()
    {
        // The back button on a shared machine. A list of somebody's reports
        // sitting in a cache is the same disclosure as handing it over, with
        // the portal not even involved the second time.
        using var browser = portal.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        var who = Ward.Everything().First(one => one.Released && !one.Sensitive).Belongs;
        var page = await browser.GetStringAsync("/SignIn");

        using var signedIn = await Post(browser, page, "/SignIn", who);
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);

        foreach (var where in new[] { "/", $"/Open?id={Ward.Everything().First(one => one.Belongs == who).Id}" })
        {
            using var answer = await browser.GetAsync(where);

            Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
            Assert.True(
                answer.Headers.CacheControl?.NoStore,
                $"{where} went out as "
                + (answer.Headers.CacheControl?.ToString() ?? "nothing at all")
                + ", so a browser may keep it for whoever sits down next.");
        }
    }

    /// <summary>Post the sign-in form, with the token off whichever page is given.</summary>
    private static async Task<HttpResponseMessage> Post(
        HttpClient browser,
        string page,
        string to,
        Portal.Core.PatientId who)
    {
        var token = Token(page) ?? throw new InvalidOperationException($"no antiforgery token on the page posting to {to}");

        return await browser.PostAsync(to, new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("Patient", who.Value),
            new KeyValuePair<string, string>("password", SignInModel.ThePassword),
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
        ]));
    }

    private static string? Token(string page)
    {
        const string Mark = "name=\"__RequestVerificationToken\"";

        var at = page.IndexOf(Mark, StringComparison.Ordinal);
        if (at < 0) return null;

        var value = page.IndexOf("value=\"", at, StringComparison.Ordinal) + 7;

        return page[value..page.IndexOf('"', value)];
    }
}
