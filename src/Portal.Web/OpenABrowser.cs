namespace Portal.Web;

using System.Diagnostics;
using System.Runtime.InteropServices;

/// <summary>
/// Open the portal when it starts, so the first thing somebody does is look at it.
/// </summary>
/// <remarks>
/// <para>
/// A URL printed in a terminal is a URL somebody has to notice, select and
/// paste. That is a small tax and it is charged at exactly the wrong moment:
/// the first ten seconds, before whoever ran this has decided whether it is
/// worth their time. Every other project in this portfolio opens its own page;
/// this one printed an address and waited.
/// </para>
/// <para>
/// ── The four times it must not ──────────────────────────────────────────────
/// </para>
/// <para>
/// A program that opens a browser when nobody is watching is worse than one
/// that never does, and each of these is hard to diagnose from the symptom:
/// </para>
/// <list type="number">
///   <item><c>--no-open</c>, because somebody said so;</item>
///   <item><c>NO_OPEN=1</c>, the same thing for a script that cannot pass arguments;</item>
///   <item>CI is set — a runner has no browser, and on some of them the launcher
///     blocks rather than failing, which turns a green job into one that hangs
///     for six hours and is quietly cancelled;</item>
///   <item>the output is redirected, which covers both a service started by
///     something else and the test host: the suite starts this application in
///     process to talk HTTP to it, and a browser opening thirty-five times
///     during <c>dotnet test</c> would be nobody's idea of a check.</item>
/// </list>
/// <para>
/// It never fails the start. A browser that will not open is a nuisance; a
/// portal that will not start because a browser would not open is a fault.
/// </para>
/// </remarks>
public static class OpenABrowser
{
    /// <summary>What happened, in words, so the caller can log it.</summary>
    /// <remarks>
    /// Silence about not opening is how "it did not open" becomes a bug report
    /// about the portal being broken.
    /// </remarks>
    public static (bool Opened, string Why) Maybe(string url, string[] args)
    {
        if (args.Contains("--no-open"))
        {
            return (false, "--no-open was given");
        }

        var noOpen = Environment.GetEnvironmentVariable("NO_OPEN");
        if (!string.IsNullOrEmpty(noOpen) && noOpen != "0")
        {
            return (false, "NO_OPEN is set");
        }

        var ci = Environment.GetEnvironmentVariable("CI");
        if (!string.IsNullOrEmpty(ci) && ci != "false")
        {
            return (false, "this is CI");
        }

        if (Console.IsOutputRedirected)
        {
            return (false, "nothing is attached to this terminal");
        }

        try
        {
            Process.Start(Launcher(url));
            return (true, "opened in the default browser");
        }
        catch (Exception bother)
        {
            return (false, $"could not open a browser: {bother.Message}");
        }
    }

    private static ProcessStartInfo Launcher(string url)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // UseShellExecute, which is what makes the URL open in whatever the
            // machine calls its browser rather than being looked for as a file.
            return new ProcessStartInfo(url) { UseShellExecute = true };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new ProcessStartInfo("open", url);
        }

        return new ProcessStartInfo("xdg-open", url);
    }
}
