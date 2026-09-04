namespace Portal.Web.Pages;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using Portal.Core;

/// <summary>Asking for a code, and using it.</summary>
/// <remarks>
/// <para>
/// Both halves go through the same <see cref="Asked"/>. A code is sent only for
/// a document the archive has already said this patient would be handed but for
/// the second factor, and it is checked against that same pair — so the
/// accession in the form is not something the caller gets to choose freely, it
/// is something that has to match what the code was minted against.
/// </para>
/// <para>
/// In the original, both halves took the accession from the request body and
/// neither put it in the same question as the patient. A patient could be sent
/// a code, to their own phone, for somebody else's study.
/// </para>
/// </remarks>
public sealed class CodeModel(IDocuments documents, ITrail trail, SecondFactor codes, ISendCodes phones) : PageModel
{
    /// <summary>The document a code was asked for.</summary>
    public string Accession { get; private set; } = string.Empty;

    /// <summary>Whether a code went out.</summary>
    public bool Sent { get; private set; }

    /// <summary>What the patient is told.</summary>
    public string Said { get; private set; } = string.Empty;

    /// <summary>The code, because this repository has no telephone.</summary>
    public string? OnTheScreen { get; private set; }

    /// <summary>Send a code.</summary>
    /// <param name="id">The accession number.</param>
    /// <param name="cancel">To give up.</param>
    /// <returns>The page.</returns>
    public async Task<IActionResult> OnGetAsync(string? id, CancellationToken cancel)
    {
        if (Who.Asking(User, id) is not { } question) return RedirectToPage("/SignIn");

        Accession = question.For.Value;
        Sent = await codes.SendACode(question, DateTimeOffset.UtcNow, cancel);

        if (!Sent)
        {
            // The same words as every other refusal. Whether the document does
            // not exist, belongs to somebody else, or does not need a code at
            // all, the patient learns nothing from asking.
            Said = "There is no such document on your record.";
            return Page();
        }

        OnTheScreen = (phones as CodesOnTheScreen)?.LastFor(question.By);

        return Page();
    }

    /// <summary>Take the six digits.</summary>
    /// <param name="id">The accession number, from the form.</param>
    /// <param name="code">The six digits.</param>
    /// <param name="cancel">To give up.</param>
    /// <returns>The file, or the form again.</returns>
    public async Task<IActionResult> OnPostAsync(string? id, string? code, CancellationToken cancel)
    {
        if (Who.Asking(User, id) is not { } question) return RedirectToPage("/SignIn");

        Accession = question.For.Value;
        Sent = true;

        // Confirm hands back a receipt, and a receipt is the only thing that
        // can tell the archive the second factor is satisfied. This page cannot
        // make one: the constructor is internal to Portal.Core.
        var receipt = codes.Confirm(question, code ?? string.Empty, DateTimeOffset.UtcNow);

        var answer = await documents.Answer(question, receipt, cancel);
        trail.Record(answer);

        if (answer.WasGiven) return Handing.AsFile(answer.Given!);

        Said = "That code is not right, or it has run out. Ask for another.";
        OnTheScreen = (phones as CodesOnTheScreen)?.LastFor(question.By);

        return Page();
    }
}
