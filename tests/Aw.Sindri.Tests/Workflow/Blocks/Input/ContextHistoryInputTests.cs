using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.History;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ContextCalls;

namespace Aw.Sindri.Tests.Workflow.Blocks.Input;

/// <summary>'input.context.history' (scoping spec C5): the history lens read in process as the service, cut to the instance's repo, with the running sync marked on a not-synced row.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextHistoryInputTests
{
    internal sealed class History(Func<Result<HistoryView>> answer) : IContextHistory
    {
        public List<(IReadOnlyList<string> Anchors, HistoryWindow Window, ContextScope Scope)> Calls { get; } = [];

        public ValueTask<Result<HistoryView>> History_(IReadOnlyList<string> anchors, HistoryWindow window, ContextScope scope)
        {
            Calls.Add((anchors, window, scope));
            return ValueTask.FromResult(answer());
        }

        public ValueTask<Result<HistoryView>> History(IReadOnlyList<string> anchors, HistoryWindow window, MapRead read, ContextScope scope, CancellationToken cancellationToken) => History_(anchors, window, scope);
    }

    internal static ContextHistoryInput Input(History history, InMemoryJobViews? jobs = null, bool profile = true) =>
        new(history, new ContextInputScopes(new ContextInputFixtures.Profiles(profile ? ContextInputFixtures.Profile() : null)), new SyncGaps(jobs ?? new InMemoryJobViews()), Aw.Sindri.Testing.Workflow.ShippedComposition.Json);

    private static StepInputContext Context(RepoName? repo, JsonElement? window, params string[] anchors)
    {
        var with = new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["anchors"] = JsonSerializer.SerializeToElement(anchors) };
        if (window is { } w)
        {
            with["window"] = w;
        }

        return new(BlockId.From(ContextHistoryInput.Id), BlockId.From("step.scope.gather"), with, new Dictionary<FactKey, JsonElement>(), repo);
    }

    [Fact]
    public async Task Fill_reads_the_window_as_the_service_cut_to_the_instances_repo_and_returns_text_and_the_typed_view()
    {
        var history = new History(() => ContextHistoryTextTests.View());
        var window = JsonSerializer.SerializeToElement(new { since = "2026-01-01", before = "2026-07-01" });

        var filled = await Input(history).Fill(Context(ContextInputFixtures.Web, window, "feature:layout.split-pane"), TestContext.Current.CancellationToken);

        var call = Assert.Single(history.Calls);
        Assert.Equal(["feature:layout.split-pane"], call.Anchors);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), call.Window.Since);
        Assert.Equal(ContextCallerKind.Service, call.Scope.Caller.Kind);
        Assert.Equal("web", Assert.Single(call.Scope.Repos).Name.Value);
        Assert.False(string.IsNullOrWhiteSpace(filled.Sections["history"]));
        Assert.Equal(JsonValueKind.Array, filled.Result!.Value.GetProperty("records").ValueKind);
    }

    [Fact]
    public async Task A_not_synced_row_is_marked_with_the_running_sync_job()
    {
        var jobs = new InMemoryJobViews();
        var id = JobId.From("JOB-0000000000000000000000000000000a");
        jobs.Put(new JobBuilder().WithId(id).OfKind(SyncGaps.SyncKind, JobKey.From("sync")).Build());

        var filled = await Input(new History(() => ContextHistoryTextTests.View()), jobs).Fill(Context(ContextInputFixtures.Web, window: null, "feature:layout.split-pane"), TestContext.Current.CancellationToken);

        Assert.Contains(id.Value, filled.Result!.Value.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_bad_window_is_a_section_and_never_reaches_the_engine()
    {
        var history = new History(() => ContextHistoryTextTests.View());

        var filled = await Input(history).Fill(Context(ContextInputFixtures.Web, JsonSerializer.SerializeToElement("90d"), "feature:a"), TestContext.Current.CancellationToken);

        Assert.Empty(history.Calls);
        Assert.Contains("SND-CONTEXT-006", filled.Sections["history"], StringComparison.Ordinal);
        Assert.Null(filled.Result);
    }

    [Fact]
    public async Task An_engine_refusal_is_an_honest_section_with_no_typed_result()
    {
        var filled = await Input(new History(() => ContextErrors.UnknownAnchor.With("no anchor resolved"))).Fill(Context(ContextInputFixtures.Web, window: null, "feature:zzz"), TestContext.Current.CancellationToken);

        Assert.Contains("SND-CONTEXT-003", filled.Sections["history"], StringComparison.Ordinal);
        Assert.Null(filled.Result);
    }

    [Fact]
    public async Task An_instance_without_a_repo_never_reaches_the_engine()
    {
        var history = new History(() => ContextHistoryTextTests.View());

        var filled = await Input(history).Fill(Context(repo: null, window: null, "feature:a"), TestContext.Current.CancellationToken);

        Assert.Empty(history.Calls);
        Assert.Contains("no repo", filled.Sections["history"], StringComparison.Ordinal);
    }
}
