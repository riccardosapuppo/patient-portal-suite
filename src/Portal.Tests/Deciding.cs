namespace Portal.Tests;

using Portal.Core;
using Portal.Measure;
using Portal.Store;
using Xunit;

/// <summary>
/// The archive, asked directly. No web server, no cookie, no HTTP.
/// </summary>
public class Deciding : IAsyncLifetime
{
    private readonly Archive archive = Archive.Open();

    private static Document Hers => Ward.Everything().First(one => one is { Released: true, Sensitive: false });

    private static Document Draft => Ward.Everything().First(one => !one.Released);

    private static Document Sensitive => Ward.Everything().First(one => one.Sensitive);

    private static PatientId SomebodyElse =>
        Ward.Patients.First(one => one != Hers.Belongs);

    public Task InitializeAsync() => Ward.FillIn(archive);

    public Task DisposeAsync()
    {
        archive.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task APatientIsHandedTheirOwnReleasedDocument()
    {
        var answer = await archive.Answer(new Asked(Hers.Belongs, Hers.Id));

        Assert.True(answer.WasGiven);
        Assert.Equal(Hers.Id, answer.Given!.Id);
    }

    [Fact]
    public async Task NobodyIsHandedSomebodyElsesDocument()
    {
        // Every patient, every document, one assertion. The matrix is generated
        // rather than listed, so a document added to the ward is covered on the
        // day it is added.
        foreach (var who in Ward.Patients)
        {
            foreach (var document in Ward.Everything())
            {
                var answer = await archive.Answer(new Asked(who, document.Id));

                if (answer.WasGiven) Assert.Equal(who, answer.Given!.Belongs);
            }
        }
    }

    [Fact]
    public async Task ADraftIsNotHandedOverEvenToItsOwner()
    {
        var answer = await archive.Answer(new Asked(Draft.Belongs, Draft.Id));

        Assert.False(answer.WasGiven);
        Assert.Equal(Refusal.NotReleasedYet, answer.Why);
    }

    [Fact]
    public async Task ARefusalToAStrangerIsTheSameWhetherItExistsOrNot()
    {
        var real = await archive.Answer(new Asked(SomebodyElse, Hers.Id));
        var invented = await archive.Answer(new Asked(SomebodyElse, Ward.NeverExisted));

        Assert.Equal(real.Why, invented.Why);
        Assert.Equal(Refusal.NotYours, real.Why);

        // And the trail, which the patient never sees, does tell them apart.
        Assert.True(real.Existed);
        Assert.False(invented.Existed);
        Assert.NotEqual(real.Line, invented.Line);
    }

    [Fact]
    public async Task TheListIsOnlyEverTheirOwn()
    {
        foreach (var who in Ward.Patients)
        {
            var list = await archive.ListFor(who);

            Assert.All(list, one => Assert.Equal(who, one.Belongs));
            Assert.Equal(Ward.Everything().Count(one => one.Belongs == who), list.Count);
        }
    }

    [Fact]
    public async Task TheListCarriesNoContent()
    {
        // The list is drawn on a page that shows drafts and sensitive documents
        // as rows. If the rows carried bytes, the page would be handing out
        // documents it is drawing a "not yet" beside.
        var list = await archive.ListFor(Draft.Belongs);

        Assert.All(list, one => Assert.Empty(one.Content));
    }

    [Fact]
    public async Task ASensitiveDocumentNeedsACodeAndThenOpens()
    {
        var question = new Asked(Sensitive.Belongs, Sensitive.Id);
        var phones = new Phones();
        var codes = new SecondFactor(archive, phones, () => "123456");

        Assert.Equal(Refusal.NeedsASecondFactor, (await archive.Answer(question)).Why);

        Assert.True(await codes.SendACode(question, Now));
        Assert.Single(phones.Sent);
        Assert.Equal(Sensitive.Belongs, phones.Sent[0].Who);

        var receipt = codes.Confirm(question, "123456", Now);
        Assert.NotNull(receipt);

        var answer = await archive.Answer(question, receipt);
        Assert.True(answer.WasGiven);
    }

    [Fact]
    public async Task ACodeForOneDocumentDoesNotOpenAnother()
    {
        var sensitive = Ward.Everything().Where(one => one.Sensitive).ToList();
        var mine = new Asked(sensitive[0].Belongs, sensitive[0].Id);
        var another = new Asked(sensitive[0].Belongs, sensitive[1].Id);

        var codes = new SecondFactor(archive, new Phones(), () => "123456");
        await codes.SendACode(mine, Now);

        // The digits are right. The document is not.
        Assert.Null(codes.Confirm(another, "123456", Now));

        // And the old lookup, kept runnable, would have found it.
        Assert.NotNull(codes.TheWayItWasLookedUp("123456", Now));
    }

    [Fact]
    public async Task ACodeIsNotSentForSomebodyElsesDocument()
    {
        var sensitive = Ward.Everything().First(one => one.Sensitive);
        var stranger = Ward.Patients.First(one => one != sensitive.Belongs);

        var phones = new Phones();
        var codes = new SecondFactor(archive, phones, () => "123456");

        Assert.False(await codes.SendACode(new Asked(stranger, sensitive.Id), Now));
        Assert.Empty(phones.Sent);
    }

    [Fact]
    public async Task ACodeIsUsedOnce()
    {
        var question = new Asked(Sensitive.Belongs, Sensitive.Id);
        var codes = new SecondFactor(archive, new Phones(), () => "123456");

        await codes.SendACode(question, Now);

        Assert.NotNull(codes.Confirm(question, "123456", Now));
        Assert.Null(codes.Confirm(question, "123456", Now));
    }

    [Fact]
    public async Task ACodeRunsOut()
    {
        var question = new Asked(Sensitive.Belongs, Sensitive.Id);
        var codes = new SecondFactor(archive, new Phones(), () => "123456");

        await codes.SendACode(question, Now);

        Assert.Null(codes.Confirm(question, "123456", Now + SecondFactor.Lasts));
    }

    [Fact]
    public async Task EveryClaimTheReadmeMakesStillHolds()
    {
        foreach (var claim in await Claims.All()) Assert.True(claim.Holds, claim.Title);
    }

    private static DateTimeOffset Now => new(2026, 7, 14, 9, 0, 0, TimeSpan.Zero);

    private sealed class Phones : ISendCodes
    {
        public List<(PatientId Who, string Code)> Sent { get; } = [];

        public void Send(PatientId who, string code) => Sent.Add((who, code));
    }
}
