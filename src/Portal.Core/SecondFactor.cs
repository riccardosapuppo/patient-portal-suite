namespace Portal.Core;

/// <summary>
/// A code sent to a phone, and what it is a code <em>for</em>.
/// </summary>
/// <remarks>
/// The binding is the whole of it. A code is not a password and it is not a
/// session: it is permission to open <em>one document</em>, held by <em>one
/// patient</em>, and both halves have to survive the round trip to the phone
/// and back.
/// </remarks>
/// <param name="For">Who it was minted for, and what for.</param>
/// <param name="Code">The six digits.</param>
/// <param name="Until">When it stops working.</param>
public sealed record Challenge(Asked For, string Code, DateTimeOffset Until);

/// <summary>
/// Proof that a code was checked, and what it was checked for.
/// </summary>
/// <remarks>
/// <para>
/// The constructor is internal, so the only thing in the world that can make
/// one of these is <see cref="SecondFactor.Confirm"/>. A page cannot decide on
/// its own that the second factor is satisfied; it can only hold something that
/// was handed to it, and that something says which question it is about.
/// </para>
/// <para>
/// It is the same move as <see cref="Asked"/>, for the same reason. The archive
/// does not trust the page to have checked; it looks at what the page is
/// carrying and at whether it is for this question.
/// </para>
/// </remarks>
public readonly record struct Confirmed
{
    internal Confirmed(Asked forWhat) => For = forWhat;

    /// <summary>The question this receipt is good for, and no other.</summary>
    public Asked For { get; }
}

/// <summary>Somewhere to send six digits.</summary>
public interface ISendCodes
{
    /// <summary>Send it.</summary>
    /// <param name="who">The patient it goes to.</param>
    /// <param name="code">The six digits.</param>
    void Send(PatientId who, string code);
}

/// <summary>
/// Minting and checking the code that opens a sensitive document.
/// </summary>
/// <remarks>
/// <para>
/// Two things went wrong here in the original, and they are both about what the
/// code is attached to.
/// </para>
/// <para>
/// <b>The caller chose what the code was for.</b> The route read the accession
/// out of the request body and asked for a code against it, without ever
/// putting that accession and the signed-in patient into the same question. So
/// a patient could ask for a code against somebody else's study, and be sent
/// one, to their own phone.
/// </para>
/// <para>
/// <b>The code was looked up by itself.</b> Confirming meant finding a live
/// challenge with those six digits — not those six digits <em>for this patient
/// and this document</em>. Six digits is a million, a code lasts ten minutes,
/// and a busy portal has hundreds live at once; that is not a brute-force
/// story, it is that the code minted for one document opened another.
/// </para>
/// <para>
/// Both are gone by construction rather than by check: a challenge is minted
/// only after <see cref="IDocuments"/> has said the patient would be given the
/// document but for the second factor, and it is looked up by
/// <see cref="Asked"/> — the same pair — not by its digits.
/// </para>
/// </remarks>
public sealed class SecondFactor(IDocuments documents, ISendCodes phones, Func<string> digits)
{
    private readonly Dictionary<Asked, Challenge> live = [];

    /// <summary>How long a code lasts.</summary>
    public static TimeSpan Lasts => TimeSpan.FromMinutes(10);

    /// <summary>
    /// Send a code, but only for a document this patient would otherwise be
    /// handed.
    /// </summary>
    /// <param name="question">Who is asking, and for what.</param>
    /// <param name="now">The clock.</param>
    /// <param name="cancel">To give up.</param>
    /// <returns>
    /// True when a code went out. False when the answer to the underlying
    /// question was anything other than "yes, but for the code" — and the
    /// caller is told nothing more, for the same reason
    /// <see cref="Refusal.NotYours"/> is one value.
    /// </returns>
    public async Task<bool> SendACode(Asked question, DateTimeOffset now, CancellationToken cancel = default)
    {
        var answer = await documents.Answer(question, cancel: cancel);

        // The question is asked before the code is minted, and it is the same
        // question the download will ask. Nothing here trusts that the caller
        // was entitled to name this document.
        if (answer.Why != Refusal.NeedsASecondFactor) return false;

        var challenge = new Challenge(question, digits(), now + Lasts);
        live[question] = challenge;
        phones.Send(question.By, challenge.Code);

        return true;
    }

    /// <summary>Check a code against the pair it was minted for.</summary>
    /// <param name="question">Who is asking, and for what.</param>
    /// <param name="code">The six digits they typed.</param>
    /// <param name="now">The clock.</param>
    /// <returns>
    /// A receipt for this question, or null. The receipt is the only way the
    /// archive can be told the second factor is satisfied.
    /// </returns>
    public Confirmed? Confirm(Asked question, string code, DateTimeOffset now)
    {
        if (!live.TryGetValue(question, out var challenge)) return null;
        if (now >= challenge.Until) return null;
        if (!Same(challenge.Code, code)) return null;

        // Used once. A code that survives its own use is a code somebody can
        // replay out of a browser history.
        live.Remove(question);

        return new Confirmed(question);
    }

    /// <summary>
    /// Find a live code by its digits alone, the way the original did.
    /// </summary>
    /// <param name="code">The six digits.</param>
    /// <param name="now">The clock.</param>
    /// <returns>Whatever challenge happens to carry those digits.</returns>
    /// <remarks>
    /// Kept runnable so the measurement can count what it opens. It is not
    /// called by anything the portal serves.
    /// </remarks>
    public Challenge? TheWayItWasLookedUp(string code, DateTimeOffset now) =>
        live.Values.FirstOrDefault(one => now < one.Until && Same(one.Code, code));

    /// <summary>How many codes are live.</summary>
    public int Live => live.Count;

    /// <summary>
    /// Compare two codes without giving away where they stopped matching.
    /// </summary>
    /// <remarks>
    /// Length is compared first and separately: it is public in the sense that
    /// the format is published, and folding it into the loop would be the one
    /// early return that matters.
    /// </remarks>
    private static bool Same(string a, string b)
    {
        if (a.Length != b.Length) return false;

        var different = 0;
        for (var i = 0; i < a.Length; i++) different |= a[i] ^ b[i];

        return different == 0;
    }
}
