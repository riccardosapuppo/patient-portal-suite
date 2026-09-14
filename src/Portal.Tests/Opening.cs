namespace Portal.Tests;

using Portal.Web;
using Xunit;

/// <summary>
/// When the portal opens a browser, and the four times it must not.
/// </summary>
/// <remarks>
/// <para>
/// Every other project in this portfolio opens its own page when it starts, and
/// this one printed an address and waited. A URL in a terminal is a URL somebody
/// has to notice, select and paste, and that tax is charged in the first ten
/// seconds -- before whoever ran it has decided whether it is worth their time.
/// </para>
/// <para>
/// The refusals are what is worth checking rather than the opening. A browser
/// that will not open is a nuisance somebody sees immediately; a browser that
/// opens when nobody is watching is a job on a runner that hangs until it is
/// cancelled, or thirty-five windows during <c>dotnet test</c> -- and this suite
/// starts the portal in process, so that last one is not hypothetical. It is
/// the reason the redirected-output guard is here at all.
/// </para>
/// <para>
/// The opening itself is left alone on purpose. Asserting it would mean either
/// launching a browser on whatever machine runs this, or mocking the launcher
/// and asserting that a mock was called -- which proves the test can call a
/// mock. What these hold is the decision.
/// </para>
/// </remarks>
public class Opening
{
    [Fact]
    public void ARefusalSaysWhichOneItWas()
    {
        // Not a bare false. "It did not open" with no reason becomes a bug
        // report about the portal being broken.
        var (opened, why) = OpenABrowser.Maybe("http://localhost:5000", ["--no-open"]);

        Assert.False(opened);
        Assert.Contains("--no-open", why);
    }

    [Fact]
    public void NoOpenInTheEnvironmentIsTheSameAsTheFlag()
    {
        // For a script that starts this and cannot pass arguments to it.
        Withenvironment("NO_OPEN", "1", () =>
        {
            var (opened, why) = OpenABrowser.Maybe("http://localhost:5000", []);

            Assert.False(opened);
            Assert.Contains("NO_OPEN", why);
        });
    }

    [Fact]
    public void ARunnerIsNeverSentToABrowser()
    {
        // A runner has no browser, and on some of them the launcher blocks
        // instead of failing -- which turns a green job into one that hangs for
        // six hours and is quietly cancelled.
        Withenvironment("CI", "true", () =>
        {
            var (opened, why) = OpenABrowser.Maybe("http://localhost:5000", []);

            Assert.False(opened);
            Assert.Contains("CI", why);
        });
    }

    [Fact]
    public void NothingOpensWhileThisSuiteIsRunning()
    {
        // CI cleared first, and that is the whole lesson of this test.
        //
        // The assertion is about the redirected-output guard -- the one that
        // keeps this suite from opening a browser per test, since it starts the
        // portal in process. Written without clearing CI it passed here and
        // failed on the first runner that saw it, because CI is set there and
        // answers before this guard does: "this is CI", not "terminal". A test
        // that asserts which of four guards won is a test about the machine it
        // was written on.
        Withenvironment("CI", null, () =>
        {
            var (opened, why) = OpenABrowser.Maybe("http://localhost:5000", []);

            Assert.False(opened);
            Assert.Contains("terminal", why);
        });
    }

    /// <summary>Set it, or clear it with null, for the length of one check.</summary>
    private static void Withenvironment(string name, string? value, Action body)
    {
        var before = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);

        try
        {
            body();
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, before);
        }
    }
}
