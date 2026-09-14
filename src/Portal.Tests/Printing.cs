namespace Portal.Tests;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using Portal.Measure;
using Portal.Store;
using Xunit;

/// <summary>
/// The reports, as the files that leave the building.
/// </summary>
/// <remarks>
/// <para>
/// The portal now hands over PDFs rather than three lines of text, and a PDF
/// written out by hand fails in a way worth checking: readers repair it. A
/// cross-reference table whose offsets are wrong is not an error anybody sees,
/// because every viewer worth the name scans the file and rebuilds the table
/// when it finds one it cannot use. The document opens, looks perfect, and is
/// malformed — so "I opened it and it was fine" proves nothing at all, and the
/// offsets have to be read back out of the bytes.
/// </para>
/// <para>
/// The other two are about what the file says and whether it says the same
/// thing twice. A report leaves the portal and keeps going, into a download
/// folder or an email, and arrives without the page that framed it; the words
/// on the page are the only context that travels with it. And bytes that
/// change when nothing changed cannot be compared with anything, including
/// with themselves.
/// </para>
/// </remarks>
public class Printing
{
    [Fact]
    public void EveryReportIsAPdfThatSaysItIsInvented()
    {
        var ward = Ward.Everything();

        Assert.NotEmpty(ward);

        foreach (var document in ward)
        {
            var file = Encoding.ASCII.GetString(document.Content);

            Assert.StartsWith("%PDF-", file, StringComparison.Ordinal);
            Assert.EndsWith("%%EOF\n", file, StringComparison.Ordinal);

            // Twice, and both are asserted: the banner at the top and the line
            // at the foot. Cropping one is not meant to remove the other.
            Assert.Equal(2, Times(file, Printed.Invented));

            // And it has to be this patient's, on the page and not only in the
            // database row. A report handed to the right person with somebody
            // else's name printed on it is the same failure one floor up.
            Assert.Contains($"({document.Id.Value})", file, StringComparison.Ordinal);
            Assert.Contains($"({document.Belongs.Value})", file, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheCrossReferenceTablePointsAtTheObjects()
    {
        // The check that "does it open" cannot make. Every offset in the table
        // is read out of the finished bytes and has to land exactly on the
        // object it claims — because a reader that finds it wrong says nothing
        // and rebuilds it, and the file is broken for everything that does not.
        foreach (var document in Ward.Everything())
        {
            var file = document.Content;
            var text = Encoding.ASCII.GetString(file);

            var start = Regex.Match(text, @"startxref\s+(\d+)");
            Assert.True(start.Success, "No startxref, so nothing here was checked.");

            var table = int.Parse(start.Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.StartsWith("xref", text[table..], StringComparison.Ordinal);

            var head = Regex.Match(text[table..], @"^xref\s+0 (\d+)\s+");
            Assert.True(head.Success, "The table has no header, so nothing here was checked.");

            var objects = int.Parse(head.Groups[1].Value, CultureInfo.InvariantCulture);
            var rows = table + head.Length;

            Assert.True(objects > 1, "A table of no objects would pass every assertion below it.");

            // Entry nought is the free head of the list and points nowhere.
            // Every other one names an object, and each entry is twenty bytes.
            for (var n = 1; n < objects; n++)
            {
                var offset = int.Parse(text.Substring(rows + (n * 20), 10), CultureInfo.InvariantCulture);
                var says = $"{n} 0 obj";

                Assert.True(
                    offset + says.Length <= text.Length
                    && text.Substring(offset, says.Length) == says,
                    $"{document.Id}: the table sends a reader to byte {offset} for object {n}, "
                    + $"and what is there is \"{Glimpse(text, offset)}\".");
            }
        }
    }

    [Fact]
    public void TheSameReportIsTheSameBytes()
    {
        // No timestamp, no identifier drawn at random, no font measured at run
        // time. Two builds of the ward have to be byte for byte the same file,
        // or nothing downstream — a cache, a comparison, a check — means
        // anything.
        var once = Ward.Everything();
        var again = Ward.Everything();

        Assert.Equal(once.Count, again.Count);

        for (var n = 0; n < once.Count; n++)
        {
            Assert.Equal(once[n].Id, again[n].Id);
            Assert.Equal(once[n].Content, again[n].Content);
        }
    }

    [Fact]
    public void TheReportInTheRepositoryIsTheOneThePortalWouldHandYou()
    {
        // A picture of a PDF is a picture. The file itself is in docs/, so
        // anybody reading the repository can open the thing rather than look at
        // a screenshot of it -- and a committed artefact drifts from the code
        // that made it unless something says otherwise.
        //
        // It can say so here only because the bytes are settled: see
        // <see cref="TheSameReportIsTheSameBytes"/>. Without that this check
        // would fail every time somebody built it on another machine, and would
        // be deleted within a week.
        var kept = Path.Combine(TheReadme.Root(), "docs", "a-report.pdf");

        Assert.True(File.Exists(kept), $"{kept} is not there, so nothing here was compared.");

        var shown = Ward.Everything().First(one => one.Released && !one.Sensitive);

        Assert.Equal(shown.Content, File.ReadAllBytes(kept));
    }

    private static int Times(string file, string phrase)
    {
        var found = 0;

        for (var at = file.IndexOf(phrase, StringComparison.Ordinal); at >= 0;
             at = file.IndexOf(phrase, at + phrase.Length, StringComparison.Ordinal))
        {
            found++;
        }

        return found;
    }

    private static string Glimpse(string text, int at) =>
        at < 0 || at >= text.Length
            ? "nothing — the offset is outside the file"
            : text[at..Math.Min(text.Length, at + 12)].Replace('\n', ' ');
}
