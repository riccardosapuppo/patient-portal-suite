namespace Portal.Store;

using System.Text;

using Portal.Core;

/// <summary>
/// An invented ward: six patients, fourteen documents, and every combination
/// that matters.
/// </summary>
/// <remarks>
/// <para>
/// Invented, and it has to be said plainly why. A patient portal is the one
/// place where a realistic fixture is a disclosure: a name, a date of birth and
/// a report title together are a person, and a repository is forever. Nothing
/// here comes from anywhere. The names are ordinary Italian given names with no
/// surname, the reports are three lines of placeholder text, and the identifiers
/// are sequential.
/// </para>
/// <para>
/// What the ward is <em>for</em> is coverage. The measurement asks every patient
/// for every document over every route, so the shape that matters is the
/// combinations: a document that is theirs and released, theirs and still a
/// draft, theirs and sensitive, somebody else's in each of those three states,
/// and an accession number that is not in the archive at all. Without the last
/// one there is no way to show that a refusal says the same thing either way.
/// </para>
/// </remarks>
public static class Ward
{
    /// <summary>The patients, in the order the measurement prints them.</summary>
    public static IReadOnlyList<PatientId> Patients { get; } =
    [
        new("giulia"), new("marco"), new("elena"), new("davide"), new("sara"), new("paolo"),
    ];

    /// <summary>
    /// An accession number that is not in the archive.
    /// </summary>
    /// <remarks>
    /// The whole of the third claim rests on this. A portal that answers
    /// differently for a document that exists and one that does not has handed
    /// a stranger a way to enumerate the archive, one guess at a time, without
    /// ever being given a single byte.
    /// </remarks>
    public static DocumentId NeverExisted { get; } = new("ACC-000000");

    /// <summary>Fill an archive with the ward.</summary>
    /// <param name="archive">The archive to fill.</param>
    /// <param name="cancel">To give up.</param>
    /// <returns>When it is filled.</returns>
    public static async Task FillIn(Archive archive, CancellationToken cancel = default)
    {
        foreach (var document in Everything()) await archive.Put(document, cancel);
    }

    /// <summary>Every document in the ward.</summary>
    /// <returns>Fourteen documents across six patients, and one of them has none.</returns>
    public static IReadOnlyList<Document> Everything()
    {
        var documents = new List<Document>();
        var number = 100100;

        // Three of each for the first two patients, so that "theirs, released",
        // "theirs, draft" and "theirs, sensitive" all have a neighbour that is
        // somebody else's and in the same state.
        foreach (var who in Patients.Take(2))
        {
            documents.Add(Make(ref number, who, "Radiology report", released: true, sensitive: false));
            documents.Add(Make(ref number, who, "Radiology report, draft", released: false, sensitive: false));
            documents.Add(Make(ref number, who, "Serology results", released: true, sensitive: true));
        }

        foreach (var who in Patients.Skip(2))
        {
            documents.Add(Make(ref number, who, "Discharge letter", released: true, sensitive: false));
            documents.Add(Make(ref number, who, "Ultrasound report", released: true, sensitive: false));
        }

        // One patient with nothing at all. A portal that only ever runs against
        // patients who have documents has never drawn its own empty page.
        return documents;
    }

    private static Document Make(ref int number, PatientId who, string title, bool released, bool sensitive)
    {
        var id = new DocumentId($"ACC-{number:D6}");
        number += 137;

        var text =
            $"""
            {title}
            Patient: {who}
            Accession: {id}

            This document is invented. It exists so that a test can ask for it.
            """;

        return new Document(id, who, title, released, sensitive, Encoding.UTF8.GetBytes(text));
    }
}
