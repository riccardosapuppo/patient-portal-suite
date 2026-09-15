namespace Portal.Store;

using System.Globalization;
using System.Text;

using Portal.Core;

/// <summary>
/// The invented reports, printed as PDFs the way the real ones arrive.
/// </summary>
/// <remarks>
/// <para>
/// A portal that hands over three lines of plain text is a portal nobody
/// believes. What comes out of a reporting system is a PDF with a letterhead, a
/// patient block and headed sections, and the difference matters here for one
/// reason beyond appearances: a file that opens in the browser's own viewer is
/// the moment somebody sees that the document really was handed to them, and
/// not merely named on a list.
/// </para>
/// <para>
/// Written out byte by byte rather than with a library. A PDF carrying a
/// letterhead and four headings needs a catalogue, a page, two fonts and one
/// content stream, which is a hundred lines; a dependency that draws it is a
/// dependency to keep, to license and to update, in a repository whose point is
/// partly what it does not have. The cross-reference table is the only fiddly
/// part and it is checked, because getting it wrong is invisible: readers
/// rebuild a broken table silently, so the file opens perfectly and is still
/// malformed.
/// </para>
/// <para>
/// <b>Every page says it is invented, twice.</b> A PDF leaves the portal and
/// keeps going — into a download folder, an email, a slide — and it arrives
/// without the page that framed it. The banner at the top and the line at the
/// foot are the only context that travels with the file, and cropping one does
/// not remove the other.
/// </para>
/// <para>
/// The bytes are the same on every machine and on every run: no timestamps, no
/// identifiers drawn at random, and the body text is wrapped by hand rather
/// than measured against a font. A document whose bytes change when nothing
/// changed cannot be held to anything.
/// </para>
/// </remarks>
public static class Printed
{
    /// <summary>The words that have to travel with the file.</summary>
    /// <remarks>
    /// Held by a check, on every document in the ward, in the bytes rather than
    /// in the source that produced them.
    /// </remarks>
    public const string Invented = "This report is invented. Nobody was examined.";

    /// <summary>A4, in points, which is what a PDF measures in.</summary>
    private const int Wide = 595;

    /// <summary>The height of the page, in points.</summary>
    private const int Tall = 842;

    /// <summary>Two centimetres, near enough, and the same on both sides.</summary>
    private const int Margin = 57;

    /// <summary>One report, as a PDF.</summary>
    /// <param name="id">The accession number, printed in the letterhead.</param>
    /// <param name="who">Whose it is.</param>
    /// <param name="title">What it is called on the list.</param>
    /// <param name="released">Whether a clinician has released it.</param>
    /// <returns>The file.</returns>
    public static byte[] Report(DocumentId id, PatientId who, string title, bool released)
    {
        var page = new Content();

        // The letterhead, green because the portal is green: a report that
        // arrives looking like the place it came from is one fewer thing for
        // somebody to have to check.
        page.Fill(0, Tall - 96, Wide, 96, 0.06, 0.34, 0.22);

        // The same two shapes the portal's header and its tab carry: a sheet,
        // and a seal on the corner of it. A report that arrives looking like
        // the place it came from is one fewer thing to have to check, and this
        // is the part of that which survives being printed.
        page.Mark(Margin, Tall - 66, 22, 1, 1, 1, 0.06, 0.34, 0.22);

        page.Write(Margin + 40, Tall - 52, Font.Bold, 17, "Patient portal", 1, 1, 1);
        page.Write(Margin + 40, Tall - 72, Font.Plain, 9.5, "Imaging and laboratory reporting", 0.72, 0.85, 0.79);
        page.Right(Wide - Margin, Tall - 52, Font.Bold, 11, id.Value, 1, 1, 1);
        page.Right(Wide - Margin, Tall - 72, Font.Plain, 9.5, released ? "Released" : "Draft", 0.72, 0.85, 0.79);

        // The banner: the first of the two places this says what it is.
        page.Fill(0, Tall - 122, Wide, 26, 0.99, 0.95, 0.89);
        page.Write(Margin, Tall - 114, Font.Bold, 9.5, Invented, 0.54, 0.31, 0.07);

        var y = Tall - 168;

        page.Write(Margin, y, Font.Bold, 21, title, 0.06, 0.13, 0.10);
        y -= 34;

        // The patient block, as a form rather than a paragraph: it is read by
        // somebody checking that the right file reached the right person.
        page.Fill(Margin, y - 62, Wide - (2 * Margin), 62, 0.95, 0.96, 0.95);

        var column = Margin + 14;
        foreach (var (name, said) in Fields(id, who, released))
        {
            page.Write(column, y - 24, Font.Plain, 8.5, name, 0.44, 0.52, 0.48);
            page.Write(column, y - 42, Font.Bold, 11, said, 0.06, 0.13, 0.10);
            column += 150;
        }

        y -= 92;

        foreach (var (heading, lines) in Sections(title))
        {
            page.Write(Margin, y, Font.Bold, 11.5, heading, 0.06, 0.34, 0.22);
            page.Rule(Margin, y - 8, Wide - Margin, 0.82, 0.86, 0.84);
            y -= 26;

            foreach (var line in lines)
            {
                page.Write(Margin, y, Font.Plain, 10.5, line, 0.10, 0.16, 0.13);
                y -= 15;
            }

            y -= 18;
        }

        // And the second place, at the foot, where a reader who has scrolled
        // past the banner meets it again.
        page.Rule(Margin, 74, Wide - Margin, 0.82, 0.86, 0.84);
        page.Write(Margin, 58, Font.Plain, 9, Invented, 0.44, 0.52, 0.48);
        page.Write(
            Margin,
            44,
            Font.Plain,
            9,
            "It is placeholder text in a demonstration portal, and is not a medical record.",
            0.44,
            0.52,
            0.48);

        return Assemble(page.Stream(), title);
    }

    /// <summary>The three boxes along the top of the patient block.</summary>
    private static IEnumerable<(string Name, string Said)> Fields(DocumentId id, PatientId who, bool released)
    {
        // A fixed date, not today's. A file whose bytes change overnight cannot
        // be compared with itself, and an invented report has no reason to
        // pretend it was written this morning.
        yield return ("Patient", who.Value);
        yield return ("Accession", id.Value);
        yield return ("Reported", released ? "14 March" : "not yet");
    }

    /// <summary>The headed sections, which differ by what the report is.</summary>
    private static IEnumerable<(string Heading, string[] Lines)> Sections(string title)
    {
        if (title.StartsWith("Serology", StringComparison.Ordinal))
        {
            yield return ("Specimen", ["Invented sample, received by an invented laboratory."]);
            yield return (
                "Results",
                [
                    "Assay A                     not measured          no reference given",
                    "Assay B                     not measured          no reference given",
                    "Assay C                     not measured          no reference given",
                ]);
            yield return (
                "Comment",
                [
                    "Nothing was measured, because there was no sample and no analyser. The",
                    "rows above are here so that a results table has the shape of one.",
                ]);

            yield break;
        }

        if (title.StartsWith("Discharge", StringComparison.Ordinal))
        {
            yield return ("Reason for admission", ["Invented, and left deliberately vague."]);
            yield return (
                "Summary",
                [
                    "Placeholder paragraph. No admission took place and nobody was treated.",
                    "This letter exists so that the portal has something other than a report",
                    "to hand over, and so that a list has more than one kind of row in it.",
                ]);
            yield return ("Follow-up", ["None. There is nothing to follow up."]);

            yield break;
        }

        yield return ("Clinical details", ["Invented referral text, so that the field is not empty."]);
        yield return ("Technique", ["Two views. No examination was performed and no equipment was used."]);
        yield return (
            "Findings",
            [
                "Placeholder paragraph, wrapped by hand so that this file is the same",
                "bytes on every machine that builds it. There is nothing to describe,",
                "because there was nothing to look at.",
            ]);
        yield return (
            "Conclusion",
            [
                "Nothing follows from this document. It exists so that a portal has",
                "something to hand over, and so that a check can ask for it.",
            ]);
    }

    /// <summary>
    /// The objects, the cross-reference table, and the offsets that tie them.
    /// </summary>
    /// <remarks>
    /// The table is a list of byte offsets, each ten digits, and every one has
    /// to be where its object actually starts. Nothing complains when they are
    /// wrong: a reader that finds the table useless rebuilds it by scanning the
    /// file, so the document opens and looks perfect. The offsets are therefore
    /// taken from the stream as it is written and never counted by hand, and a
    /// check reads them back out of the finished bytes.
    /// </remarks>
    private static byte[] Assemble(string content, string title)
    {
        var file = new MemoryStream();
        var at = new List<int>();

        void Put(string text)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            file.Write(bytes, 0, bytes.Length);
        }

        void Thing(string text)
        {
            at.Add((int)file.Length);
            Put(text);
        }

        Put("%PDF-1.4\n");

        Thing("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        Thing("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");
        Thing(
            "3 0 obj\n<< /Type /Page /Parent 2 0 R "
            + $"/MediaBox [0 0 {Wide} {Tall}] "
            + "/Resources << /Font << /F1 5 0 R /F2 6 0 R >> >> "
            + "/Contents 4 0 R >>\nendobj\n");

        Thing(
            $"4 0 obj\n<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n"
            + content
            + "endstream\nendobj\n");

        Thing("5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");
        Thing("6 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>\nendobj\n");
        Thing($"7 0 obj\n<< /Title ({Escaped(title)}) >>\nendobj\n");

        var table = (int)file.Length;

        Put($"xref\n0 {at.Count + 1}\n");
        Put("0000000000 65535 f \n");
        foreach (var offset in at) Put($"{offset:D10} 00000 n \n");

        Put(
            $"trailer\n<< /Size {at.Count + 1} /Root 1 0 R /Info 7 0 R >>\n"
            + $"startxref\n{table}\n%%EOF\n");

        return file.ToArray();
    }

    /// <summary>A string as a PDF literal, with the three characters that bite.</summary>
    private static string Escaped(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);

    /// <summary>Which of the two fonts a run of text is set in.</summary>
    private enum Font
    {
        /// <summary>Helvetica.</summary>
        Plain,

        /// <summary>Helvetica-Bold.</summary>
        Bold,
    }

    /// <summary>The drawing, as the operators a content stream is made of.</summary>
    private sealed class Content
    {
        private readonly StringBuilder said = new();

        /// <summary>A filled rectangle, measured from the bottom left.</summary>
        /// <param name="x">Left edge.</param>
        /// <param name="y">Bottom edge.</param>
        /// <param name="width">How wide.</param>
        /// <param name="height">How tall.</param>
        /// <param name="r">Red, nought to one.</param>
        /// <param name="g">Green.</param>
        /// <param name="b">Blue.</param>
        public void Fill(double x, double y, double width, double height, double r, double g, double b) =>
            said.Append(CultureInfo.InvariantCulture, $"{Num(r)} {Num(g)} {Num(b)} rg\n")
                .Append(CultureInfo.InvariantCulture, $"{Num(x)} {Num(y)} {Num(width)} {Num(height)} re f\n");

        /// <summary>A hairline across the page.</summary>
        /// <param name="from">Where it starts.</param>
        /// <param name="y">Its height up the page.</param>
        /// <param name="to">Where it ends.</param>
        /// <param name="r">Red, nought to one.</param>
        /// <param name="g">Green.</param>
        /// <param name="b">Blue.</param>
        public void Rule(double from, double y, double to, double r, double g, double b) =>
            said.Append(CultureInfo.InvariantCulture, $"{Num(r)} {Num(g)} {Num(b)} RG 0.6 w\n")
                .Append(CultureInfo.InvariantCulture, $"{Num(from)} {Num(y)} m {Num(to)} {Num(y)} l S\n");

        /// <summary>A run of text, with its baseline at y.</summary>
        /// <param name="x">Where it starts.</param>
        /// <param name="y">Its baseline.</param>
        /// <param name="font">Which of the two.</param>
        /// <param name="size">In points.</param>
        /// <param name="text">The words.</param>
        /// <param name="r">Red, nought to one.</param>
        /// <param name="g">Green.</param>
        /// <param name="b">Blue.</param>
        public void Write(double x, double y, Font font, double size, string text, double r, double g, double b) =>
            said.Append("BT\n")
                .Append(CultureInfo.InvariantCulture, $"{Num(r)} {Num(g)} {Num(b)} rg\n")
                .Append(CultureInfo.InvariantCulture, $"/{(font == Font.Bold ? "F2" : "F1")} {Num(size)} Tf\n")
                .Append(CultureInfo.InvariantCulture, $"{Num(x)} {Num(y)} Td ({Escaped(text)}) Tj\n")
                .Append("ET\n");

        /// <summary>A run of text ending at x, for what is set against the right margin.</summary>
        /// <remarks>
        /// Helvetica's widths are not carried here, so a character is taken as
        /// roughly half its point size. It is an approximation and it is allowed
        /// to be: it places two short strings in a letterhead, and the
        /// alternative is a table of widths for the sake of a millimetre.
        /// </remarks>
        /// <param name="x">Where it ends.</param>
        /// <param name="y">Its baseline.</param>
        /// <param name="font">Which of the two.</param>
        /// <param name="size">In points.</param>
        /// <param name="text">The words.</param>
        /// <param name="r">Red, nought to one.</param>
        /// <param name="g">Green.</param>
        /// <param name="b">Blue.</param>
        public void Right(double x, double y, Font font, double size, string text, double r, double g, double b) =>
            Write(x - (text.Length * size * 0.52), y, font, size, text, r, g, b);

        /// <summary>The portal's mark: a sheet, and a seal on its corner.</summary>
        /// <remarks>
        /// Drawn rather than placed, because a raster image in a PDF is a
        /// resolution somebody chose once and a stream to keep in step. Four
        /// curves make the disc: a circle is not a primitive here, and the
        /// constant is the usual one for fitting a bezier to a quarter turn.
        /// </remarks>
        /// <param name="x">Left edge of the sheet.</param>
        /// <param name="y">Bottom edge of the sheet.</param>
        /// <param name="size">How tall the sheet is, in points.</param>
        /// <param name="r">Red of the mark itself, nought to one.</param>
        /// <param name="g">Green of the mark.</param>
        /// <param name="b">Blue of the mark.</param>
        /// <param name="onR">Red of whatever it is drawn on, for the seal's gap.</param>
        /// <param name="onG">Green of whatever it is drawn on.</param>
        /// <param name="onB">Blue of whatever it is drawn on.</param>
        public void Mark(
            double x, double y, double size,
            double r, double g, double b,
            double onR, double onG, double onB)
        {
            // The same proportions the header and the tab are drawn at: a
            // sheet four-fifths as wide as it is tall, and a seal sitting on
            // its bottom-right corner rather than halfway up the side, which
            // is what made the first one look like a bitten rectangle.
            var wide = size * 0.8;

            // The sheet, as an outline.
            said.Append(CultureInfo.InvariantCulture, $"{Num(r)} {Num(g)} {Num(b)} RG {Num(size * 0.1)} w\n")
                .Append(CultureInfo.InvariantCulture, $"{Num(x)} {Num(y)} {Num(wide)} {Num(size)} re S\n");

            // The seal, twice: once in the ground colour so it does not sit on
            // top of the sheet's line, once in the mark's own.
            var seal = (X: x + wide + (size * 0.05), Y: y - (size * 0.05));

            Disc(seal.X, seal.Y, size * 0.375, onR, onG, onB);
            Disc(seal.X, seal.Y, size * 0.26, r, g, b);
        }

        /// <summary>The operators, as the stream they are written into.</summary>
        /// <returns>The content stream.</returns>
        public string Stream() => said.ToString();

        /// <summary>A filled circle, as the four curves a PDF draws one with.</summary>
        private void Disc(double cx, double cy, double radius, double r, double g, double b)
        {
            // 0.5523 is the length, as a fraction of the radius, of the control
            // arms that make a cubic bezier hug a quarter circle.
            var k = radius * 0.5523;

            said.Append(CultureInfo.InvariantCulture, $"{Num(r)} {Num(g)} {Num(b)} rg\n")
                .Append(CultureInfo.InvariantCulture, $"{Num(cx + radius)} {Num(cy)} m\n")
                .Append(CultureInfo.InvariantCulture, $"{Num(cx + radius)} {Num(cy + k)} {Num(cx + k)} {Num(cy + radius)} {Num(cx)} {Num(cy + radius)} c\n")
                .Append(CultureInfo.InvariantCulture, $"{Num(cx - k)} {Num(cy + radius)} {Num(cx - radius)} {Num(cy + k)} {Num(cx - radius)} {Num(cy)} c\n")
                .Append(CultureInfo.InvariantCulture, $"{Num(cx - radius)} {Num(cy - k)} {Num(cx - k)} {Num(cy - radius)} {Num(cx)} {Num(cy - radius)} c\n")
                .Append(CultureInfo.InvariantCulture, $"{Num(cx + k)} {Num(cy - radius)} {Num(cx + radius)} {Num(cy - k)} {Num(cx + radius)} {Num(cy)} c\n")
                .Append("f\n");
        }

        /// <summary>
        /// A number as a PDF writes it: a point, never a comma, whatever the
        /// machine happens to think a decimal looks like.
        /// </summary>
        private static string Num(double value) =>
            Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture);
    }
}
