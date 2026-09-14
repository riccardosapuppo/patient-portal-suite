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
    public IReadOnlyList<(string Who, string What)> Choices { get; } =
        Ward.Patients
            .Select(who =>
            {
                var theirs = Ward.Everything().Where(one => one.Belongs == who).ToList();

                var parts = new List<string>();
                var readable = theirs.Count(one => one.Released && !one.Sensitive);
                var guarded = theirs.Count(one => one.Released && one.Sensitive);
                var waiting = theirs.Count(one => !one.Released);

                if (readable > 0) parts.Add($"{readable} to read");
                if (guarded > 0) parts.Add($"{guarded} needing a code");
                if (waiting > 0) parts.Add($"{waiting} not signed off");

                return (
                    who.Value,
                    parts.Count == 0 ? "nothing yet — the empty page" : string.Join(", ", parts));
            })
            .ToList();

    /// <summary>Show the form.</summary>
    public void OnGet()
    {
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
