namespace Portal.Core;

/// <summary>Which patient. Not a string, so it cannot be a document by mistake.</summary>
/// <param name="Value">The identifier the portal knows them by.</param>
public readonly record struct PatientId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Which document.</summary>
/// <param name="Value">The accession number the archive knows it by.</param>
public readonly record struct DocumentId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>
/// A question the store can answer, and there is no way to write one that does
/// not say who is asking.
/// </summary>
/// <remarks>
/// <para>
/// <b>This type is the whole design.</b> Every leak the original had was the
/// same shape: the signed-in patient was loaded, was even written into the log
/// line, and was then not part of the question put to the archive. The
/// identifier that reached the archive came from the request body, so whoever
/// sent the request chose it.
/// </para>
/// <para>
/// The fix is not a check. A check is a thing somebody can forget to write on
/// the next route, and the original had four routes of which two remembered.
/// The fix is that the store has no method that takes a
/// <see cref="DocumentId"/> on its own — the only thing you can hand it is one
/// of these, and one of these cannot be built without a
/// <see cref="PatientId"/>. The dangerous call is not guarded; it is
/// unwritable.
/// </para>
/// </remarks>
/// <param name="By">The patient who is signed in, taken from the session.</param>
/// <param name="For">The document they say they want, taken from the request.</param>
public readonly record struct Asked(PatientId By, DocumentId For);

/// <summary>Why a document was not handed over.</summary>
public enum Refusal
{
    /// <summary>It was handed over.</summary>
    None,

    /// <summary>
    /// There is no such document, or there is and it belongs to somebody else.
    /// </summary>
    /// <remarks>
    /// <b>Deliberately one value and not two.</b> A portal that answers "not
    /// yours" for a document that exists and "not found" for one that does not
    /// has told a stranger which accession numbers are real. The two cases are
    /// told apart in the audit line, which the patient never sees, and are the
    /// same answer to the person asking.
    /// </remarks>
    NotYours,

    /// <summary>
    /// It is theirs, and the clinician has not released it yet.
    /// </summary>
    /// <remarks>
    /// Safe to distinguish: the patient already knows the examination happened,
    /// because they were there. What this must not do is hand over a draft.
    /// </remarks>
    NotReleasedYet,

    /// <summary>It is theirs, released, and needs a code sent to their phone.</summary>
    NeedsASecondFactor,
}
