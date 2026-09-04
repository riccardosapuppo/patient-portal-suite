namespace Portal.Core;

/// <summary>
/// The four routes as they were, kept runnable so the difference can be counted.
/// </summary>
/// <remarks>
/// <para>
/// All four begin the same way — read the signed-in patient out of the session,
/// load them, write them into the log line — and then two of them put that
/// patient into the question and two do not. The two that do not take the
/// document identifier straight out of the request body and send it on.
/// </para>
/// <para>
/// It is worth being clear that this was not carelessness about security. The
/// author knew the patient mattered: the patient is loaded on every route, and
/// named in every log line. What went missing is smaller and harder to see —
/// <b>the loaded value was never passed to the thing that answers</b>. Reading
/// the file, all four routes look alike. They are alike, right up to the line
/// that matters.
/// </para>
/// <para>
/// That is why the replacement does not add a check to the two that were wrong.
/// A check is another line that can be absent, and absence is exactly what this
/// file demonstrates is invisible. <see cref="Asked"/> makes the wrong version
/// unwritable instead.
/// </para>
/// </remarks>
public sealed class TheWayItWas(IReadOnlyList<Document> archive, ITrail trail)
{
    /// <summary>
    /// Download a report. The route that leaked.
    /// </summary>
    /// <param name="signedIn">Loaded from the session, and used for the log line.</param>
    /// <param name="wanted">Taken from the request body, and used for the query.</param>
    /// <returns>The bytes, whoever they belong to.</returns>
    /// <remarks>
    /// The log line names <paramref name="signedIn"/>. The query uses
    /// <paramref name="wanted"/>. Nothing joins them, and nothing ever said so.
    /// </remarks>
    public byte[]? PostDocument(PatientId signedIn, DocumentId wanted)
    {
        var document = archive.FirstOrDefault(one => one.Id == wanted);

        // The line the original wrote. It names the session, so it reads as
        // though the session were part of the request. It was not.
        trail.Record(new Answer(
            new Asked(signedIn, wanted),
            document,
            document is null ? Refusal.NotYours : Refusal.None,
            document is not null));

        return document?.Content;
    }

    /// <summary>
    /// Delete a document. The other route that leaked, and the worse one.
    /// </summary>
    /// <param name="signedIn">Loaded, logged, unused.</param>
    /// <param name="wanted">From the request body.</param>
    /// <returns>True if something was removed.</returns>
    public bool RequestRemove(PatientId signedIn, DocumentId wanted)
    {
        var document = archive.FirstOrDefault(one => one.Id == wanted);

        trail.Record(new Answer(
            new Asked(signedIn, wanted),
            document,
            document is null ? Refusal.NotYours : Refusal.None,
            document is not null));

        return document is not null;
    }

    /// <summary>
    /// Fetch the images. One of the two routes that did it correctly.
    /// </summary>
    /// <param name="signedIn">Loaded, logged, and put into the question.</param>
    /// <param name="wanted">From the request body, and only the accession.</param>
    /// <returns>The bytes, or nothing.</returns>
    /// <remarks>
    /// The difference from <see cref="PostDocument"/> is one term in one
    /// predicate. In the original it was the difference between constructing a
    /// parameter object with the token in it and deserialising the request body
    /// into the parameter object whole.
    /// </remarks>
    public byte[]? RequestImage(PatientId signedIn, DocumentId wanted)
    {
        var document = archive.FirstOrDefault(one => one.Id == wanted && one.Belongs == signedIn);

        trail.Record(new Answer(
            new Asked(signedIn, wanted),
            document,
            document is null ? Refusal.NotYours : Refusal.None,
            archive.Any(one => one.Id == wanted)));

        return document?.Content;
    }

    /// <summary>Fetch the images another way. Also correct.</summary>
    /// <param name="signedIn">In the question.</param>
    /// <param name="wanted">From the request body.</param>
    /// <returns>The bytes, or nothing.</returns>
    public byte[]? PostImage(PatientId signedIn, DocumentId wanted) =>
        RequestImage(signedIn, wanted);

    /// <summary>The four routes by name, so the measurement can walk them.</summary>
    /// <returns>Each route's name and a way to call it.</returns>
    public IReadOnlyList<(string Name, Func<PatientId, DocumentId, bool> Hands)> Routes =>
    [
        ("PostDocument", (who, what) => PostDocument(who, what) is not null),
        ("RequestRemove", RequestRemove),
        ("RequestImage", (who, what) => RequestImage(who, what) is not null),
        ("PostImage", (who, what) => PostImage(who, what) is not null),
    ];
}
