namespace Portal.Core;

/// <summary>
/// Everything the portal is allowed to ask the archive.
/// </summary>
/// <remarks>
/// <para>
/// Read the shape of it rather than the methods. There is no
/// <c>Find(DocumentId)</c>, no <c>Get(string accession)</c>, and no overload
/// that takes an identifier on its own — every question names the patient
/// asking it, because <see cref="Asked"/> cannot be built without one and
/// <see cref="ListFor"/> takes nothing else.
/// </para>
/// <para>
/// That is the difference between this and the code it was rebuilt from. There,
/// the archive could be asked for a document by identifier, and four routes
/// decided separately whether to bind that to the signed-in patient. Two did.
/// The other two loaded the patient, wrote them into the log line, and sent the
/// request body straight through.
/// </para>
/// </remarks>
public interface IDocuments
{
    /// <summary>Everything this patient may see on their own list.</summary>
    /// <param name="who">The patient, from the session.</param>
    /// <param name="cancel">To give up.</param>
    /// <returns>Their documents, released and not.</returns>
    Task<IReadOnlyList<Document>> ListFor(PatientId who, CancellationToken cancel = default);

    /// <summary>Answer one question about one document.</summary>
    /// <param name="question">Who is asking, and for what.</param>
    /// <param name="code">
    /// A receipt from <see cref="SecondFactor"/>, when the caller has one. The
    /// archive checks that it is a receipt for <em>this</em> question; a
    /// receipt for another document is the same as none.
    /// </param>
    /// <param name="cancel">To give up.</param>
    /// <returns>The document, or why not, and the audit line either way.</returns>
    Task<Answer> Answer(Asked question, Confirmed? code = null, CancellationToken cancel = default);
}

/// <summary>Where the audit trail goes.</summary>
/// <remarks>
/// Separate from a logger on purpose. Application logs get turned down, sampled
/// and rotated; this is the record of who was handed which document, which is
/// the thing a hospital has to be able to produce two years later.
/// </remarks>
public interface ITrail
{
    /// <summary>Record one answer.</summary>
    /// <param name="answer">The answer, which carries its own question.</param>
    void Record(Answer answer);

    /// <summary>Everything recorded so far, oldest first.</summary>
    IReadOnlyList<string> Lines { get; }
}

/// <summary>An audit trail held in memory, for the measurement and the tests.</summary>
public sealed class TrailInMemory : ITrail
{
    private readonly List<string> lines = [];

    /// <inheritdoc />
    public IReadOnlyList<string> Lines => lines;

    /// <inheritdoc />
    public void Record(Answer answer) => lines.Add(answer.Line);
}
