namespace Portal.Web;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

/// <summary>
/// An antiforgery token that has gone stale sends somebody back to the form,
/// not to the browser's error page.
/// </summary>
/// <remarks>
/// <para>
/// The token is tied to the identity it was drawn for, which is a good thing
/// and is exactly what stops one person's token being replayed as another's.
/// But on the sign-in page the identity is the thing being changed, and there
/// is a perfectly ordinary way to fall foul of it: leave the portal open in two
/// tabs, sign in as somebody on one, then press a patient on the other. The
/// second form was drawn for nobody, the request arrives as somebody, and the
/// token is refused.
/// </para>
/// <para>
/// What happened next was the worst part. The refusal is a bare 400 with no
/// body, so the browser drew its own error page — "this page is not working",
/// in the language of the machine, with no way back and nothing naming the
/// portal. The check was right; the answer was useless. A refusal a person
/// cannot act on is a fault whatever its status code, and this repository has
/// already said that about a refusal one floor up.
/// </para>
/// <para>
/// So the refusal is turned into the sign-in page with a sentence on it. Note
/// what is not done: the token is not waived, the check is not softened, and no
/// page opts out of it. The request is still refused. It is refused somewhere
/// a person can see it.
/// </para>
/// <para>
/// <see cref="IAlwaysRunResultFilter"/> and not an ordinary result filter,
/// because the antiforgery check is an authorization filter and short-circuits
/// before anything else runs. "Always run" is the only kind that sees what a
/// short circuit produced.
/// </para>
/// </remarks>
public sealed class WhenTheFormWentStale : IAlwaysRunResultFilter
{
    /// <summary>The query the sign-in page reads to know it should explain itself.</summary>
    public const string Stale = "stale";

    /// <inheritdoc />
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not AntiforgeryValidationFailedResult) return;

        // Back to the form itself rather than to the page that was posted to.
        // The two are the same here, and saying so explicitly means this keeps
        // working if a second form is ever added somewhere else.
        context.Result = new RedirectToPageResult("/SignIn", new Dictionary<string, string?>
        {
            [Stale] = "1",
        });
    }

    /// <inheritdoc />
    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
