using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.History;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ContextCalls;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;

namespace Aw.Sindri.Tests.Workflow.Blocks.Input;

/// <summary>'input.context.history' (scoping spec C5): the history lens read in process as the service, cut to the instance's repo, with the running sync marked on a not-synced row.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextHistoryInputTests
{
    internal sealed class StubHistory(Func<Result<HistoryView>> answer) : IContextHistory
    {
        public List<(IReadOnlyList<string> Anchors, HistoryWindow Window, ContextScope Scope)> Calls { get; } = [];

        public ValueTask<Result<HistoryView>> Record(IReadOnlyList<string> anchors, HistoryWindow window, ContextScope scope)
        {
            Calls.Add((anchors, window, scope));
            return ValueTask.FromResult(answer());
        }

        public ValueTask<Result<HistoryView>> History(IReadOnlyList<string> anchors, HistoryWindow window, MapRead read, ContextScope scope, CancellationToken cancellationToken) => Record(anchors, window, scope);
    }

    internal static ContextHistoryInput Input(StubHistory history, bool profile = true) =>
        new(EngineProxy.Of(history), new ContextInputScopes(new ContextInputFixtures.Profiles(profile ? ContextInputFixtures.Profile() : null)), ShippedComposition.Json);

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
        var history = new StubHistory(() => ContextHistoryTextTests.View());
        var request = JsonSerializer.SerializeToElement(new { since = "2026-01-01", before = "2026-07-01" });

        var filled = await Input(history).Fill(Context(ContextInputFixtures.Web, request, "feature:layout.split-pane"), TestContext.Current.CancellationToken);

        var (anchors, window, scope) = Assert.Single(history.Calls);
        Assert.Equal(["feature:layout.split-pane"], anchors);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), window.Since);
        Assert.Equal(ContextCallerKind.Service, scope.Caller.Kind);
        Assert.Equal("web", Assert.Single(scope.Repos).Name.Value);
        Assert.False(string.IsNullOrWhiteSpace(filled.Sections["history"]));
        Assert.Equal(JsonValueKind.Array, filled.Result!.Value.GetProperty("records").ValueKind);
    }

    [Fact]
    public async Task A_bad_window_is_a_section_and_never_reaches_the_engine()
    {
        var history = new StubHistory(() => ContextHistoryTextTests.View());

        var filled = await Input(history).Fill(Context(ContextInputFixtures.Web, JsonSerializer.SerializeToElement("90d"), "feature:a"), TestContext.Current.CancellationToken);

        Assert.Empty(history.Calls);
        Assert.Contains("SND-CONTEXT-006", filled.Sections["history"], StringComparison.Ordinal);
        Assert.Null(filled.Result);
    }

    [Theory]
    [InlineData("[1]")]
    [InlineData("7")]
    [InlineData("true")]
    public async Task A_window_of_another_shape_is_a_section_naming_the_argument_and_never_reaches_the_engine(string json)
    {
        var history = new StubHistory(() => ContextHistoryTextTests.View());

        var filled = await Input(history).Fill(Context(ContextInputFixtures.Web, JsonDocument.Parse(json).RootElement.Clone(), "feature:a"), TestContext.Current.CancellationToken);

        Assert.Empty(history.Calls);
        Assert.Contains("window argument", filled.Sections["history"], StringComparison.Ordinal);
        Assert.Null(filled.Result);
    }

    [Fact]
    public async Task Two_fills_of_the_same_stored_state_at_different_clocks_give_identical_bytes()
    {
        var first = await Input(new StubHistory(() => Later(ContextHistoryTextTests.View(), days: 3))).Fill(Context(ContextInputFixtures.Web, window: null, "feature:layout.split-pane"), TestContext.Current.CancellationToken);
        var second = await Input(new StubHistory(() => Later(ContextHistoryTextTests.View(), days: 40))).Fill(Context(ContextInputFixtures.Web, window: null, "feature:layout.split-pane"), TestContext.Current.CancellationToken);

        Assert.Equal(first.Sections["history"], second.Sections["history"]);
        Assert.Equal(first.Result!.Value.GetRawText(), second.Result!.Value.GetRawText());
    }

    private static HistoryView Later(HistoryView view, int days) => view with
    {
        AsOf = view.AsOf.AddDays(days),
        Coverage = view.Coverage with { Map = new MapCoverage([], Truncated: false, 1, 0, 0, view.AsOf.AddDays(-10), 10 + days) },
        Searched = view.Searched with { Sources = [.. view.Searched.Sources.Select(s => s with { AsOf = view.AsOf.AddDays(days) })] },
    };

    [Fact]
    public async Task An_engine_refusal_is_an_honest_section_with_no_typed_result()
    {
        var filled = await Input(new StubHistory(() => ContextErrors.UnknownAnchor.With("no anchor resolved"))).Fill(Context(ContextInputFixtures.Web, window: null, "feature:zzz"), TestContext.Current.CancellationToken);

        Assert.Contains("SND-CONTEXT-003", filled.Sections["history"], StringComparison.Ordinal);
        Assert.Null(filled.Result);
    }

    [Fact]
    public async Task An_instance_without_a_repo_never_reaches_the_engine()
    {
        var history = new StubHistory(() => ContextHistoryTextTests.View());

        var filled = await Input(history).Fill(Context(repo: null, window: null, "feature:a"), TestContext.Current.CancellationToken);

        Assert.Empty(history.Calls);
        Assert.Contains("no repo", filled.Sections["history"], StringComparison.Ordinal);
    }
}
