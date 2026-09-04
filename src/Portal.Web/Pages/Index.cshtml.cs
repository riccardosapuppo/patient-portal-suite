namespace Portal.Web.Pages;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using Portal.Core;

/// <summary>The patient's own list.</summary>
/// <remarks>
/// The list is drawn from <see cref="IDocuments.ListFor"/>, which takes a
/// patient and nothing else, so there is no filtering to get wrong here. The
/// page cannot show a row it should not, because it was never handed one.
/// </remarks>
public sealed class IndexModel(IDocuments documents) : PageModel
{
    /// <summary>What to draw.</summary>
    public IReadOnlyList<Document> Documents { get; private set; } = [];

    /// <summary>Fetch the list.</summary>
    /// <param name="cancel">To give up.</param>
    /// <returns>The page.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancel)
    {
        if (Who.SignedIn(User) is not { } patient) return RedirectToPage("/SignIn");

        Documents = await documents.ListFor(patient, cancel);

        return Page();
    }
}
