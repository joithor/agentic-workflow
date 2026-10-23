using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands.Chain;

/// <summary>The real chain on ApproveProfile, an AppConfirm row (owner decision q93): the app and a session pass, no caller and a service don't, and the prover needs no marker.</summary>
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
    public async Task A_session_passes_and_a_caller_with_no_token_or_a_service_is_refused_before_the_handler()
    {
        await using var chain = new ChainHarness();
        chain.H.Profiles.Live = SliceHarness.Profile();
        _ = await chain.H.HoldLease();

        Assert.Null(await Code(chain, A.Caller().Session().Build()));
        Assert.Equal("SND-AUTH-002", await Code(chain, caller: null));
        Assert.Equal("SND-AUTH-002", await Code(chain, A.Caller().Service("jobs").Build()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_prover_passes_with_or_without_the_scratch_marker()
    {
        var prover = A.Caller().Human().Client(ClientLabel.Prover).Build();
        await using var unmarked = new ChainHarness(prover: new ProverLabel(Allowed: true, MayConfirm: false));
        unmarked.H.Profiles.Live = SliceHarness.Profile();
        _ = await unmarked.H.HoldLease();

        Assert.Null(await Code(unmarked, prover));
    }
}
