namespace Portal.Tests;

using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;

using Portal.Store;
using Portal.Web.Pages;
using Xunit;

/// <summary>
/// The sign-in page, and the six cards somebody picks from.
/// </summary>
/// <remarks>
/// <para>
/// The first person to open this portal submitted an empty form and asked what
/// the credentials were, with the answer three inches below the button. So the
/// page now offers the ward as six cards, one click each, and each card says in
/// the documents' own three colours what signing in as that patient will show.
/// </para>
/// <para>
/// Which makes the card a second place the ward is described. The first went
/// stale without anybody noticing -- the README said one patient had no
/// documents while all six had some -- and a card is worse than a sentence,
/// because somebody chooses with it: a card promising a report that asks for a
/// code, on a patient who has none, sends a reader to the one page in this
/// repository that has nothing to show them.
/// </para>
/// <para>
/// So the counts are held against the archive here, and the wording is held
/// against the page. Nothing in this file names a patient or a number.
/// </para>
/// </remarks>
public class Choosing : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> portal;

    public Choosing(WebApplicationFactory<Program> portal) => this.portal = portal;

    [Fact]
    public void EveryPatientInTheWardIsOneOfTheCards()
    {
        // Not six. The ward is the thing being described, so it is also the
        // thing being counted -- a literal here would go stale on the same day
        // the page did, and agree with it.
        Assert.Equal(Ward.Patients.Count, new SignInModel().Choices.Count);

        foreach (var who in Ward.Patients)
        {
            Assert.Contains(new SignInModel().Choices, one => one.Who == who.Value);
        }
    }

    [Fact]
    public void ACardCountsWhatSigningInAsThemWouldShow()
    {
        foreach (var card in new SignInModel().Choices)
        {
            var theirs = Ward.Everything().Where(one => one.Belongs.Value == card.Who).ToList();

            Assert.Equal(theirs.Count(one => one is { Released: true, Sensitive: false }), card.Readable);
            Assert.Equal(theirs.Count(one => one is { Released: true, Sensitive: true }), card.Guarded);
            Assert.Equal(theirs.Count(one => !one.Released), card.Waiting);

            // The empty patient exists on purpose: until the ward had somebody
            // with nothing in it, no run of this portal ever drew an empty list.
            Assert.Equal(theirs.Count == 0, card.Empty);
        }
    }

    [Fact]
    public async Task ThePageOffersThemInWordsAndNotOnlyInColour()
    {
        using var browser = portal.CreateClient();
        using var answer = await browser.GetAsync("/SignIn");

        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);

        var page = await answer.Content.ReadAsStringAsync();
        var cards = new SignInModel().Choices;

        // A coloured edge is not a sentence, and two of these three colours are
        // the pair most often confused. Every count on the page is checked as
        // the words beside the dot rather than as the dot.
        Assert.Equal(cards.Count(one => one.Readable > 0), Times(page, "to read</span>"));
        Assert.Equal(cards.Count(one => one.Guarded > 0), Times(page, "needs a code</span>"));
        Assert.Equal(cards.Count(one => one.Waiting > 0), Times(page, "not signed off</span>"));
        Assert.Equal(cards.Count(one => one.Empty), Times(page, "nothing yet"));

        // And the cards have to look like the buttons they are. The shadow
        // alone was not enough for the person who opened this and went looking
        // for something to press.
        Assert.Equal(cards.Count, Times(page, "Open their file"));
    }

    [Fact]
    public async Task TheTypedWayIsNotBehindAScript()
    {
        // The cards are the way in and the typed form is the thing beside
        // them, now behind a button -- but behind a button only once there is
        // a script to open it with.
        //
        // The markup therefore ships the dialog already open and the button
        // hidden, and the script does it the other way round. Written the
        // obvious way round instead, a browser that never runs the script gets
        // a page with a dead button on it and no form at all: a way in that
        // depends on JavaScript having run is a way in somebody does not have.
        //
        // This is what the checks in Reaching drive, so losing it would not
        // merely lose a way in -- it would take the checks with it.
        using var browser = portal.CreateClient();
        var page = await browser.GetStringAsync("/SignIn");

        // Open in the markup, so it is on the page before anything runs.
        Assert.Contains("<dialog class=\"typed\" open>", page, StringComparison.Ordinal);

        // And the opener hidden in the markup, so nothing dead is ever shown.
        Assert.Contains("class=\"typed-open\" hidden", page, StringComparison.Ordinal);

        // The form itself, with everybody on it and somewhere to type.
        foreach (var who in Ward.Patients)
        {
            Assert.Contains($"<option value=\"{who}\">", page, StringComparison.Ordinal);
        }

        Assert.Contains("type=\"password\"", page, StringComparison.Ordinal);
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
