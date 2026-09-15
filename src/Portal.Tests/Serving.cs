namespace Portal.Tests;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Mvc.Testing;

using Portal.Web;
using Xunit;

/// <summary>
/// The stylesheet, and whether a browser is allowed to show yesterday's.
/// </summary>
/// <remarks>
/// <para>
/// A response carrying an ETag and a Last-Modified and no Cache-Control is not
/// an uncached response. It is an unspecified one, and the browser is entitled
/// to guess how long it stays fresh -- the usual guess being a tenth of the
/// file's age. A stylesheet untouched for a week is then fresh for most of a
/// day, and the ETag beside it is never asked about once.
/// </para>
/// <para>
/// So somebody changes the look of this portal, restarts it, reloads, and is
/// shown what they had before. Nothing failed. Nothing was logged. The next
/// hour goes into the CSS, which was right all along. That is the same shape as
/// everything else in this repository: not a thing that broke, a thing that was
/// never asked.
/// </para>
/// <para>
/// Both halves are checked here, because only the pair is worth anything: the
/// answer has to say revalidate, and revalidating has to be cheap. A header
/// that forced a full download every time would be traded away by the first
/// person who measured the page.
/// </para>
/// </remarks>
public class Serving : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> portal;

    public Serving(WebApplicationFactory<Program> portal) => this.portal = portal;

    [Fact]
    public async Task TheStylesheetIsAskedAboutRatherThanAssumedFresh()
    {
        using var browser = portal.CreateClient();
        using var answer = await browser.GetAsync("/portal.css");

        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);

        // Not merely "a Cache-Control is present". Present and permissive is
        // the failure being guarded against.
        Assert.True(
            answer.Headers.CacheControl?.NoCache,
            "The stylesheet went out as " + (answer.Headers.CacheControl?.ToString() ?? "nothing at all")
            + ", which leaves a browser free to decide for itself how long yesterday's copy is good for.");
    }

    [Fact]
    public async Task AndAskingCostsNothingWhenItHasNotChanged()
    {
        using var browser = portal.CreateClient();

        using var first = await browser.GetAsync("/portal.css");
        var tag = first.Headers.ETag;

        Assert.NotNull(tag);

        using var again = new HttpRequestMessage(HttpMethod.Get, "/portal.css");
        again.Headers.IfNoneMatch.Add(tag);

        using var second = await browser.SendAsync(again);

        // The whole argument for revalidating is that the answer is usually
        // this one: no body, and the browser uses what it already has.
        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
        Assert.Equal(0, second.Content.Headers.ContentLength ?? 0);
    }

    [Fact]
    public async Task EveryFontTheStylesheetNamesActuallyAnswers()
    {
        // A font that does not load is invisible. The page renders, in whatever
        // the machine had lying about, and nobody is told -- not the reader,
        // not the log, not the build. Rename a file, move a folder, or leave one
        // out of the publish, and the portal quietly goes back to being set in
        // system defaults while every check that looks at markup stays green.
        //
        // So the stylesheet is read as the browser reads it, and every file it
        // names is fetched.
        using var browser = portal.CreateClient();

        var sheet = await browser.GetStringAsync("/portal.css");
        var named = Regex.Matches(sheet, @"url\('([^']+)'\)")
            .Select(one => one.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Nothing named is a pass by finding nothing, which is the failure this
        // whole file is about.
        Assert.NotEmpty(named);

        foreach (var file in named)
        {
            using var answer = await browser.GetAsync($"/{file}");

            Assert.True(
                answer.StatusCode == HttpStatusCode.OK,
                $"The stylesheet asks for {file} and the portal answers {(int)answer.StatusCode}. "
                + "Nothing would have said so: the page renders in something else.");

            // And it has to be a font rather than, say, an error page with a
            // helpful status code on it.
            var bytes = await answer.Content.ReadAsByteArrayAsync();

            Assert.True(
                bytes.Length > 4 && Encoding.ASCII.GetString(bytes, 0, 4) == "wOF2",
                $"{file} answered {bytes.Length} bytes that do not begin with a woff2 signature.");
        }
    }

    [Fact]
    public async Task ThePortalHasAMarkOfItsOwn()
    {
        using var browser = portal.CreateClient();

        // In the tab.
        using var icon = await browser.GetAsync("/favicon.svg");

        Assert.Equal(HttpStatusCode.OK, icon.StatusCode);
        Assert.Contains("<svg", await icon.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        // And on the page, drawn rather than fetched, so it is the band's own
        // colour wherever the band is.
        var page = await browser.GetStringAsync("/SignIn");

        Assert.Contains("favicon.svg", page, StringComparison.Ordinal);
        Assert.Contains("mark-seal", page, StringComparison.Ordinal);
        Assert.Contains("currentColor", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AndTheAnswerChangesWhenTheFileHas()
    {
        using var browser = portal.CreateClient();

        // A stale tag stands for the copy somebody edited the file out from
        // under. Answering 304 to this is exactly the bug: it would hand back
        // a page the file no longer describes.
        using var asking = new HttpRequestMessage(HttpMethod.Get, "/portal.css");
        asking.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"not-the-one-on-disk\""));

        using var answer = await browser.SendAsync(asking);

        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        Assert.True(answer.Content.Headers.ContentLength > 0);
    }
}
