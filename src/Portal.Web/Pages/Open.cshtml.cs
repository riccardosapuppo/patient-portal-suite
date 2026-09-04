namespace Portal.Web.Pages;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using Portal.Core;

/// <summary>Handing over one document.</summary>
/// <remarks>
/// <para>
/// This is the route that leaked, rewritten. The whole of the rewrite is that
/// <see cref="Who.Asking"/> returns a patient and a document together, and the
/// archive takes nothing else — so there is no version of this method that
/// forwards the identifier from the query string on its own.
/// </para>
/// <para>
/// It is also the route that decides what a patient is told when the answer is
/// no, and it deliberately tells them very little. See
/// <see cref="Refusal.NotYours"/>.
/// </para>
/// </remarks>
public sealed class OpenModel(IDocuments documents, ITrail trail) : PageModel
{
    /// <summary>What the patient is told, when they are not given the document.</summary>
    public string Said { get; private set; } = string.Empty;

    /// <summary>The document a code would be for, when that is what is missing.</summary>
    public string? NeedsACodeFor { get; private set; }

    /// <summary>Open a document.</summary>
    /// <param name="id">The accession number, from the query string.</param>
    /// <param name="cancel">To give up.</param>
    /// <returns>The file, or a page saying no.</returns>
    public async Task<IActionResult> OnGetAsync(string? id, CancellationToken cancel)
    {
        if (Who.Asking(User, id) is not { } question) return RedirectToPage("/SignIn");

        var answer = await documents.Answer(question, cancel: cancel);

        // Recorded whatever happens, and recorded from the answer, which
        // carries the question the archive was actually given.
        trail.Record(answer);

        if (answer.WasGiven) return Handing.AsFile(answer.Given!);

        Said = answer.Why switch
        {
            Refusal.NotReleasedYet => "That report has not been released yet. A clinician has still to sign it off.",
            Refusal.NeedsASecondFactor => "That document needs a code sent to your phone before it will open.",
            _ => "There is no such document on your record.",
        };

        if (answer.Why == Refusal.NeedsASecondFactor) NeedsACodeFor = question.For.Value;

        return Page();
    }
}
