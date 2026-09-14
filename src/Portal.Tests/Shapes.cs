namespace Portal.Tests;

using System.Reflection;
using System.Text.RegularExpressions;

using Portal.Core;
using Portal.Measure;
using Xunit;

/// <summary>
/// Checks about the shape of the code rather than about what it does.
/// </summary>
/// <remarks>
/// These are the ones that matter most and read the strangest. Everything else
/// in this repository tests that the portal answers correctly; these test that
/// the wrong answer cannot be written. A behavioural test catches the bug that
/// is there. These catch the bug somebody is about to add.
/// </remarks>
public class Shapes
{
    /// <summary>
    /// The default button look must stay out of the cascade's way.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Green and filled is what a button looks like here unless something says
    /// otherwise, and the patient cards on the sign-in page are the otherwise:
    /// they restate the background, the ink and the padding, and they read
    /// correctly. Written as a plain element rule, though, the default also
    /// owned <c>:hover</c> — a state the cards had no opinion about. Putting a
    /// pointer on a card repainted it bottle green while the name kept the dark
    /// ink the card had asked for, and the name vanished.
    /// </para>
    /// <para>
    /// Nothing failed, nothing was logged, and no screenshot shows it: a hover
    /// is not a state anybody photographs. It was found by a person moving a
    /// mouse, which is the worst way to find anything.
    /// </para>
    /// <para>
    /// <c>:where()</c> makes the block count for nothing, so a rule that names a
    /// button by class wins on every property it mentions and on the ones it
    /// never thought to mention. This holds that, because the repair is one
    /// word and losing it again is one word too.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheDefaultButtonLookCannotOutrankACardThatIsAButton()
    {
        var sheet = Path.Combine(TheReadme.Root(), "src", "Portal.Web", "wwwroot", "portal.css");
        var css = File.ReadAllText(sheet);

        // Read first, asserted after: a check that passed because it could not
        // find the file is not a check.
        Assert.True(css.Length > 0, $"{sheet} is empty, so nothing here was checked.");

        Assert.Contains(":where(button)", css, StringComparison.Ordinal);

        var element = Regex.Matches(css, @"(?m)^button\s*(:[\w-]+)?\s*\{")
            .Select(one => one.Value.Trim())
            .ToList();

        Assert.True(
            element.Count == 0,
            "The default button look is written as an element rule again — "
            + string.Join(", ", element)
            + " — so it outranks nothing and overrules anything that styled a button by class "
            + "in a state that rule did not mention.");
    }

    [Fact]
    public void NothingCanAskTheArchiveAboutADocumentWithoutSayingWho()
    {
        // The whole thesis, as a loop. Every method on the interface either
        // takes a PatientId or takes an Asked, which contains one — so there is
        // no question the archive will answer that does not name the patient.
        //
        // Add `Task<Document?> Find(DocumentId id)` to IDocuments and this
        // fails, before any route has had the chance to call it.
        foreach (var method in typeof(IDocuments).GetMethods())
        {
            var takes = method.GetParameters().Select(one => one.ParameterType).ToList();

            Assert.True(
                takes.Contains(typeof(PatientId)) || takes.Contains(typeof(Asked)),
                $"IDocuments.{method.Name} can be called without naming a patient");
        }
    }

    [Fact]
    public void AskedCannotBeBuiltWithoutBothHalves()
    {
        var constructors = typeof(Asked).GetConstructors();

        Assert.All(constructors, one =>
        {
            var takes = one.GetParameters().Select(p => p.ParameterType).ToList();

            // The parameterless constructor every struct has is the exception
            // the language forces on us, and it produces a question with an
            // empty patient and an empty document — which matches nothing.
            if (takes.Count == 0) return;

            Assert.Contains(typeof(PatientId), takes);
            Assert.Contains(typeof(DocumentId), takes);
        });
    }

    [Fact]
    public void AReceiptForTheSecondFactorCannotBeForged()
    {
        // Confirmed has an internal constructor, so the only thing that can
        // produce one is SecondFactor.Confirm. A page cannot decide on its own
        // that the code was checked.
        Assert.Empty(typeof(Confirmed).GetConstructors(BindingFlags.Public | BindingFlags.Instance));

        // And Confirm is the one thing that hands them out.
        var minted = typeof(SecondFactor)
            .GetMethods()
            .Where(one => one.ReturnType == typeof(Confirmed?))
            .Select(one => one.Name)
            .ToList();

        Assert.Equal(["Confirm"], minted);
    }

    [Fact]
    public void AnAnswerCarriesTheQuestionItAnswered()
    {
        // Which is what makes the audit line unable to name anybody else. If
        // this property is ever dropped, the trail becomes a second thing to
        // keep in step with the query — which is exactly the bug.
        var question = new Asked(new PatientId("giulia"), new DocumentId("ACC-1"));
        var answer = Answer.No(question, Refusal.NotYours, existed: false);

        Assert.Equal(question, answer.Question);
        Assert.Contains("giulia", answer.Line, StringComparison.Ordinal);
        Assert.Contains("ACC-1", answer.Line, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-12)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(14)]
    public void AnExpiredSessionIsExpiredOnEveryClock(int hours)
    {
        var until = new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
        var aMinuteLate = until.AddMinutes(1);

        Assert.False(Expiry.StillGood(Expiry.Write(until), aMinuteLate));

        // And the same instant, read on a clock somewhere else, is still
        // expired. The old seventeen digits are not: see the measurement.
        Assert.False(Expiry.StillGood(Expiry.Write(until), aMinuteLate.ToOffset(TimeSpan.FromHours(hours))));
    }

    [Fact]
    public void AnUnreadableExpiryIsNotPermission()
    {
        var now = DateTimeOffset.UtcNow;

        Assert.False(Expiry.StillGood(null, now));
        Assert.False(Expiry.StillGood(string.Empty, now));
        Assert.False(Expiry.StillGood("tomorrow", now));
        Assert.False(Expiry.StillGood("20260714120000", now));
    }

    [Fact]
    public void TheOldExpiryDisagreesWithItselfAcrossTheWorld()
    {
        // Kept as a check rather than left to the measurement, because it is
        // the reason the format changed and somebody will otherwise decide the
        // seventeen digits were fine.
        var until = new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
        var stamp = Expiry.WriteTheWayItWas(until.DateTime);
        var aMinuteLate = until.AddMinutes(1);

        Assert.False(Expiry.StillGoodTheWayItWas(stamp, aMinuteLate.UtcDateTime));
        Assert.True(Expiry.StillGoodTheWayItWas(stamp, aMinuteLate.ToOffset(TimeSpan.FromHours(-5)).DateTime));
    }
}
