using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands.Chain;

/// <summary>The real chain on ApproveProfile, an AppConfirm row (setup-once spec §4 items 6 and 7): the app passes, a session and no caller don't, and the prover passes only in a marked scratch home.</summary>
public sealed class AppConfirmChainTests
{
    private static readonly ApproveProfileRequest _preview = new(Hash: null);

    private static async Task<string?> Code(ChainHarness chain, CallerContext? caller)
    {
        var result = await chain.Run<ApproveProfileRequest, ApproveProfileResponse>(_preview, caller);
        return result.IsError ? result.Error.Code : null;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_app_passes_the_chain_on_an_AppConfirm_row()
    {
        await using var chain = new ChainHarness();
        chain.H.Profiles.Live = SliceHarness.Profile();
        _ = await chain.H.HoldLease();

        Assert.Null(await Code(chain, A.Caller().Human().Client(ClientLabel.Ui).Build()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_and_a_caller_with_no_token_are_refused_before_the_handler()
    {
        await using var chain = new ChainHarness();
        chain.H.Profiles.Live = SliceHarness.Profile();
        _ = await chain.H.HoldLease();

        Assert.Equal("SND-AUTH-002", await Code(chain, A.Caller().Session().Build()));
        Assert.Equal("SND-AUTH-002", await Code(chain, caller: null));
        Assert.Equal("SND-AUTH-002", await Code(chain, A.Caller().Service("jobs").Build()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_prover_is_refused_in_a_home_without_the_scratch_marker()
    {
        var prover = A.Caller().Human().Client(ClientLabel.Prover).Build();
        await using var unmarked = new ChainHarness(prover: new ProverLabel(Allowed: true, MayConfirm: false));
        await using var default_ = new ChainHarness();
        unmarked.H.Profiles.Live = SliceHarness.Profile();
        default_.H.Profiles.Live = SliceHarness.Profile();
        _ = await unmarked.H.HoldLease();
        _ = await default_.H.HoldLease();

        Assert.Equal("SND-AUTH-002", await Code(unmarked, prover));
        Assert.Equal("SND-AUTH-002", await Code(default_, prover));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_prover_passes_in_a_home_with_the_scratch_marker()
    {
        await using var marked = new ChainHarness(prover: new ProverLabel(Allowed: true, MayConfirm: true));
        marked.H.Profiles.Live = SliceHarness.Profile();
        _ = await marked.H.HoldLease();

        Assert.Null(await Code(marked, A.Caller().Human().Client(ClientLabel.Prover).Build()));
    }
}
