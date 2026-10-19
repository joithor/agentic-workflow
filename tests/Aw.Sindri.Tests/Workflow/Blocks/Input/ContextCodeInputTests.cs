using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Tests.ContextCalls;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;

namespace Aw.Sindri.Tests.Workflow.Blocks.Input;

/// <summary>'input.context.code' (scoping spec C3): the code lens read in process as the service, cut to the instance's repo.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextCodeInputTests
{
    internal sealed class StubCode(Func<Result<CodeView>> answer) : IContextCode
    {
        public List<(IReadOnlyList<string> Anchors, bool Draft, string? MapVersion, ContextScope Scope)> Calls { get; } = [];

        public ValueTask<Result<CodeView>> Code(IReadOnlyList<string> anchors, bool draft, string? mapVersion, ContextScope scope, CancellationToken cancellationToken)
        {
            Calls.Add((anchors, draft, mapVersion, scope));
            return ValueTask.FromResult(answer());
        }
    }

    internal static ContextCodeInput Input(StubCode code, bool profile = true) =>
        new(EngineProxy.Of(code), new ContextInputScopes(new ContextInputFixtures.Profiles(profile ? ContextInputFixtures.Profile() : null)), ContextInputFixtures.NoFacts(), ShippedComposition.Json);

    private static StepInputContext Context(RepoName? repo, params string[] anchors) => new(
        BlockId.From(ContextCodeInput.Id), BlockId.From("step.scope.gather"),
        new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["anchors"] = JsonSerializer.SerializeToElement(anchors) },
        new Dictionary<FactKey, JsonElement>(), repo);

    [Fact]
    public async Task Fill_reads_the_anchors_on_the_current_map_as_the_service_cut_to_the_instances_repo_and_returns_text_and_the_typed_view()
    {
        var code = new StubCode(() => ContextCodeVerbsTests.View());

        var filled = await Input(code).Fill(Context(ContextInputFixtures.Web, "feature:sharing"), TestContext.Current.CancellationToken);

        var (anchors, draft, mapVersion, scope) = Assert.Single(code.Calls);
        Assert.Equal(["feature:sharing"], anchors);
        Assert.False(draft);
        Assert.Null(mapVersion);
        Assert.Equal(ContextCallerKind.Service, scope.Caller.Kind);
        Assert.Equal("web", Assert.Single(scope.Repos).Name.Value);
        Assert.Contains("src/sharing/links.ts", filled.Value.Sections["code"], StringComparison.Ordinal);
        Assert.Equal(1, filled.Value.Result!.Value.GetProperty("files").GetArrayLength());
    }

    [Fact]
    public async Task An_engine_refusal_is_an_honest_section_with_no_typed_result()
    {
        var filled = await Input(new StubCode(() => ContextErrors.UnknownAnchor.With("no anchor resolved"))).Fill(Context(ContextInputFixtures.Web, "feature:zzz"), TestContext.Current.CancellationToken);

        Assert.Contains("SND-CONTEXT-003", filled.Value.Sections["code"], StringComparison.Ordinal);
        Assert.Null(filled.Value.Result);
    }

    [Fact]
    public async Task An_instance_without_a_repo_never_reaches_the_engine()
    {
        var code = new StubCode(() => ContextCodeVerbsTests.View());

        var filled = await Input(code).Fill(Context(repo: null, "feature:a"), TestContext.Current.CancellationToken);

        Assert.Empty(code.Calls);
        Assert.Contains("no repo", filled.Value.Sections["code"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_anchors_is_a_section()
    {
        var code = new StubCode(() => ContextCodeVerbsTests.View());

        var filled = await Input(code).Fill(Context(ContextInputFixtures.Web), TestContext.Current.CancellationToken);

        Assert.Empty(code.Calls);
        Assert.Contains("no anchors", filled.Value.Sections["code"], StringComparison.Ordinal);
    }
}
