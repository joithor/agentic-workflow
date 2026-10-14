using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Registry;
using Xunit;

namespace Aw.Sindri.Testing.Workflow.Registry;

/// <summary>
/// The contract every <see cref="IRecipeWake"/> honours with nothing running (engine spec §4.1, Phase 2): a hash no
/// instance runs finds none, and a wake succeeds. Run against the fake and the no-op adapter; P4's signal sender adds its own.
/// </summary>
public abstract class RecipeWakeContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A fresh wake with no instances running.</summary>
    /// <returns>The wake.</returns>
    protected abstract IRecipeWake Create();

    /// <summary>No instance runs a hash nothing started.</summary>
    [Fact]
    public async Task Running_for_a_hash_nothing_runs_is_empty()
    {
        var running = await Create().Running(RegistryData.Hash("idle"), Token);

        Expect.True(running.IsSuccess);
        Expect.Equal(0, running.Value.Count);
    }

    /// <summary>A wake for a hash nothing runs succeeds.</summary>
    [Fact]
    public async Task Wake_with_nothing_running_succeeds()
    {
        var woken = await Create().Wake(new RecipeWakeNotice(RegistryData.Hash("idle"), RecipeWakeKind.Revoked, 7, "human:cli"), Token);

        Expect.True(woken.IsSuccess);
    }
}
