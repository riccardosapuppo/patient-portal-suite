namespace Portal.Tests;

using System.Net;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using Portal.Core;
using Portal.Store;
using Portal.Web.Pages;
using Xunit;

/// <summary>
/// The portal, started in process and talked to over HTTP.
/// </summary>
/// <remarks>
/// These are the checks that could not be written against a class. Whether a
/// page can be reached without a cookie is a property of the pipeline; whether
/// two refusals look the same to a browser is a property of the bytes on the
/// wire. The original's login controller was reachable anonymously and nothing
/// short of a request would have said so.
/// </remarks>
public class Reaching : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> portal;

    public Reaching(WebApplicationFactory<Program> portal) => this.portal = portal;

    private static Document Hers => Ward.Everything().First(one => one is { Released: true, Sensitive: false });

    private static Document Sensitive => Ward.Everything().First(one => one.Sensitive);

    private static PatientId Stranger => Ward.Patients.First(one => one != Hers.Belongs);

    [Fact]
    public void EveryPageWantsACookieExceptTheTwoThatSayTheyDoNot()
    {
        // Read out of the running application rather than listed here. A page
        // added tomorrow is covered tomorrow, and a page that opts out of
        // authorisation has to appear in this list to pass — which is a thing a
        // reviewer sees in a diff.
        var open = new[] { "/SignIn", "/Error" };

        var endpoints = portal.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .ToList();

        Assert.NotEmpty(endpoints);

        foreach (var endpoint in endpoints)
        {
            var path = "/" + endpoint.RoutePattern.RawText?.TrimStart('/');

            // Anonymous is decided by IAllowAnonymous winning, not by
            // IAuthorizeData being absent: AuthorizeFolder puts the requirement
            // on every page and AllowAnonymousToPage adds an override beside
            // it rather than taking it away. Asking the first question gets the
            // answer "everything is protected", which is the answer this check
            // exists to not take on trust.
            var letsAnyoneIn = endpoint.Metadata.GetOrderedMetadata<IAllowAnonymous>().Count > 0;
            var wantsACookie = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0;

            if (open.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                Assert.True(letsAnyoneIn, $"{path} is on the anonymous list and does not let anyone in");
                continue;
            }

            Assert.False(letsAnyoneIn, $"{path} can be reached without signing in");
            Assert.True(wantsACookie, $"{path} carries no authorisation requirement at all");
        }
    }

    [Fact]
    public async Task WithoutACookieEverythingSendsYouToSignIn()
    {
        using var browser = portal.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        foreach (var where in new[] { "/", $"/Open?id={Hers.Id}", $"/Code?id={Hers.Id}" })
        {
            var answer = await browser.GetAsync(where);

            Assert.Equal(HttpStatusCode.Redirect, answer.StatusCode);
            Assert.Contains("/SignIn", answer.Headers.Location?.OriginalString ?? string.Empty, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task APatientIsHandedTheirOwnDocument()
    {
        var browser = await SignedInAs(Hers.Belongs);

        var answer = await browser.GetAsync($"/Open?id={Hers.Id}");

        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        Assert.Equal("text/plain", answer.Content.Headers.ContentType?.MediaType);
        Assert.Contains(Hers.Id.Value, await answer.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AskingForSomebodyElsesDocumentLooksExactlyLikeAskingForNothing()
    {
        var browser = await SignedInAs(Stranger);

        var real = await browser.GetAsync($"/Open?id={Hers.Id}");
        var invented = await browser.GetAsync($"/Open?id={Ward.NeverExisted}");

        Assert.Equal(real.StatusCode, invented.StatusCode);

        // Byte for byte. A page that differs by a word, a length header, or a
        // whitespace is a page somebody can tell apart, and telling them apart
        // is the whole of the enumeration attack.
        Assert.Equal(
            await real.Content.ReadAsStringAsync(),
            await invented.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TheListShowsNobodyElsesRows()
    {
        var browser = await SignedInAs(Hers.Belongs);
        var page = await browser.GetStringAsync("/");

        foreach (var document in Ward.Everything())
        {
            if (document.Belongs == Hers.Belongs)
            {
                Assert.Contains(document.Id.Value, page, StringComparison.Ordinal);
            }
            else
            {
                Assert.DoesNotContain(document.Id.Value, page, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public async Task ThePatientWithNothingIsDrawnTheEmptyPage()
    {
        // This branch of Index.cshtml has been written since the first commit and
        // no run of this repository had ever reached it: every patient on the
        // ward had documents, so the list was never empty and the "nothing here
        // yet" arm was dead. A written branch no fixture reaches can be wrong for
        // years without anything failing, which is the thesis of this repository
        // pointed at its own tests.
        var browser = await SignedInAs(Ward.NothingYet);

        var page = await browser.GetStringAsync("/");

        Assert.Contains("There is nothing here yet", page, StringComparison.Ordinal);

        // And empty rather than broken: no list drawn at all, and none of the
        // ward's accession numbers on a page whose owner owns none of them.
        Assert.DoesNotContain("class=\"documents\"", page, StringComparison.Ordinal);

        foreach (var document in Ward.Everything())
        {
            Assert.DoesNotContain(document.Id.Value, page, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ASensitiveDocumentTakesACodeAndThenOpens()
    {
        var browser = await SignedInAs(Sensitive.Belongs);

        var asked = await browser.GetStringAsync($"/Code?id={Sensitive.Id}");
        var digits = SixDigitsIn(asked);

        Assert.NotNull(digits);

        var opened = await Send(browser, asked, "/Code",
        [
            new("id", Sensitive.Id.Value),
            new("code", digits),
        ]);

        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        Assert.Equal("text/plain", opened.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ACodeCannotBeAskedForOnSomebodyElsesDocument()
    {
        var browser = await SignedInAs(Stranger);

        var asked = await browser.GetStringAsync($"/Code?id={Sensitive.Id}");

        Assert.Null(SixDigitsIn(asked));
        Assert.Contains("no such document", asked, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ACodeMintedForOneDocumentDoesNotOpenAnother()
    {
        var two = Ward.Everything().Where(one => one.Sensitive).ToList();
        var browser = await SignedInAs(two[0].Belongs);

        var asked = await browser.GetStringAsync($"/Code?id={two[0].Id}");
        var digits = SixDigitsIn(asked);
        Assert.NotNull(digits);

        // Right patient, right digits, wrong document.
        var tried = await Send(browser, asked, "/Code",
        [
            new("id", two[1].Id.Value),
            new("code", digits),
        ]);

        Assert.NotEqual("text/plain", tried.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task TheWrongPasswordSaysTheSameThingAsTheWrongName()
    {
        using var browser = portal.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var wrongPassword = await Refused(Ward.Patients[0].Value, "not-the-password");
        var noSuchPatient = await Refused("nobody-of-that-name", SignInModel.ThePassword);

        // Not the whole page: two renderings of a form carry two different
        // antiforgery tokens. What has to match is what the portal says, which
        // is the part a person reads and the part that would otherwise tell
        // them which half they got right.
        Assert.Equal(wrongPassword, noSuchPatient);
        Assert.NotEmpty(wrongPassword);

        async Task<string> Refused(string patient, string password)
        {
            var answer = await Fill(browser, "/SignIn", "/SignIn",
            [
                new("Patient", patient),
                new("password", password),
            ]);

            Assert.Equal(HttpStatusCode.OK, answer.StatusCode);

            return Said(await answer.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task APostWithoutATokenIsRefused()
    {
        // The first version of these tests posted the fields on their own, and
        // every one of them failed. That was antiforgery working, so the tests
        // were wrong and the portal was right — and the check is worth keeping
        // pointing the other way round.
        using var browser = portal.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var answer = await browser.PostAsync("/SignIn", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("Patient", Ward.Patients[0].Value),
            new KeyValuePair<string, string>("password", SignInModel.ThePassword),
        ]));

        Assert.Equal(HttpStatusCode.BadRequest, answer.StatusCode);
    }

    private async Task<HttpClient> SignedInAs(PatientId who)
    {
        var browser = portal.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        var answer = await Fill(browser, "/SignIn", "/SignIn",
        [
            new("Patient", who.Value),
            new("password", SignInModel.ThePassword),
        ]);

        Assert.Equal(HttpStatusCode.Redirect, answer.StatusCode);

        return browser;
    }

    /// <summary>
    /// Fetch a page, take its antiforgery token, and post the form back.
    /// </summary>
    /// <remarks>
    /// Which is what a browser does, and the reason this helper exists rather
    /// than a bare POST: the first version of these tests posted the fields on
    /// their own and every one of them failed with a 400. That is antiforgery
    /// doing its job, so the tests were wrong and the portal was right — and
    /// the shape of the fix is a check in itself. See
    /// <see cref="APostWithoutATokenIsRefused"/>.
    /// </remarks>
    private static async Task<HttpResponseMessage> Fill(
        HttpClient browser,
        string form,
        string to,
        IReadOnlyList<KeyValuePair<string, string>> fields)
    {
        var page = await browser.GetStringAsync(form);

        return await Send(browser, page, to, fields);
    }

    private static async Task<HttpResponseMessage> Send(
        HttpClient browser,
        string page,
        string to,
        IReadOnlyList<KeyValuePair<string, string>> fields)
    {
        var all = fields.Append(new KeyValuePair<string, string>(
            "__RequestVerificationToken",
            TokenIn(page) ?? throw new InvalidOperationException($"no antiforgery token on the page posting to {to}")));

        return await browser.PostAsync(to, new FormUrlEncodedContent(all));
    }

    private static string Said(string page)
    {
        var at = page.IndexOf("class=\"said\"", StringComparison.Ordinal);
        if (at < 0) return string.Empty;

        var from = page.IndexOf('>', at) + 1;

        return page[from..page.IndexOf('<', from)].Trim();
    }

    private static string? TokenIn(string page)
    {
        const string Mark = "name=\"__RequestVerificationToken\"";

        var at = page.IndexOf(Mark, StringComparison.Ordinal);
        if (at < 0) return null;

        var value = page.IndexOf("value=\"", at, StringComparison.Ordinal) + 7;

        return page[value..page.IndexOf('"', value)];
    }

    private static string? SixDigitsIn(string page)
    {
        var mark = page.IndexOf("class=\"code\"", StringComparison.Ordinal);
        if (mark < 0) return null;

        var from = page.IndexOf('>', mark) + 1;
        var to = page.IndexOf('<', from);

        var digits = page[from..to].Trim();

        return digits.Length == 6 && digits.All(char.IsDigit) ? digits : null;
    }
}
