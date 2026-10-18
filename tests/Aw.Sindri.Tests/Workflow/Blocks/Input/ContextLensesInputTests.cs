using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Input;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;

namespace Aw.Sindri.Tests.Workflow.Blocks.Input;

/// <summary>'input.context.lenses' (scoping spec C1): the multi-anchor lenses read in process as the service, cut to the instance's repo.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextLensesInputTests
{
    internal sealed class Lenses(Func<IReadOnlyList<string>, Result<LensesSetView>> answer) : IContextLenses
    {
        public List<(IReadOnlyList<string> Anchors, LensRange Range, ContextScope Scope)> Calls { get; } = [];

        public ValueTask<Result<LensesSetView>> LensesOf(IReadOnlyList<string> anchors, MapRead read, LensRange range, ContextScope scope, CancellationToken cancellationToken)
        {
            Calls.Add((anchors, range, scope));
            return ValueTask.FromResult(answer(anchors));
        }

        public ValueTask<Result<LensesSetView>> LensesOf(IReadOnlyList<string> anchors, MapRead read, int? depth, ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<LensesView>> Lenses(string anchor, bool draft, int? depth, ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<LensesView>> Lenses(string anchor, MapRead read, int? depth, ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<LensesView>> Lenses(string anchor, MapRead read, LensRange range, ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<FileMapView>> FileMap(FileMapQuery query, ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    internal static ContextLensesInput Input(Lenses lenses, bool profile = true) =>
        new(lenses, new ContextInputScopes(new ContextInputFixtures.Profiles(profile ? ContextInputFixtures.Profile() : null)), Aw.Sindri.Testing.Workflow.ShippedComposition.Json);

    private static StepInputContext Context(RepoName? repo, params string[] anchors) => new(
        BlockId.From(ContextLensesInput.Id), BlockId.From("step.scope.gather"),
        new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["anchors"] = JsonSerializer.SerializeToElement(anchors),
            ["depth"] = JsonSerializer.SerializeToElement(2),
            ["hops"] = JsonSerializer.SerializeToElement(new[] { "state", "flow" }),
        },
        new Dictionary<FactKey, JsonElement>(), repo);

    [Fact]
    public async Task Fill_reads_the_anchor_set_as_the_service_cut_to_the_instances_repo_and_returns_text_and_the_typed_view()
    {
        var lenses = new Lenses(a => ContextInputFixtures.SmallSet([.. a]));

        var filled = await Input(lenses).Fill(Context(ContextInputFixtures.Web, "feature:a", "feature:b"), TestContext.Current.CancellationToken);

        var call = Assert.Single(lenses.Calls);
        Assert.Equal(["feature:a", "feature:b"], call.Anchors);
        Assert.Equal(2, call.Range.Depth);
        Assert.Equal("state,flow", call.Range.Hops);
        Assert.Equal(ContextCallerKind.Service, call.Scope.Caller.Kind);
        Assert.Equal("web", Assert.Single(call.Scope.Repos).Name.Value);
        Assert.Contains("lenses of 2 anchors", filled.Sections["lenses"], StringComparison.Ordinal);
        Assert.Equal(2, filled.Result!.Value.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(2, filled.Result.Value.GetProperty("anchors").GetArrayLength());
    }

    [Fact]
    public async Task An_engine_refusal_is_an_honest_section_with_no_typed_result()
    {
        var lenses = new Lenses(_ => ContextErrors.UnknownAnchor.With("no anchor resolved"));

        var filled = await Input(lenses).Fill(Context(ContextInputFixtures.Web, "feature:zzz"), TestContext.Current.CancellationToken);

        Assert.Contains("unavailable", filled.Sections["lenses"], StringComparison.Ordinal);
        Assert.Contains("SND-CONTEXT-003", filled.Sections["lenses"], StringComparison.Ordinal);
        Assert.Null(filled.Result);
    }

    [Fact]
    public async Task An_instance_without_a_repo_never_reaches_the_engine()
    {
        var lenses = new Lenses(a => ContextInputFixtures.SmallSet([.. a]));

        var filled = await Input(lenses).Fill(Context(repo: null, "feature:a"), TestContext.Current.CancellationToken);

        Assert.Empty(lenses.Calls);
        Assert.Contains("no repo", filled.Sections["lenses"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_repo_outside_the_profile_never_reaches_the_engine()
    {
        var lenses = new Lenses(a => ContextInputFixtures.SmallSet([.. a]));

        var filled = await Input(lenses).Fill(Context(RepoName.From("other"), "feature:a"), TestContext.Current.CancellationToken);

        Assert.Empty(lenses.Calls);
        Assert.Contains("SND-CONTEXT-045", filled.Sections["lenses"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_approved_profile_is_a_section_not_an_exception()
    {
        var lenses = new Lenses(a => ContextInputFixtures.SmallSet([.. a]));

        var filled = await Input(lenses, profile: false).Fill(Context(ContextInputFixtures.Web, "feature:a"), TestContext.Current.CancellationToken);

        Assert.Empty(lenses.Calls);
        Assert.Contains("SND-PROFILE-005", filled.Sections["lenses"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_anchors_is_a_section()
    {
        var lenses = new Lenses(a => ContextInputFixtures.SmallSet([.. a]));

        var filled = await Input(lenses).Fill(Context(ContextInputFixtures.Web), TestContext.Current.CancellationToken);

        Assert.Empty(lenses.Calls);
        Assert.Contains("no anchors", filled.Sections["lenses"], StringComparison.Ordinal);
    }
}
