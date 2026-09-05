namespace Portal.Measure;

using Portal.Store;

/// <summary>
/// The README, read back and held against the run that just happened.
/// </summary>
/// <remarks>
/// <para>
/// The measurement block at the bottom of the README is regenerated and diffed
/// by CI, so it cannot go stale. The prose above it can, and the prose is where
/// a reader actually looks: the summary table quotes nine of these numbers in
/// the first screenful, before anybody has scrolled as far as the block. A
/// number copied into a sentence is a number that was true once.
/// </para>
/// <para>
/// It had already happened here. The README said one of the six patients had no
/// documents; all six had some, and had done for as long as the sentence had
/// been there. Nothing failed, because nothing was looking — which is the same
/// thing this repository says about a missing term in a predicate, one floor
/// down.
/// </para>
/// <para>
/// So every figure the prose quotes is written below as the sentence it appears
/// in, with the count spliced in from <see cref="Claim.Figures"/>. Change the
/// ward and the sentence stops being found, and the program exits non-zero the
/// same way it does when a claim itself stops holding. It is the same failure:
/// the repository saying something about itself that is not so.
/// </para>
/// <para>
/// Rewording a sentence breaks this too, and that is the price rather than a
/// defect. The cheaper check — look for the bare number anywhere in the file —
/// passes on the day the sentence around it is deleted, and passes again on the
/// day the number turns up somewhere it does not mean the same thing.
/// </para>
/// </remarks>
public static class TheReadme
{
    /// <summary>
    /// The words for the small numbers, because the prose writes them out.
    /// </summary>
    /// <remarks>
    /// Twenty is further than this ward is going. Past it <see cref="Spelled"/>
    /// refuses rather than falling back to digits: a check that quietly began
    /// looking for "21 documents" in prose that says "twenty-one" would pass by
    /// finding nothing, on the day it stopped meaning anything.
    /// </remarks>
    private static readonly string[] Words =
    [
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
        "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen",
        "nineteen", "twenty",
    ];

    /// <summary>
    /// The sentences the README's prose has to contain for the claims to be
    /// described as well as counted.
    /// </summary>
    /// <param name="claims">The claims as they were just worked out.</param>
    /// <returns>Each sentence, with this run's figures in it.</returns>
    public static IReadOnlyList<string> WhatItQuotes(IReadOnlyList<Claim> claims)
    {
        var counted = new Dictionary<string, int>(StringComparer.Ordinal);

        // Add rather than assign: two claims counting different things under one
        // name is a collision that would otherwise resolve silently, in whatever
        // order the claims happen to run.
        foreach (var claim in claims)
        {
            foreach (var (name, count) in claim.Figures) counted.Add(name, count);
        }

        var patients = Ward.Patients.Count;
        var documents = Ward.Everything().Count;
        var withNothing = Ward.Patients.Count(who => !Ward.Everything().Any(one => one.Belongs == who));

        return
        [
            // The five claims, as the summary table states them.
            $"Over {Count("leak.questions")} questions",
            $"hand over **{Count("leak.wrong")}** documents belonging to somebody else",
            $"This portal hands over **{Count("leak.wrong.now")}**.",
            $"**{Count("trail.wrong")}** of the old route's {Count("trail.lines")} lines",
            $"The portal's: **{Count("trail.wrong.now")}**.",
            $"{Count("refusal.tried")} accession numbers tried by somebody who owns none of them",
            $"**{Count("refusal.real")}** of them real",
            $"**{Count("refusal.apart")}** answer they can tell apart",
            $"still valid on **{Count("clock.stillgood")}** of the {Count("clock.offsets")} offsets",

            // And the ward the claims are counted over, which is the sentence
            // that was wrong.
            $"{Capital(Spelled(patients))} given names, {Spelled(documents)} documents of placeholder text",
            $"{Capital(Spelled(withNothing))} of the {Spelled(patients)} patients has no documents",
        ];

        int Count(string name) =>
            counted.TryGetValue(name, out var found)
                ? found
                : throw new InvalidOperationException(
                    $"No claim counted '{name}', so there is nothing to hold the README to.");
    }

    /// <summary>Which of them the README does not say.</summary>
    /// <param name="sentences">What it ought to say.</param>
    /// <returns>The ones that are not in it, which is empty when all is well.</returns>
    public static IReadOnlyList<string> NotSaid(IReadOnlyList<string> sentences)
    {
        // An empty list would come back empty and pass, for ever, however wrong
        // the file got. So the emptiness is what is tested first.
        if (sentences.Count == 0)
        {
            throw new InvalidOperationException("Nothing was held against the README. Nothing was checked.");
        }

        var prose = Prose();

        return [.. sentences.Where(one => !prose.Contains(one, StringComparison.Ordinal))];
    }

    /// <summary>The README, without the block, on one line.</summary>
    /// <returns>The prose, with every run of whitespace collapsed to a space.</returns>
    public static string Prose()
    {
        var text = File.ReadAllText(Path.Combine(Root(), "README.md"));

        // The measurement block comes out first. CI regenerates it and diffs it,
        // which is a stronger check than this one and does not want doing twice —
        // and it holds every figure below in digits, so leaving it in would let a
        // check on the prose pass by finding its number in the one part of the
        // file it is not checking.
        const string Heading = "## The measurement, in full";

        var section = text.IndexOf(Heading, StringComparison.Ordinal);
        var opens = section < 0 ? -1 : text.IndexOf("\n```", section, StringComparison.Ordinal);
        var closes = opens < 0 ? -1 : text.IndexOf("\n```", opens + 4, StringComparison.Ordinal);

        if (closes < 0)
        {
            throw new InvalidOperationException(
                $"No fenced block under \"{Heading}\" in the README. This check works by leaving that "
                + "block out, and it will not run over a file it cannot find it in.");
        }

        var prose = text[..section] + text[(closes + 4)..];

        // Collapsed, because the prose is hard-wrapped and most of these
        // sentences have a line break somewhere in the middle of them. Rewrapping
        // a paragraph must not be the thing that stops the build.
        return string.Join(' ', prose.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>The word for a number, as the prose writes it.</summary>
    /// <param name="number">The number.</param>
    /// <returns>Its name, in lower case.</returns>
    public static string Spelled(int number) =>
        number >= 0 && number < Words.Length
            ? Words[number]
            : throw new InvalidOperationException(
                $"The README spells its small numbers out and there is no word here for {number}.");

    private static string Capital(string word) => char.ToUpperInvariant(word[0]) + word[1..];

    /// <summary>The repository this assembly was built inside.</summary>
    /// <remarks>
    /// Walked up from the assembly rather than taken from the working directory.
    /// <c>dotnet test</c>, <c>dotnet run --project</c> and the built dll do not
    /// agree about what the working directory is, and a check that passes because
    /// it could not find the file is not a check.
    /// </remarks>
    private static string Root()
    {
        for (var here = new DirectoryInfo(AppContext.BaseDirectory); here is not null; here = here.Parent)
        {
            if (File.Exists(Path.Combine(here.FullName, "README.md"))
                && File.Exists(Path.Combine(here.FullName, "Portal.sln")))
            {
                return here.FullName;
            }
        }

        throw new InvalidOperationException(
            $"No README.md beside a Portal.sln anywhere above {AppContext.BaseDirectory}. This check "
            + "reads the repository it is checking, so not finding it is a failure and not a pass.");
    }
}
