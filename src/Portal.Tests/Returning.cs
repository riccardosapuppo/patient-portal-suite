namespace Portal.Tests;

using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;

using Portal.Store;
using Portal.Web;
using Portal.Web.Pages;
using Xunit;

/// <summary>
/// Getting back from a document.
/// </summary>
/// <remarks>
/// <para>
/// Pressing Open navigated away, handed over a PDF, and left somebody in the
/// browser's document viewer with nothing to press. The way back was the back
/// button — the one button people using a portal have been taught not to
/// trust, and the one this portal spends a check making sure shows them
/// nothing.
/// </para>
/// <para>
/// So the list is the place you work from and a report arrives on top of it.
/// What is held here is not the dialog, which is a script and a stylesheet and
/// could be written twenty ways; it is that <b>no route out of the list is a
/// one-way street</b>. Every report is reachable without losing the page it was
/// listed on, with the script or without it, and the page a code is asked for
/// on says how to get back.
/// </para>
/// </remarks>
public class Returning : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> portal;

    public Returning(WebApplicationFactory<Program> portal) => this.portal = portal;

    [Fact]
    public async Task OpeningAReportDoesNotCostYouTheList()
    {
        var who = Ward.Everything().First(one => one.Released && !one.Sensitive).Belongs;
        using var browser = await SignedInAs(who);

        var list = await browser.GetStringAsync("/");
        var theirs = Ward.Everything()
            .Where(one => one.Belongs == who && one is { Released: true, Sensitive: false })
            .ToList();

        Assert.NotEmpty(theirs);

        foreach (var document in theirs)
        {
            // The link is real and goes to the real document: the dialog is an
            // enhancement over it, not a replacement for it.
            Assert.Contains($"href=\"/Open?id={document.Id}\"", list, StringComparison.Ordinal);

            // Which the script finds by, and which is also what it prints in
            // the bar so the reader can see which report they are looking at.
            Assert.Contains($"data-open=\"{document.Id}\"", list, StringComparison.Ordinal);
        }

        // And with the script turned off, the link still has to leave the list
        // standing. That is what the target does, and it is the whole of the
        // fallback.
        Assert.Contains("target=\"_blank\"", list, StringComparison.Ordinal);

        // One place for the report to arrive, and something to close it with.
        Assert.Equal(1, Times(list, "<dialog class=\"viewer\""));
        Assert.Contains("viewer-shut", list, StringComparison.Ordinal);
        Assert.Contains("viewer.js", list, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheCodePageSaysHowToGetBack()
    {
        // The page somebody lands on when a report asks for a code, which is
        // the deepest anybody goes in this portal. It offered a way back when
        // it was refusing and none at all when it was working.
        var guarded = Ward.Everything().First(one => one.Sensitive);
        using var browser = await SignedInAs(guarded.Belongs);

        var asked = await browser.GetStringAsync($"/Code?id={guarded.Id}");

        Assert.Contains("Back to your documents", asked, StringComparison.Ordinal);
        Assert.Contains("href=\"/\"", asked, StringComparison.Ordinal);
    }

    private async Task<HttpClient> SignedInAs(Portal.Core.PatientId who)
    {
        var browser = portal.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        var page = await browser.GetStringAsync("/SignIn");
        var token = Token(page) ?? throw new InvalidOperationException("no antiforgery token on the sign-in page");

        using var answer = await browser.PostAsync("/SignIn", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("Patient", who.Value),
            new KeyValuePair<string, string>("password", SignInModel.ThePassword),
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
        ]));

        Assert.Equal(HttpStatusCode.Redirect, answer.StatusCode);

        return browser;
    }

    private static string? Token(string page)
    {
        const string Mark = "name=\"__RequestVerificationToken\"";

        var at = page.IndexOf(Mark, StringComparison.Ordinal);
        if (at < 0) return null;

        var value = page.IndexOf("value=\"", at, StringComparison.Ordinal) + 7;

        return page[value..page.IndexOf('"', value)];
    }

    private static int Times(string page, string phrase)
    {
        var found = 0;

        for (var at = page.IndexOf(phrase, StringComparison.Ordinal); at >= 0;
             at = page.IndexOf(phrase, at + phrase.Length, StringComparison.Ordinal))
        {
            found++;
        }

        return found;
    }
}
