namespace Portal.Web.Pages;

using System.Security.Claims;
using System.Text;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using Portal.Core;
using Portal.Store;

/// <summary>Signing in, and out.</summary>
/// <remarks>
/// The authentication here is a stub and says so on the page: the ward is
/// invented, so there is nobody to authenticate. What the rest of the
/// repository is about is the other half — what a signed-in patient may then be
/// handed — and that half is not a stub.
/// </remarks>
public sealed class SignInModel : PageModel
{
    /// <summary>The password for every invented patient.</summary>
    /// <remarks>
    /// A constant, in the open, on a ward that does not exist. Anything else
    /// here would be theatre: a hash of a published password is still a
    /// published password, and pretending otherwise is how a reader comes away
    /// thinking they have seen a credential store.
    /// </remarks>
    public const string ThePassword = "ward";

    /// <summary>Which patient the form has chosen.</summary>
    [BindProperty]
    public string Patient { get; set; } = string.Empty;

    /// <summary>What went wrong, if anything.</summary>
    public string? Said { get; private set; }

    /// <summary>Everyone on the invented ward.</summary>
    public IReadOnlyList<PatientId> Everyone => Ward.Patients;

    /// <summary>Each invented patient, and what signing in as them shows.</summary>
    /// <remarks>
    /// <para>
    /// The page used to be a select, a password box, and the password printed
    /// in a line underneath. Somebody opening this for the first time submitted
    /// it empty, got told it was not a patient and a password we recognise, and
    /// asked what the credentials were -- with the answer three inches below
    /// the button they had just pressed. A thing needed in order to get in
    /// cannot live under the form.
    /// </para>
    /// <para>
    /// So each patient is a button that signs you in, and each says what that
    /// patient is here to show: one has a document that will ask for a code,
    /// one has nothing at all. Choosing becomes choosing what to look at rather
    /// than picking a name off a list.
    /// </para>
    /// <para>
    /// Counted from the invented ward rather than asked of the archive. This
    /// page runs before anybody is signed in, and a sign-in page that queries
    /// the record of every patient in order to describe them is the shape of
    /// thing this repository exists to argue against -- even where the data is
    /// invented and the page says so.
    /// </para>
    /// </remarks>
    public IReadOnlyList<Choice> Choices { get; } =
        Ward.Patients
            .Select(who =>
            {
                var theirs = Ward.Everything().Where(one => one.Belongs == who).ToList();

                return new Choice(
                    who.Value,
                    theirs.Count(one => one.Released && !one.Sensitive),
                    theirs.Count(one => one.Released && one.Sensitive),
                    theirs.Count(one => !one.Released));
            })
            .ToList();

    /// <summary>One invented patient, and what signing in as them shows.</summary>
    /// <remarks>
    /// Three counts rather than a sentence, so the page can colour them the way
    /// the documents themselves are coloured: the same green, amber and slate
    /// on both screens, so that picking the patient with the amber one is
    /// picking the thing you are about to see.
    /// </remarks>
    /// <param name="Who">The patient.</param>
    /// <param name="Readable">Released, and openable without anything further.</param>
    /// <param name="Guarded">Released, and asks for a code first.</param>
    /// <param name="Waiting">Not released: nobody has signed it off.</param>
    public sealed record Choice(string Who, int Readable, int Guarded, int Waiting)
    {
        /// <summary>Whether this patient has nothing at all.</summary>
        public bool Empty => Readable + Guarded + Waiting == 0;
    }

    /// <summary>Show the form.</summary>
    /// <param name="stale">
    /// Set when the person was sent here by <see cref="WhenTheFormWentStale"/>,
    /// having pressed a button on a page drawn before somebody signed in on
    /// this browser.
    /// </param>
    public void OnGet(string? stale)
    {
        if (string.IsNullOrEmpty(stale)) return;

        // Not "an error occurred". It says what happened, that the page in
        // front of them is the fresh one, and what to do -- which is the
        // difference between a refusal and a dead end.
        Said = "Somebody signed in on this browser while that page was open, so it "
            + "would not submit. This one is fresh: pick a patient again.";
    }

    /// <summary>Take the form.</summary>
    /// <param name="password">What was typed.</param>
    /// <returns>The list, or the form again.</returns>
    public async Task<IActionResult> OnPostAsync(string? password)
    {
        var known = Ward.Patients.FirstOrDefault(one => one.Value == Patient);

        // One message for both halves. "No such patient" and "wrong password"
        // as separate answers is a way of asking the portal who is registered
        // with the hospital, one guess at a time — the same mistake as telling
        // a stranger whether a document exists, one floor up.
        if (known.Value is null || !Same(password ?? string.Empty, ThePassword))
        {
            Said = "That is not a patient and a password we recognise.";
            return Page();
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, known.Value)],
            CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return RedirectToPage("/Index");
    }

    /// <summary>Sign out.</summary>
    /// <returns>The sign-in page.</returns>
    public async Task<IActionResult> OnPostOutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/SignIn");
    }

    private static bool Same(string typed, string real)
    {
        var a = Encoding.UTF8.GetBytes(typed);
        var b = Encoding.UTF8.GetBytes(real);

        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Security.Cryptography.SHA256.HashData(a),
            System.Security.Cryptography.SHA256.HashData(b));
    }
}
