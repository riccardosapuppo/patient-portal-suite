namespace Portal.Core;

/// <summary>One document, as the portal knows it.</summary>
/// <param name="Id">The accession number.</param>
/// <param name="Belongs">Whose it is. Never inferred, always stored.</param>
/// <param name="Title">What it is called on the list.</param>
/// <param name="Released">Whether a clinician has released it to the patient.</param>
/// <param name="Sensitive">Whether reading it needs a code sent to their phone.</param>
/// <param name="Content">The bytes, which in this repository are invented.</param>
public sealed record Document(
    DocumentId Id,
    PatientId Belongs,
    string Title,
    bool Released,
    bool Sensitive,
    byte[] Content);

/// <summary>
/// What came back, and the line that goes in the audit trail — the same object,
/// on purpose.
/// </summary>
/// <remarks>
/// <para>
/// <b>The audit line is a by-product of the answer, not a description of it.</b>
/// </para>
/// <para>
/// In the original, the log line and the outgoing request were built from two
/// different variables. The line said
/// <c>Call Api api/Document (Request[{user.Token}])</c> while the request that
/// went out carried the document identifier from the request body and no token
/// at all. The log was not wrong by accident and it was not a typo: it
/// described what the author believed the code did. An audit trail that
/// describes an intention is worse than none, because somebody will read it
/// afterwards and be reassured.
/// </para>
/// <para>
/// Here there is nothing to keep in step. <see cref="Question"/> is the value
/// the store was handed, and <see cref="Line"/> is computed from it, so the log
/// cannot name an identity the query did not use.
/// </para>
/// </remarks>
/// <param name="Question">The question, exactly as the store received it.</param>
/// <param name="Given">The document, when it was handed over.</param>
/// <param name="Why">Why not, when it was not.</param>
/// <param name="Existed">
/// Whether the document exists at all. For the audit trail only — see
/// <see cref="Refusal.NotYours"/> for why the patient is not told.
/// </param>
public sealed record Answer(Asked Question, Document? Given, Refusal Why, bool Existed)
{
    /// <summary>The document was handed over.</summary>
    /// <param name="question">The question that was asked.</param>
    /// <param name="document">What was handed over.</param>
    /// <returns>The answer.</returns>
    public static Answer Handed(Asked question, Document document) =>
        new(question, document, Refusal.None, true);

    /// <summary>Nothing was handed over.</summary>
    /// <param name="question">The question that was asked.</param>
    /// <param name="why">The reason, as the patient will see it.</param>
    /// <param name="existed">Whether the document exists, for the audit trail.</param>
    /// <returns>The answer.</returns>
    public static Answer No(Asked question, Refusal why, bool existed) =>
        new(question, null, why, existed);

    /// <summary>Whether anything was handed over.</summary>
    public bool WasGiven => Given is not null;

    /// <summary>
    /// The line for the audit trail, built from the question that was asked.
    /// </summary>
    /// <remarks>
    /// It records what the patient was not told: whether the document existed.
    /// That is the point of keeping the two apart — the person asking learns
    /// nothing, and the person reading the trail afterwards learns everything.
    /// </remarks>
    public string Line =>
        WasGiven
            ? $"{Question.By} was handed {Question.For}"
            : $"{Question.By} was refused {Question.For}: {Why}"
              + (Why == Refusal.NotYours ? $" (it {(Existed ? "exists and is somebody else's" : "does not exist")})" : string.Empty);
}
