namespace Portal.Core;

using System.Globalization;

/// <summary>
/// When a sign-in stops being good for anything.
/// </summary>
/// <remarks>
/// <para>
/// The original wrote the expiry as <c>yyyyMMddHHmmssFFF</c> — seventeen digits
/// with no offset — and asked whether <c>DateTime.Now</c> was still before it.
/// Both halves of that are a problem, and they are the same problem: a wall
/// clock reading does not say which wall it was on.
/// </para>
/// <para>
/// The stamp was written by one process and read by another. When those two
/// disagreed about the offset — a portal in one place and an archive in
/// another, or the same machine either side of a daylight-saving change — the
/// session outlived its own expiry by exactly the difference. Nothing failed,
/// nothing was logged, and the number in the row was the number that had been
/// written.
/// </para>
/// <para>
/// The replacement stores an instant, round-tripped with its offset, and
/// compares instants. It is not more careful; it is a different question, and
/// the answer to it does not depend on who is asking.
/// </para>
/// </remarks>
public static class Expiry
{
    /// <summary>How the original wrote it: seventeen digits, no offset.</summary>
    public const string TheOldStamp = "yyyyMMddHHmmssFFF";

    /// <summary>Write an instant so that reading it back cannot lose the offset.</summary>
    /// <param name="when">The moment the session stops being good.</param>
    /// <returns>A round-trippable stamp.</returns>
    public static string Write(DateTimeOffset when) =>
        when.ToString("o", CultureInfo.InvariantCulture);

    /// <summary>Is the session still good?</summary>
    /// <param name="stamp">What <see cref="Write"/> produced.</param>
    /// <param name="now">The moment to judge it at.</param>
    /// <returns>True while it is still good. False when the stamp is unreadable.</returns>
    /// <remarks>
    /// An unreadable stamp is not good. Silence is not permission: the original
    /// got this right, and it is worth keeping right, because the tempting
    /// version of a parse failure is to carry on.
    /// </remarks>
    public static bool StillGood(string? stamp, DateTimeOffset now) =>
        DateTimeOffset.TryParseExact(
            stamp,
            "o",
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var until)
        && now < until;

    /// <summary>
    /// The original: seventeen digits, read on whatever clock happened to be
    /// running.
    /// </summary>
    /// <param name="stamp">The seventeen digits.</param>
    /// <param name="nowOnThisWall">
    /// The reader's own wall clock, which is where the offset gets in.
    /// </param>
    /// <returns>What the original would have said.</returns>
    public static bool StillGoodTheWayItWas(string? stamp, DateTime nowOnThisWall) =>
        DateTime.TryParseExact(
            stamp,
            TheOldStamp,
            null,
            DateTimeStyles.None,
            out var until)
        && nowOnThisWall < until;

    /// <summary>Write the seventeen digits, as the original did.</summary>
    /// <param name="wallClock">A reading with no offset attached.</param>
    /// <returns>The stamp.</returns>
    public static string WriteTheWayItWas(DateTime wallClock) =>
        wallClock.ToString(TheOldStamp, CultureInfo.InvariantCulture);
}
