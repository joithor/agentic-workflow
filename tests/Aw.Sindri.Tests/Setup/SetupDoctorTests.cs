using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Setup;

/// <summary>Doctor's 'setup' check: ok when every step is done or skipped; otherwise a warning that names the steps still open, with the owner's next move.</summary>
public sealed class SetupDoctorTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<DoctorCheck> Run(InMemorySetupFacts facts) => Assert.Single(await new SetupDoctor(facts).Run(Token));

    [Fact, Trait("Lane", "unit")]
    public async Task Every_step_done_is_ok()
    {
        var check = await Run(InMemorySetupFacts.AllDone());

        Assert.Equal(("setup", CheckState.Ok), (check.Name, check.State));
        Assert.Null(check.Fix);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_skipped_onyx_still_counts_as_finished()
    {
        var facts = InMemorySetupFacts.AllDone();
        facts.OnyxAnswer = new Core.Setup.OnyxFact(Core.Onyx.OnyxState.NotSetUp, Unreachable: false, BlockedByCode: null) { Deferred = true };

        Assert.Equal(CheckState.Ok, (await Run(facts)).State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_fresh_box_warns_and_names_the_first_open_step_and_its_move()
    {
        var check = await Run(new InMemorySetupFacts());

        Assert.Equal(CheckState.Warn, check.State);
        Assert.Contains("repo", check.Detail, StringComparison.Ordinal);
        Assert.Equal("sindri repo add <path>", check.Fix);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_step_that_cannot_be_read_is_a_warning_not_a_throw()
    {
        var facts = InMemorySetupFacts.AllDone();
        facts.OnyxAnswer = Aw.Sindri.Core.SindriErrors.ApiInternal.With("boom");

        var check = await Run(facts);

        Assert.Equal(CheckState.Warn, check.State);
        Assert.Contains("onyx", check.Detail, StringComparison.Ordinal);
    }
}
