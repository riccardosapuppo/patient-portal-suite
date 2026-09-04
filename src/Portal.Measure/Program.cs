// Runs the ward through everything and prints what came out.
//
// It exits non-zero when a claim stops holding, so it is a check and not a
// demonstration: the README quotes these numbers, CI runs this, and a figure
// that stops being true stops the build.
using Portal.Measure;
using Portal.Store;

Console.WriteLine();
Console.WriteLine(
    $"An invented ward: {Ward.Patients.Count} patients, {Ward.Everything().Count} documents, "
    + $"and one accession number ({Ward.NeverExisted}) that is not in the archive.");
Console.WriteLine();

foreach (var who in Ward.Patients)
{
    var theirs = Ward.Everything().Where(one => one.Belongs == who).ToList();

    Console.WriteLine(
        $"  {who,-10} {theirs.Count} documents"
        + (theirs.Count == 0
            ? " — the empty page, which a portal that only runs against busy patients never draws"
            : $": {theirs.Count(one => one.Released && !one.Sensitive)} released, "
              + $"{theirs.Count(one => !one.Released)} draft, {theirs.Count(one => one.Sensitive)} sensitive"));
}

var claims = await Claims.All();

foreach (var claim in claims)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', 78));
    Console.WriteLine(claim.Holds ? "HOLDS   " + claim.Title : "BROKEN  " + claim.Title);
    Console.WriteLine(new string('=', 78));
    Console.WriteLine();

    foreach (var line in claim.Lines) Console.WriteLine(line.Length == 0 ? string.Empty : "  " + line);
}

Console.WriteLine();

var broken = claims.Where(claim => !claim.Holds).ToList();

if (broken.Count == 0)
{
    Console.WriteLine($"All {claims.Count} claims hold.");
    return 0;
}

foreach (var claim in broken) Console.WriteLine($"BROKEN: {claim.Title}");

return 1;
