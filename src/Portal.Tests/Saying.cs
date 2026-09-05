namespace Portal.Tests;

using System.Reflection;

using Portal.Measure;
using Xunit;

/// <summary>
/// What the repository says about itself, held against what it is.
/// </summary>
/// <remarks>
/// <para>
/// The five claims are checked by <c>Portal.Measure</c>, which reads the
/// README's prose back and fails when a figure in it has stopped being the
/// figure it just counted. This is the one number that program cannot reach: it
/// does not know how many checks there are, because counting them means being
/// one of them.
/// </para>
/// <para>
/// So it is counted here, out of the assembly, rather than typed into the README
/// and left. It had been typed and left before now — the sentence about the ward
/// went stale exactly that way — and a count of checks is the figure most likely
/// to, because it changes on the day somebody is thinking about something else.
/// </para>
/// </remarks>
public class Saying
{
    [Fact]
    public void TheReadmeSaysHowManyChecksThereAre()
    {
        var cases = typeof(Saying).Assembly
            .GetTypes()
            .SelectMany(one => one.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Sum(Cases);

        // Not a number in an assertion, which would only move the copy from the
        // README into here. The README is the thing being checked, so the count
        // is spliced into the two sentences that carry it and both have to be in
        // the file — including the one in the command block, which is the line
        // somebody actually reads before running anything.
        var missing = TheReadme.NotSaid(
        [
            $"dotnet test src/Portal.Tests # {cases} checks",
            $"| {cases} checks: the archive asked directly",
        ]);

        Assert.True(
            missing.Count == 0,
            $"There are {cases} checks in this assembly and the README does not say so: "
            + string.Join(" / ", missing));
    }

    /// <summary>How many cases one method is.</summary>
    /// <remarks>
    /// A <c>[Theory]</c> is as many as it has rows, and <c>TheoryAttribute</c>
    /// derives from <c>FactAttribute</c>, so the rows are counted first and the
    /// plain facts are what is left.
    /// </remarks>
    private static int Cases(MethodInfo method)
    {
        if (method.GetCustomAttribute<FactAttribute>(inherit: false) is null) return 0;

        var rows = method.GetCustomAttributes<InlineDataAttribute>(inherit: false).Count();

        return Math.Max(rows, 1);
    }
}
