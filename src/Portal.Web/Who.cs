namespace Portal.Web;

using System.Security.Claims;
using System.Security.Cryptography;

using Portal.Core;

/// <summary>
/// Turning a signed-in browser and a query string into a question.
/// </summary>
/// <remarks>
/// <para>
/// One place, used by every page, and it returns an <see cref="Asked"/> — which
/// is to say it returns a patient and a document together or it returns
/// nothing. There is no arrangement of these three lines that produces a
/// document identifier a page could use on its own.
/// </para>
/// <para>
/// That is the point at which the original went wrong four times over. Each
/// route did this by hand: read the claim, load the user, and then — in two of
/// the four — deserialise the request body into the parameter object and send
/// that. The patient was in scope the whole time.
/// </para>
/// </remarks>
public static class Who
{
    /// <summary>The patient this browser is signed in as, if any.</summary>
    /// <param name="user">The principal from the cookie.</param>
    /// <returns>The patient, or null.</returns>
    public static PatientId? SignedIn(ClaimsPrincipal? user)
    {
        var claim = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return string.IsNullOrWhiteSpace(claim) ? null : new PatientId(claim);
    }

    /// <summary>The question this request is asking.</summary>
    /// <param name="user">The principal from the cookie.</param>
    /// <param name="accession">The document identifier from the request.</param>
    /// <returns>The question, or null when either half is missing.</returns>
    public static Asked? Asking(ClaimsPrincipal? user, string? accession)
    {
        var patient = SignedIn(user);

        if (patient is null || string.IsNullOrWhiteSpace(accession)) return null;

        return new Asked(patient.Value, new DocumentId(accession));
    }
}

/// <summary>
/// Six digits, from the operating system's own source.
/// </summary>
/// <remarks>
/// <see cref="RandomNumberGenerator"/> rather than <see cref="Random"/>. The
/// second is seeded from the clock and predictable from any other output of it,
/// and this number is the only thing between a browser and a set of blood
/// results.
/// </remarks>
public static class SixDigits
{
    /// <summary>Mint one.</summary>
    /// <returns>Six digits, left-padded.</returns>
    public static string Next() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}

/// <summary>
/// Where the codes go in a repository that has no telephone.
/// </summary>
/// <remarks>
/// Onto the screen, with the page saying so in as many words. The alternative —
/// a fake gateway that logs and pretends — is the kind of thing somebody points
/// at a real number eighteen months later.
/// </remarks>
public sealed class CodesOnTheScreen : ISendCodes
{
    private readonly Dictionary<PatientId, string> shown = [];

    /// <inheritdoc />
    public void Send(PatientId who, string code) => shown[who] = code;

    /// <summary>The code this patient would have been sent.</summary>
    /// <param name="who">The patient.</param>
    /// <returns>The digits, or null.</returns>
    public string? LastFor(PatientId who) => shown.GetValueOrDefault(who);
}

/// <summary>Turning a document into a download.</summary>
/// <remarks>
/// One place, so that the two pages that hand a document over hand it over the
/// same way. The bytes come from the archive's answer and from nowhere else:
/// there is no path here that fetches content after a refusal.
/// </remarks>
public static class Handing
{
    /// <summary>Send the document to the browser.</summary>
    /// <param name="document">What the archive handed back.</param>
    /// <returns>The file.</returns>
    public static Microsoft.AspNetCore.Mvc.FileContentResult AsFile(Document document) =>
        new(document.Content, "text/plain; charset=utf-8")
        {
            FileDownloadName = $"{document.Id}.txt",
        };
}
