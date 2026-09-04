# Patient Portal Suite

A hospital puts its patients' reports on the web. From then on, one thing
matters more than everything else in the system put together: **nobody is ever
handed somebody else's.**

This is a rebuild of a portal that got that wrong, and the interesting part is
*how* it got it wrong. Not by forgetting about the patient — the patient is
loaded on every route, and named in every log line. Four routes read the
signed-in patient out of the session, wrote them into the audit trail, and then
two of them sent the accession number from the request body straight to the
archive with nothing binding the two together. Reading the four methods side by
side will not tell you which two.

```csharp
// This one is fine.
new DocumentImageParam { Accession = fromTheRequest.Accession, Token = user.Token }

// This one is not, and it is nine lines away.
JsonConvert.DeserializeObject<DocumentItem>(param)
```

The log line above the second one said `Request[{user.Token}]`. The request
carried no token at all.

It comes with five claims, and each of them can fail:

| | |
| --- | --- |
| **No route hands a patient a document that is not theirs.** | Over 90 questions: the two leaking routes hand over **70** documents belonging to somebody else. This portal hands over **0**. |
| **The audit trail names the identity the archive was actually asked with.** | **70** of the old route's 90 lines claim a handover to somebody who does not own the document. The portal's: **0**. |
| **A refusal does not tell a stranger whether the document exists.** | 12 accession numbers tried by somebody who owns none of them, **11** of them real, and **1** answer they can tell apart. |
| **A code opens the one document it was sent for.** | Sent only for documents the archive would otherwise hand over; checked against the same patient and document; used once. |
| **A session expires at the same moment whichever clock reads it.** | The old seventeen-digit stamp is still valid on **12** of the 27 offsets a reader could be sitting on. |

`dotnet run --project src/Portal.Measure` prints those and exits non-zero if any
of them stops being true. So does CI.

---

## The one idea

Every leak in the original was the same shape, so the repair is not four fixes.
It is one type:

```csharp
public readonly record struct Asked(PatientId By, DocumentId For);
```

and an archive with no method that takes anything else:

```csharp
public interface IDocuments
{
    Task<IReadOnlyList<Document>> ListFor(PatientId who, CancellationToken cancel = default);
    Task<Answer> Answer(Asked question, Confirmed? code = null, CancellationToken cancel = default);
}
```

There is no `Find(DocumentId)`. There is no overload that takes an accession
number on its own. A route cannot forget to bind the patient to the document,
because it has nothing to hand the archive except the two of them together.

That distinction matters more than it looks. **A check is a line that can be
absent, and this repository is a demonstration that an absent line is invisible**
— it was absent in two of four nearly identical methods and nobody saw it for
years. A type is not absent. The wrong version does not fail review; it fails to
compile.

A test says so out loud, by reflection, so that adding the dangerous method
fails before any route has had the chance to call it:

```csharp
foreach (var method in typeof(IDocuments).GetMethods())
{
    var takes = method.GetParameters().Select(one => one.ParameterType).ToList();

    Assert.True(
        takes.Contains(typeof(PatientId)) || takes.Contains(typeof(Asked)),
        $"IDocuments.{method.Name} can be called without naming a patient");
}
```

---

## Before you start

**The .NET 9 SDK**, and nothing else.

```
dotnet --version        # 9.0.317 here; any 9.0.x will do
```

```
dotnet test src/Portal.Tests            # 33 checks
dotnet run  --project src/Portal.Measure    # the five claims
dotnet run  --project src/Portal.Web        # the portal, on http://localhost:5000
```

All three run anywhere the SDK does, Linux included, which is why CI is one job.
Sign in as any of the invented patients; the password is `ward` and the page says
so.

---

## What is in here

| | |
| --- | --- |
| `Portal.Core` | The deciding. Identities, an answer, an audit line, the second factor, and the four routes as they were — kept runnable so the difference can be counted. |
| `Portal.Store` | The archive, in SQLite, where the binding is visible in a `WHERE` clause. And the invented ward. |
| `Portal.Measure` | Runs the ward through both and prints the difference. Exits non-zero when a claim stops holding. |
| `Portal.Web` | ASP.NET Core, Razor Pages, a cookie. |
| `Portal.Tests` | 33 checks: the archive asked directly, the portal driven over HTTP, and three that are about the shape of the code rather than what it does. |

### The ward is invented, and that is not a detail

A patient portal is the one place where a realistic fixture is a disclosure: a
name, a date of birth and a report title together are a person, and a repository
is forever. Nothing here comes from anywhere. Six given names, fourteen
documents of placeholder text, sequential accession numbers, and one accession
number that is not in the archive at all — which the third claim depends on
entirely.

One of the six patients has no documents, because a portal that only ever runs
against patients who have some has never drawn its own empty page.

---

## Four things that were wrong, and what replaced each

### The identity was loaded, logged, and not used

Described above. Replaced by `Asked`.

The audit trail is now a by-product of the answer rather than a description of
it: `Answer` carries the `Asked` the archive was handed, and the line is computed
from that, so there are not two things to keep in step. **An audit trail that
describes an intention is worse than none**, because somebody reads it afterwards
and is reassured.

### The login route had no `[Authorize]`

It read the signed-in patient out of the session anyway, and threw a
`NullReferenceException` when there was not one — which was caught, and returned
as a generic error. The route was protected by an accident, and would have
stopped being protected the day somebody tidied up the exception.

Turned round:

```csharp
options.Conventions.AuthorizeFolder("/");
options.Conventions.AllowAnonymousToPage("/SignIn");
options.Conventions.AllowAnonymousToPage("/Error");
```

Forgetting is now safe. A new page is signed-in-only until somebody writes its
name in that list, and writing a name there is a thing a reviewer sees in a diff.
A test reads the list of endpoints out of the *running application* and checks it
against those two names, so a page added tomorrow is covered tomorrow.

### The second factor was attached to nothing

The route read the accession from the request body and asked for a code against
it, so a patient could be sent a code — to their own phone — for somebody else's
study. And confirming meant finding a live challenge with those six digits,
rather than those six digits for this patient and this document.

Now a code is minted only after the archive has said this patient would be handed
this document but for the second factor, and confirming returns a **receipt**
whose constructor is internal to `Portal.Core`. A page cannot decide that the
second factor was satisfied; it can only hold something `SecondFactor.Confirm`
handed it, and that something names the question it is good for. The archive
checks that it is the same question.

### The expiry was seventeen digits with no offset

`yyyyMMddHHmmssFFF`, written by one process and compared against `DateTime.Now`
in another. When the two disagreed about the offset, the session outlived its own
expiry by exactly the difference. Nothing failed. Nothing was logged. The number
in the row was the number that had been written.

---

## The refusal that says nothing

The natural repair for a leak is to look the document up, find it belongs to
somebody else, and answer 403 — while answering 404 when there is nothing there.
That is still wrong, and it is wrong in a way no amount of testing the happy path
finds.

Somebody walking the accession space would separate the real numbers from the
invented ones without being handed a single byte, and on a real archive the
numbers are sequential, so that is the patient list. So `NotYours` is one value
covering both cases, the two are told apart only in the audit trail, and a test
compares the two responses **byte for byte**:

```csharp
Assert.Equal(
    await real.Content.ReadAsStringAsync(),
    await invented.Content.ReadAsStringAsync());
```

The sign-in page does the same thing with "no such patient" and "wrong password",
for the same reason.

---

## The measurement, in full

Printed by `dotnet run --project src/Portal.Measure`, and checked against this
file by CI, so it cannot quietly stop being what the program says.

```

An invented ward: 6 patients, 14 documents, and one accession number (ACC-000000) that is not in the archive.

  giulia     3 documents: 1 released, 1 draft, 1 sensitive
  marco      3 documents: 1 released, 1 draft, 1 sensitive
  elena      2 documents: 2 released, 0 draft, 0 sensitive
  davide     2 documents: 2 released, 0 draft, 0 sensitive
  sara       2 documents: 2 released, 0 draft, 0 sensitive
  paolo      2 documents: 2 released, 0 draft, 0 sensitive

==============================================================================
HOLDS   No route hands a patient a document that is not theirs
==============================================================================

  route             questions  handed over  not theirs
  ----------------------------------------------------
  PostDocument             90           84          70
  RequestRemove            90           84          70
  RequestImage             90           14           0
  PostImage                90           14           0
  the portal               90           10           0

    Two of the four routes bind the patient to the document and two do not, and
    reading the four methods will not tell you which. All four load the patient
    from the session; all four name the patient in the log line. The difference is
    one term in one predicate, and its absence looks exactly like nothing.

    The portal hands over 10 of 90: the released documents that are the
    asker's own. Drafts and sensitive ones are refused here and dealt with lower
    down. It cannot leak, because the question it is asked cannot be written
    without the patient in it.

==============================================================================
HOLDS   The audit trail names the identity the archive was actually asked with
==============================================================================

    lines written by the old route    90
    of which claim a handover to somebody who does not own it    70

    lines written by the portal       90
    of which claim the same            0

    The original wrote its log line from the session and its request from the
    request body. The line read

        Call Api api/Document (Request[<token>], Response[LEN : 41284])

    on a call that carried no token at all. That is worse than no trail: somebody
    reads it afterwards and is reassured.

    Here the line is computed from the question the archive was handed, so there
    are not two things to keep in step. A trail entry naming a patient is an entry
    for a query that named that patient, because it is the same value.

    A refusal is one answer to the patient and two lines in the trail:

        giulia was refused ACC-100511: NotYours (it exists and is somebody else's)
        giulia was refused ACC-000000: NotYours (it does not exist)

    The patient was told exactly the same thing in both cases.

==============================================================================
HOLDS   A refusal does not tell a stranger whether the document exists
==============================================================================

    accessions tried by somebody who owns none of them    12
    of which are real                                     11
    distinct answers they could tell apart                 1   (NotYours)

    This is the check that a fix for the first claim tends to fail. The natural
    repair for a leak is to look the document up, find it belongs to somebody
    else, and answer 403 — while answering 404 when there is nothing there.

    Somebody walking the accession space would then separate the 11 real numbers
    from the 1 that is not, without being handed a single byte. On a real
    archive the numbers are sequential and that is the whole patient list.

    So the two cases are one answer to the person asking, and two lines in the
    trail. The distinction is kept where it is useful and removed where it leaks.

==============================================================================
HOLDS   A code opens the one document it was sent for, and only for the patient it was sent to
==============================================================================

    requests for a code                 90
    codes actually sent                  2   (the sensitive documents, asked for by their owner)
    sent for somebody else's document    0

    the right code for the right document  opens
    the same code for another document     refused

    Two things went wrong here, and both are about what the code is attached to.

    The route read the accession out of the request body and asked for a code
    against it, without ever putting that accession and the signed-in patient into
    the same question. A patient could be sent a code, to their own phone, for a
    study belonging to somebody else.

    And confirming meant finding a live challenge with those six digits, rather
    than those six digits for this patient and this document. That is not a
    brute-force story: a lookup by digits alone finds this code, and the
    route then opened whichever accession the request named.

    A code is now minted only after the archive has said this patient would be
    handed this document but for the second factor, and it is looked up by that
    same pair. It is also used once: a code that survives its own use is a code
    somebody can replay out of a browser history.

==============================================================================
HOLDS   A session expires at the same moment whichever clock reads it
==============================================================================

    written  20260714120000   and   2026-07-14T12:00:00.0000000+00:00
    judged one minute after it should have stopped working

     reader's offset   the old way  the portal
  --------------------------------------------
              UTC-12    still good     expired
               UTC-1    still good     expired
               UTC+0       expired     expired
               UTC+1       expired     expired
               UTC+2       expired     expired
              UTC+14       expired     expired
  --------------------------------------------
   still good, of 27            12           0

    A wall clock reading does not say which wall it was on. The stamp was written
    by one process and read by another, and when those two disagreed about the
    offset the session outlived its own expiry by exactly the difference — 12 of the
    27 offsets a reader could be on, up to twelve hours past.

    Nothing failed while that was true. Nothing was logged. The number in the row
    was the number that had been written.

All 5 claims hold.
```

---

## About the original

Rebuilt from a patient portal written for a hospital group, with everything
identifying removed: no client, no hospital, no server names, no addresses, no
real accession numbers and no real reports. What is kept is the shape of the
problem and the four mistakes, which were real, and one of which handed
documents to whoever asked for them by number.

MIT licensed. See [LICENSE](LICENSE).
