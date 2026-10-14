using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Testing;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows.Effects;

/// <summary>
/// The recheck after the dispatch delay (engine spec rev 8.1 §5.7, §3.8, §4.3 W11, §10; engine P6 unit (b)), on the
/// time-skipping server with the shipped 'RecheckEffect': it reads the recipe's approval now and the approved profile's
/// 'workflow.effects' (an <see cref="InMemoryProfiles"/> stands in for the approved snapshot until unit (d) lands the
/// keys). A refusal writes 'outbox.refused' with its code and parks the instance with spec-conflict of that kind.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class EffectRecheckDispatchTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private static readonly TimeSpan _delay = EffectSettings.DefaultDispatchDelay;
    private static readonly BlockId _notify = BlockId.From("effect.notify.desk");
    private readonly WorkflowsHarness _harness = new();
    private readonly FakeDeskEffect _desk = new();
    private readonly InMemoryProfiles _profiles = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Revoked_effect_is_refused_at_dispatch_and_the_instance_parks_revoked_with_only_cancel()
    {
        var env = server.Environment;
        var recipe = _harness.Approve(WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        await Allow(_notify);
        await using var sindri = Sindri();
        await WorkflowsHarness.HoldLease(sindri, Token);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await Completed(sindri, "T-91");
            await Armed(handle, timers: 1);
            _harness.Revoke(recipe);
            await env.DelayAsync(_delay + TimeSpan.FromSeconds(1), Token);
            var refused = Assert.Single(await EffectRows.Eventually(sindri, OutboxEvents.Refused, CommandOrigin.Recipe));
            var parked = await WorkflowsHarness.Until(handle, static v => v.State == "parked");

            Assert.Equal(("SND-RECIPE-005", EffectState.Refused), (refused.Code, refused.State));
            Assert.Equal("spec-conflict (revoked)", parked.Detail);
            _ = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(() => handle.ExecuteUpdateAsync(wf => wf.AnswerAsync(Resume('e')), new WorkflowUpdateOptions(new string('e', 32))));
            var cancelled = await handle.ExecuteUpdateAsync(wf => wf.CancelAsync(Cancel()), new WorkflowUpdateOptions(new string('c', 32)));
            Assert.Equal("cancelled", (await handle.GetResultAsync()).State);
            Assert.Equal("cancelled", cancelled.Instance.State);
            Assert.Empty(await EffectRows.Of(sindri, OutboxEvents.Dispatched, CommandOrigin.Recipe));
            Assert.Empty(_desk.RecipeCalls);
        }, Token);
    }

    [Fact]
    public async Task An_effect_off_the_allowlist_parks_dispatch_and_resume_after_allowlisting_reemits_it()
    {
        var env = server.Environment;
        _harness.Recipe("workflow.dev-notify", WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        await Allow();
        await using var sindri = Sindri();
        await WorkflowsHarness.HoldLease(sindri, Token);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await Completed(sindri, "T-92");
            await Armed(handle, timers: 1);
            await env.DelayAsync(_delay + TimeSpan.FromSeconds(1), Token);
            var refused = Assert.Single(await EffectRows.Eventually(sindri, OutboxEvents.Refused, CommandOrigin.Recipe));
            var parked = await WorkflowsHarness.Until(handle, static v => v.State == "parked");
            Assert.Equal(("SND-WF-011", "spec-conflict (dispatch)"), (refused.Code, parked.Detail));
            Assert.Empty(_desk.RecipeCalls);

            await Allow(_notify);
            var resumed = await handle.ExecuteUpdateAsync(wf => wf.AnswerAsync(Resume('b')), new WorkflowUpdateOptions(new string('b', 32)));
            await Armed(handle, timers: 2);
            await env.DelayAsync(_delay + TimeSpan.FromSeconds(1), Token);
            var dispatched = Assert.Single(await EffectRows.Eventually(sindri, OutboxEvents.Dispatched, CommandOrigin.Recipe));
            var pending = await EffectRows.Of(sindri, OutboxEvents.Pending, CommandOrigin.Recipe);

            Assert.Equal("sent", resumed.Instance.State);
            Assert.Equal(2, pending.Count);
            Assert.Equal(refused.Identity, dispatched.Identity);
            Assert.All(pending, p => Assert.Equal(refused.Identity, p.Identity));
            Assert.NotEqual(pending[0].Command, pending[1].Command, StringComparer.Ordinal);
            Assert.Equal(pending[1].Command, dispatched.Command);
            Assert.Equal([refused.Identity], _desk.RecipePerformed);
            await handle.TerminateAsync("test over");
        }, Token);
    }

    [Fact]
    public async Task An_effect_whose_setup_cannot_run_parks_dispatch_naming_the_cause_without_retrying_forever()
    {
        var env = server.Environment;
        _harness.Recipe("workflow.dev-notify", WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        await Allow(_notify);
        _desk.Failure = Aw.Sindri.Core.Workflow.WorkflowErrors.NoRuntime.With("/repos/dev has no commit to point refs/sindri/me/T-95 at");
        await using var sindri = Sindri();
        await WorkflowsHarness.HoldLease(sindri, Token);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await Completed(sindri, "T-95");
            await Armed(handle, timers: 1);
            await env.DelayAsync(_delay + TimeSpan.FromSeconds(1), Token);
            var refused = Assert.Single(await EffectRows.Eventually(sindri, OutboxEvents.Refused, CommandOrigin.Recipe));
            var parked = await WorkflowsHarness.Until(handle, static v => v.State == "parked");
            await env.DelayAsync(TimeSpan.FromMinutes(5), Token);

            Assert.Equal(("SND-WF-010", "spec-conflict (dispatch)"), (refused.Code, parked.Detail));
            Assert.Contains("has no commit", refused.Reason, StringComparison.Ordinal);
            Assert.Contains("has no commit", parked.Question?.Question, StringComparison.Ordinal);
            _ = Assert.Single(_desk.RecipeCalls);   // a non-retryable failure: one try, not the retry policy's
            await handle.TerminateAsync("test over");
        }, Token);
    }

    [Fact]
    public async Task A_drifted_recipes_effect_is_refused_and_the_instance_parks_drift()
    {
        var env = server.Environment;
        var recipe = _harness.Approve(WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        await Allow(_notify);
        await using var sindri = Sindri();
        await WorkflowsHarness.HoldLease(sindri, Token);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await Completed(sindri, "T-93");
            await Armed(handle, timers: 1);
            await Drift(sindri, recipe);
            await env.DelayAsync(_delay + TimeSpan.FromSeconds(1), Token);
            var refused = Assert.Single(await EffectRows.Eventually(sindri, OutboxEvents.Refused, CommandOrigin.Recipe));
            var parked = await WorkflowsHarness.Until(handle, static v => v.State == "parked");

            Assert.Equal(("SND-RECIPE-006", "spec-conflict (drift)"), (refused.Code, parked.Detail));
            Assert.Empty(_desk.RecipeCalls);
            await handle.TerminateAsync("test over");
        }, Token);
    }

    /// <summary>The drift approval's wake reaches the parked instance through Temporal: the park ends and the refused effect is sent again (§3.8, §4.1).</summary>
    [Fact]
    public async Task A_drift_reapproval_wakes_the_parked_instance_and_resends_the_refused_effect()
    {
        var env = server.Environment;
        var recipe = _harness.Approve(WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        await Allow(_notify);
        await using var sindri = Sindri();
        await WorkflowsHarness.HoldLease(sindri, Token);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            _ = await WorkflowsHarness.AsServe(sindri, env, Token);
            var handle = await Completed(sindri, "T-96");
            await Armed(handle, timers: 1);
            var original = await Pins(sindri, recipe);
            await Drift(sindri, recipe);
            await env.DelayAsync(_delay + TimeSpan.FromSeconds(1), Token);
            var refused = Assert.Single(await EffectRows.Eventually(sindri, OutboxEvents.Refused, CommandOrigin.Recipe));
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "parked");

            await Pins(sindri, recipe, original);   // the re-approval: the pins match this build again
            var woken = await sindri.GetRequiredService<IRecipeWake>().Wake(new RecipeWakeNotice(recipe.Hash, RecipeWakeKind.Approved, Sequence: 99, "human:cli"), Token);
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "sent");
            await Armed(handle, timers: 2);
            await env.DelayAsync(_delay + TimeSpan.FromSeconds(1), Token);
            var dispatched = Assert.Single(await EffectRows.Eventually(sindri, OutboxEvents.Dispatched, CommandOrigin.Recipe));

            Assert.True(woken.IsSuccess);
            Assert.Equal(refused.Identity, dispatched.Identity);
            Assert.Equal([refused.Identity], _desk.RecipePerformed);
            await handle.TerminateAsync("test over");
        }, Token);
    }

    /// <summary>The pins of the latest approval of <paramref name="recipe"/>; with <paramref name="set"/>, a newer approval carrying those pins.</summary>
    private static async Task<RecipePins> Pins(IServiceProvider sindri, RegisteredRecipe recipe, RecipePins? set = null)
    {
        var approvals = sindri.GetRequiredService<IRecipeApprovals>();
        var latest = (await approvals.Latest(recipe.Id, recipe.Version, recipe.Hash, Token)).Value!;
        if (set is { } pins)
        {
            var approved = await approvals.Approve(latest with { Pins = pins, ApprovedAt = latest.ApprovedAt.AddMinutes(1) }, Token);
            Assert.False(approved.IsError, approved.IsError ? approved.Error.Message : null);
        }

        return latest.Pins;
    }

    [Fact]
    public async Task The_engines_desk_notice_is_never_refused_even_on_a_revoked_recipe_without_an_allowlist()
    {
        var recipe = _harness.Approve(WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        _harness.Revoke(recipe);
        await using var sindri = Sindri();
        var version = new RecipeVersion(recipe.Id, recipe.Version, recipe.Hash);
        var local = new ActivityEnvironment { Info = ActivityEnvironment.DefaultInfo with { IsLocal = true } };
        var activities = sindri.GetRequiredService<EffectActivities>();

        var engine = await local.RunAsync(() => activities.RecheckAsync(new EffectRecheck(version, _notify, CommandOrigin.Engine)));
        var fromRecipe = await local.RunAsync(() => activities.RecheckAsync(new EffectRecheck(version, _notify, CommandOrigin.Recipe)));

        Assert.Null(engine.Code);
        Assert.Equal("SND-RECIPE-005", fromRecipe.Code);
    }

    /// <summary>The host over the harness home, with the fake desk block and <see cref="_profiles"/> as the approved profile.</summary>
    private ServiceProvider Sindri() => _harness.Sindri(
        services =>
        {
            _desk.Replace(services);
            _ = services.AddSingleton<IApprovedProfile>(_profiles);
        },
        name => name == "AW_SINDRI_TEMPORAL_NAMESPACE" ? server.Environment.Client.Options.Namespace : null);   // the registry's wake signals in the server's namespace

    /// <summary>Approves a profile whose 'workflow.effects' lists <paramref name="kinds"/> (none: no allowlist at all).</summary>
    private async Task Allow(params BlockId[] kinds)
    {
        var workflow = kinds.Length == 0 ? null : new WorkflowSettings(Effects: [.. kinds.Select(static k => k.Value)]);
        var profile = new SindriProfile(SindriProfile.CurrentSchemaVersion, "tester", Workflow: workflow);
        var saved = await _profiles.Save(new LoadedProfile(profile, ProfileHash.From(new string((char)('a' + kinds.Length), 64)), "/home/profile.yaml"), Token);
        Assert.False(saved.IsError);
    }

    /// <summary>A drift approval of <paramref name="recipe"/>'s hash whose block pins no longer match this build (§4.1).</summary>
    private static async Task Drift(IServiceProvider sindri, RegisteredRecipe recipe)
    {
        var approvals = sindri.GetRequiredService<IRecipeApprovals>();
        var latest = (await approvals.Latest(recipe.Id, recipe.Version, recipe.Hash, Token)).Value!; // why: the harness approved it
        var stale = new BlockPin(_notify, DescriptorHash.From("sha256:" + new string('0', 64)));
        var pins = latest.Pins with { Blocks = [.. latest.Pins.Blocks.Select(p => p.Block == _notify ? stale : p)] };
        var drifted = await approvals.Approve(latest with { Pins = pins, ApprovedAt = latest.ApprovedAt.AddMinutes(1) }, Token);
        Assert.False(drifted.IsError, drifted.IsError ? drifted.Error.Message : null);
    }

    /// <summary>Starts 'workflow.dev-notify' on <paramref name="item"/> and completes its desk Step: 'ask -> sent' with 'effect.notify.desk'.</summary>
    private async Task<WorkflowHandle<RecipeWorkflow, RecipeResult>> Completed(IServiceProvider sindri, string item)
    {
        var handle = await WorkflowsHarness.Start(server.Environment.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.dev-notify", item, Token));
        _ = await WorkflowsHarness.Until(handle, static v => v.Task is not null);
        var report = new StepCompleted("human:cli", RunToken.From("1.1"), OutcomeName.From("done"),
            new Dictionary<FactKey, FactValue> { [FactKey.From("fact:output")] = _harness.Fact("Hello.") }, Tokens: 0, StepRuntime.Desk, new string('f', 64));
        var completed = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(report), new WorkflowUpdateOptions(new string('9', 32)));
        Assert.Equal("-> sent", completed.Happened);
        return handle;
    }

    /// <summary>Waits until the instance's history has <paramref name="timers"/> dispatch-delay timers (each follows a pending row).</summary>
    private static async Task Armed(WorkflowHandle handle, int timers)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            var started = (await handle.FetchHistoryAsync()).Events
                .Count(static e => e.EventType == EventType.TimerStarted && e.TimerStartedEventAttributes.StartToFireTimeout.ToTimeSpan() == _delay);
            if (started >= timers)
            {
                return;
            }

            await Task.Delay(100, Token);
        }

        Assert.Fail(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the dispatch delay's timer {timers} was never started"));
    }

    private static AnswerGiven Resume(char nonce) =>
        new("human:cli", new CommandStamp(CommandNonce.From(new string(nonce, 32)), new string('0', 64)), AnswerVariant.Resume, Text: null, Amount: null, Forwarded: null);

    private static CancelRequested Cancel() =>
        new("human:cli", CancelOrigin.Command, new CommandStamp(CommandNonce.From(new string('c', 32)), new string('0', 64)), Sequence: null, Parent: null);
}
