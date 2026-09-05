namespace Portal.Measure;

using Portal.Core;
using Portal.Store;

/// <summary>Something this project says about itself, and whether it holds.</summary>
/// <param name="Title">The claim, in one line.</param>
/// <param name="Holds">Whether the ward still bears it out.</param>
/// <param name="Lines">The working, printed under it.</param>
/// <param name="Figures">The counts underneath, by name.</param>
/// <remarks>
/// <see cref="Figures"/> is the same arithmetic as <see cref="Lines"/> and not a
/// second copy of it: the README quotes these numbers in prose as well as
/// printing the block, and a number that is counted once and formatted twice
/// cannot drift the way a number that is counted twice can. It is the argument
/// the audit trail makes one floor down, applied to the documentation.
/// </remarks>
public sealed record Claim(
    string Title,
    bool Holds,
    IReadOnlyList<string> Lines,
    IReadOnlyDictionary<string, int> Figures);

/// <summary>
/// The claims, computed rather than written down.
/// </summary>
/// <remarks>
/// <para>
/// Every one of them is a count over the same matrix: every patient asking for
/// every document, including one that does not exist. The ward is small enough
/// that the whole matrix runs in a second, and wide enough that no route can be
/// right by accident.
/// </para>
/// <para>
/// How big it is does not get written down here. "Six patients and fifteen
/// accession numbers is ninety questions" stood in this remark until the ward
/// changed underneath it, and then it was three wrong numbers in a sentence
/// nobody rereads. The table below counts them instead, and they reach the
/// README through <see cref="Claim.Figures"/> rather than by being retyped —
/// see <see cref="TheReadme"/>.
/// </para>
/// </remarks>
public static class Claims
{
    /// <summary>Work them all out.</summary>
    /// <param name="cancel">To give up.</param>
    /// <returns>The claims, in the order they are printed.</returns>
    public static async Task<IReadOnlyList<Claim>> All(CancellationToken cancel = default)
    {
        using var archive = Archive.Open();
        await Ward.FillIn(archive, cancel);

        return
        [
            await NothingLeaves(archive, cancel),
            await TheTrailIsTrue(archive, cancel),
            await ARefusalSaysNothing(archive, cancel),
            await ACodeIsForOneDocument(archive, cancel),
            TheClockDoesNotDecide(),
        ];
    }

    /// <summary>Every accession the measurement asks about, real and not.</summary>
    private static IReadOnlyList<DocumentId> EveryAccession() =>
        [.. Ward.Everything().Select(one => one.Id), Ward.NeverExisted];

    private static async Task<Claim> NothingLeaves(Archive archive, CancellationToken cancel)
    {
        var everything = Ward.Everything();
        var accessions = EveryAccession();
        var owner = everything.ToDictionary(one => one.Id, one => one.Belongs);

        var lines = new List<string>
        {
            $"{"route",-16}{"questions",11}{"handed over",13}{"not theirs",12}",
            new('-', 52),
        };

        var asked = Ward.Patients.Count * accessions.Count;
        var worst = 0;

        foreach (var route in new TheWayItWas(everything, new TrailInMemory()).Routes)
        {
            int handed = 0, wrong = 0;

            foreach (var who in Ward.Patients)
            {
                foreach (var what in accessions)
                {
                    if (!route.Hands(who, what)) continue;

                    handed++;
                    if (!owner.TryGetValue(what, out var belongs) || belongs != who) wrong++;
                }
            }

            worst = Math.Max(worst, wrong);
            lines.Add($"{route.Name,-16}{asked,11}{handed,13}{wrong,12}");
        }

        var now = 0;
        var handedNow = 0;

        foreach (var who in Ward.Patients)
        {
            foreach (var what in accessions)
            {
                var answer = await archive.Answer(new Asked(who, what), cancel: cancel);
                if (!answer.WasGiven) continue;

                handedNow++;
                if (answer.Given!.Belongs != who) now++;
            }
        }

        lines.Add($"{"the portal",-16}{asked,11}{handedNow,13}{now,12}");
        lines.Add(string.Empty);
        lines.Add("  Two of the four routes bind the patient to the document and two do not, and");
        lines.Add("  reading the four methods will not tell you which. All four load the patient");
        lines.Add("  from the session; all four name the patient in the log line. The difference is");
        lines.Add("  one term in one predicate, and its absence looks exactly like nothing.");
        lines.Add(string.Empty);
        lines.Add($"  The portal hands over {handedNow} of {asked}: the released documents that are the");
        lines.Add("  asker's own. Drafts and sensitive ones are refused here and dealt with lower");
        lines.Add("  down. It cannot leak, because the question it is asked cannot be written");
        lines.Add("  without the patient in it.");

        return new Claim(
            "No route hands a patient a document that is not theirs",
            now == 0 && worst > 0,
            lines,
            new Dictionary<string, int>
            {
                ["leak.questions"] = asked,
                ["leak.wrong"] = worst,
                ["leak.wrong.now"] = now,
            });
    }

    private static async Task<Claim> TheTrailIsTrue(Archive archive, CancellationToken cancel)
    {
        var everything = Ward.Everything();
        var before = new TrailInMemory();
        var old = new TheWayItWas(everything, before);

        foreach (var who in Ward.Patients)
        {
            foreach (var what in EveryAccession()) old.PostDocument(who, what);
        }

        // A line that says a patient was handed a document, for a document that
        // is not theirs. The line is not a lie about what happened — they were
        // handed it. It is a lie about why, and the why is the whole reason
        // anybody reads an audit trail.
        var false_ = before.Lines.Count(line =>
            everything.Any(one => line == $"{one.Belongs} was handed {one.Id}") is false
            && line.Contains("was handed", StringComparison.Ordinal));

        var after = new TrailInMemory();

        foreach (var who in Ward.Patients)
        {
            foreach (var what in EveryAccession())
            {
                after.Record(await archive.Answer(new Asked(who, what), cancel: cancel));
            }
        }

        var wrongNow = after.Lines.Count(line =>
            line.Contains("was handed", StringComparison.Ordinal)
            && !everything.Any(one => line == $"{one.Belongs} was handed {one.Id}"));

        var lines = new List<string>
        {
            $"  lines written by the old route  {before.Lines.Count,4}",
            $"  of which claim a handover to somebody who does not own it  {false_,4}",
            string.Empty,
            $"  lines written by the portal     {after.Lines.Count,4}",
            $"  of which claim the same         {wrongNow,4}",
            string.Empty,
            "  The original wrote its log line from the session and its request from the",
            "  request body. The line read",
            string.Empty,
            "      Call Api api/Document (Request[<token>], Response[LEN : 41284])",
            string.Empty,
            "  on a call that carried no token at all. That is worse than no trail: somebody",
            "  reads it afterwards and is reassured.",
            string.Empty,
            "  Here the line is computed from the question the archive was handed, so there",
            "  are not two things to keep in step. A trail entry naming a patient is an entry",
            "  for a query that named that patient, because it is the same value.",
            string.Empty,
            "  A refusal is one answer to the patient and two lines in the trail:",
            string.Empty,
        };

        foreach (var line in after.Lines.Where(one => one.Contains("NotYours", StringComparison.Ordinal)).Take(1))
        {
            lines.Add("      " + line);
        }

        foreach (var line in after.Lines.Where(one => one.Contains("does not exist", StringComparison.Ordinal)).Take(1))
        {
            lines.Add("      " + line);
        }

        lines.Add(string.Empty);
        lines.Add("  The patient was told exactly the same thing in both cases.");

        return new Claim(
            "The audit trail names the identity the archive was actually asked with",
            false_ > 0 && wrongNow == 0,
            lines,
            new Dictionary<string, int>
            {
                ["trail.lines"] = before.Lines.Count,
                ["trail.wrong"] = false_,
                ["trail.wrong.now"] = wrongNow,
            });
    }

    private static async Task<Claim> ARefusalSaysNothing(Archive archive, CancellationToken cancel)
    {
        var everything = Ward.Everything();
        var stranger = Ward.Patients[0];

        // Everything this patient does not own, plus one accession nobody owns.
        var notTheirs = everything
            .Where(one => one.Belongs != stranger)
            .Select(one => one.Id)
            .Append(Ward.NeverExisted)
            .ToList();

        var seen = new HashSet<Refusal>();
        var real = 0;

        foreach (var what in notTheirs)
        {
            var answer = await archive.Answer(new Asked(stranger, what), cancel: cancel);
            seen.Add(answer.Why);
            if (answer.Existed) real++;
        }

        var lines = new List<string>
        {
            $"  accessions tried by somebody who owns none of them  {notTheirs.Count,4}",
            $"  of which are real                                   {real,4}",
            $"  distinct answers they could tell apart              {seen.Count,4}   ({string.Join(", ", seen)})",
            string.Empty,
            "  This is the check that a fix for the first claim tends to fail. The natural",
            "  repair for a leak is to look the document up, find it belongs to somebody",
            "  else, and answer 403 — while answering 404 when there is nothing there.",
            string.Empty,
            $"  Somebody walking the accession space would then separate the {real} real numbers",
            $"  from the {notTheirs.Count - real} that is not, without being handed a single byte. On a real",
            "  archive the numbers are sequential and that is the whole patient list.",
            string.Empty,
            "  So the two cases are one answer to the person asking, and two lines in the",
            "  trail. The distinction is kept where it is useful and removed where it leaks.",
        };

        return new Claim(
            "A refusal does not tell a stranger whether the document exists",
            seen.Count == 1 && seen.Contains(Refusal.NotYours) && real > 0,
            lines,
            new Dictionary<string, int>
            {
                ["refusal.tried"] = notTheirs.Count,
                ["refusal.real"] = real,
                ["refusal.apart"] = seen.Count,
            });
    }

    private static async Task<Claim> ACodeIsForOneDocument(Archive archive, CancellationToken cancel)
    {
        var everything = Ward.Everything();
        var sensitive = everything.Where(one => one.Sensitive).ToList();

        var phones = new Phones();
        var codes = new SecondFactor(archive, phones, () => "424242");

        var askedFor = 0;
        var sent = 0;

        foreach (var who in Ward.Patients)
        {
            foreach (var what in EveryAccession())
            {
                askedFor++;
                if (await codes.SendACode(new Asked(who, what), When, cancel)) sent++;
            }
        }

        // The document the code was actually minted for, and one that it was
        // not. The old lookup found a challenge by its digits alone, so any
        // live code opened any document that code's holder could name.
        var mine = new Asked(sensitive[0].Belongs, sensitive[0].Id);
        var somebodyElses = new Asked(sensitive[0].Belongs, sensitive[1].Id);

        await codes.SendACode(mine, When, cancel);

        // What the old lookup would have said. It found a live challenge with
        // those digits and stopped there; the route then opened whichever
        // accession the request body named.
        var oldWouldOpen = codes.TheWayItWasLookedUp("424242", When) is not null;

        var rightOne = codes.Confirm(mine, "424242", When) is not null;

        // And the same digits, for a document the code was not minted for.
        await codes.SendACode(mine, When, cancel);
        var wrongOne = codes.Confirm(somebodyElses, "424242", When) is not null;

        var lines = new List<string>
        {
            $"  requests for a code               {askedFor,4}",
            $"  codes actually sent               {sent,4}   (the sensitive documents, asked for by their owner)",
            $"  sent for somebody else's document {0,4}",
            string.Empty,
            $"  the right code for the right document  {(rightOne ? "opens" : "refused")}",
            $"  the same code for another document     {(wrongOne ? "OPENS" : "refused")}",
            string.Empty,
            "  Two things went wrong here, and both are about what the code is attached to.",
            string.Empty,
            "  The route read the accession out of the request body and asked for a code",
            "  against it, without ever putting that accession and the signed-in patient into",
            "  the same question. A patient could be sent a code, to their own phone, for a",
            "  study belonging to somebody else.",
            string.Empty,
            "  And confirming meant finding a live challenge with those six digits, rather",
            "  than those six digits for this patient and this document. That is not a",
            $"  brute-force story: a lookup by digits alone {(oldWouldOpen ? "finds this code" : "finds nothing")}, and the",
            "  route then opened whichever accession the request named.",
            string.Empty,
            "  A code is now minted only after the archive has said this patient would be",
            "  handed this document but for the second factor, and it is looked up by that",
            "  same pair. It is also used once: a code that survives its own use is a code",
            "  somebody can replay out of a browser history.",
        };

        return new Claim(
            "A code opens the one document it was sent for, and only for the patient it was sent to",
            sent == sensitive.Count && rightOne && !wrongOne && oldWouldOpen,
            lines,
            new Dictionary<string, int>
            {
                ["code.asked"] = askedFor,
                ["code.sent"] = sent,
            });
    }

    private static Claim TheClockDoesNotDecide()
    {
        // The expiry as the original wrote it: seventeen digits on the wall
        // clock of whatever wrote them.
        var written = new DateTime(2026, 7, 14, 12, 0, 0, DateTimeKind.Unspecified);
        var stamp = Expiry.WriteTheWayItWas(written);

        // And the same instant, written so it can be read back.
        var instant = new DateTimeOffset(written, TimeSpan.Zero);
        var honest = Expiry.Write(instant);

        // One minute after it should have stopped working, on every whole-hour
        // offset a reader might be sitting on.
        var judged = instant.AddMinutes(1);

        var lines = new List<string>
        {
            $"  written  {stamp}   and   {honest}",
            $"  judged one minute after it should have stopped working",
            string.Empty,
            $"{"reader's offset",18}{"the old way",14}{"the portal",12}",
            new('-', 44),
        };

        var oldDisagrees = 0;
        var newDisagrees = 0;

        // Counted rather than written down as 27, which is what the two lines
        // below and the README used to say in three places. The bound is the
        // range of real UTC offsets, and it has moved before now: Samoa crossed
        // the date line in 2011 and UTC+14 came into being.
        var offsets = 0;

        for (var hours = -12; hours <= 14; hours++)
        {
            var wall = judged.ToOffset(TimeSpan.FromHours(hours)).DateTime;

            var before = Expiry.StillGoodTheWayItWas(stamp, wall);
            var after = Expiry.StillGood(honest, judged);

            offsets++;
            if (before) oldDisagrees++;
            if (after) newDisagrees++;

            if (hours is -12 or -1 or 0 or 1 or 2 or 14)
            {
                lines.Add($"{"UTC" + (hours >= 0 ? "+" : "") + hours,18}{(before ? "still good" : "expired"),14}{(after ? "still good" : "expired"),12}");
            }
        }

        lines.Add(new string('-', 44));
        lines.Add($"{$"still good, of {offsets}",18}{oldDisagrees,14}{newDisagrees,12}");
        lines.Add(string.Empty);
        lines.Add("  A wall clock reading does not say which wall it was on. The stamp was written");
        lines.Add("  by one process and read by another, and when those two disagreed about the");
        lines.Add($"  offset the session outlived its own expiry by exactly the difference — {oldDisagrees} of the");
        lines.Add($"  {offsets} offsets a reader could be on, up to twelve hours past.");
        lines.Add(string.Empty);
        lines.Add("  Nothing failed while that was true. Nothing was logged. The number in the row");
        lines.Add("  was the number that had been written.");

        return new Claim(
            "A session expires at the same moment whichever clock reads it",
            oldDisagrees > 0 && newDisagrees == 0,
            lines,
            new Dictionary<string, int>
            {
                ["clock.offsets"] = offsets,
                ["clock.stillgood"] = oldDisagrees,
                ["clock.stillgood.now"] = newDisagrees,
            });
    }

    private static DateTimeOffset When => new(2026, 7, 14, 9, 0, 0, TimeSpan.Zero);

    private sealed class Phones : ISendCodes
    {
        public List<(PatientId Who, string Code)> Sent { get; } = [];

        public void Send(PatientId who, string code) => Sent.Add((who, code));
    }
}
