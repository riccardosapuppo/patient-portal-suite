namespace Portal.Web.Pages;

using Microsoft.AspNetCore.Mvc.RazorPages;

/// <summary>What a patient sees when nothing else fits.</summary>
/// <remarks>
/// It says nothing about the request. An error page that repeats the path, the
/// query string, or the exception is a page that will one day repeat an
/// accession number to the wrong person.
/// </remarks>
public sealed class ErrorModel : PageModel
{
    /// <summary>What the patient is told.</summary>
    public string Said { get; private set; } = string.Empty;

    /// <summary>Draw it.</summary>
    /// <param name="code">The status code, from the re-execute.</param>
    public void OnGet(int? code) =>
        Said = code == 404
            ? "There is no such page."
            : "The portal could not do that. Nothing has been changed.";
}
