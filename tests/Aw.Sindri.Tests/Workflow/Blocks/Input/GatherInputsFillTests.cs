using System.Text.Json;
using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.History;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Context.Core.Facade;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.ContextCalls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.Blocks.Input;

/// <summary>
/// The scoping recipe's gather Step names all three context inputs. Through the shipped composition and the real runner, each one fills its
/// section and its typed result for the instance's repo (the facades are the only stubs), so the Step sees what the recipe promises.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class GatherInputsFillTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aw-gather-inputs-" + RealTime.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private ServiceProvider Compose() => Composition.Compose(new ServiceCollection(), name => name == "AW_STATE_DIR" ? _root : null, new FakeTimeProvider())
        .AddScoped<IContextEngine>(static _ => EngineProxy.Of(new Combined()))
        .AddSingleton<IApprovedProfile>(new ContextInputFixtures.Profiles(ContextInputFixtures.Profile()))
        .BuildServiceProvider();

    /// <summary>One stub answering all three lenses.</summary>
    private sealed class Combined : IContextLenses, IContextHistory, IContextCode
    {
        private readonly ContextLensesInputTests.StubLenses _lenses = new(a => ContextInputFixtures.SmallSet([.. a]));
        private readonly ContextHistoryInputTests.StubHistory _history = new(() => ContextHistoryTextTests.View());
        private readonly ContextCodeInputTests.StubCode _code = new(() => ContextCodeVerbsTests.View());

        public ValueTask<Result<LensesSetView>> LensesOf(IReadOnlyList<string> anchors, MapRead read, LensRange range, ContextScope scope, CancellationToken cancellationToken) => _lenses.LensesOf(anchors, read, range, scope, cancellationToken);

        public ValueTask<Result<LensesSetView>> LensesOf(IReadOnlyList<string> anchors, MapRead read, int? depth, ContextScope scope, CancellationToken cancellationToken) => _lenses.LensesOf(anchors, read, depth, scope, cancellationToken);

        public ValueTask<Result<LensesView>> Lenses(string anchor, bool draft, int? depth, ContextScope scope, CancellationToken cancellationToken) => _lenses.Lenses(anchor, draft, depth, scope, cancellationToken);

        public ValueTask<Result<LensesView>> Lenses(string anchor, MapRead read, int? depth, ContextScope scope, CancellationToken cancellationToken) => _lenses.Lenses(anchor, read, depth, scope, cancellationToken);

        public ValueTask<Result<LensesView>> Lenses(string anchor, MapRead read, LensRange range, ContextScope scope, CancellationToken cancellationToken) => _lenses.Lenses(anchor, read, range, scope, cancellationToken);

        public ValueTask<Result<FileMapView>> FileMap(FileMapQuery query, ContextScope scope, CancellationToken cancellationToken) => _lenses.FileMap(query, scope, cancellationToken);

        public ValueTask<Result<HistoryView>> History(IReadOnlyList<string> anchors, HistoryWindow window, MapRead read, ContextScope scope, CancellationToken cancellationToken) => _history.History(anchors, window, read, scope, cancellationToken);

        public ValueTask<Result<CodeView>> Code(IReadOnlyList<string> anchors, bool draft, string? mapVersion, ContextScope scope, CancellationToken cancellationToken) => _code.Code(anchors, draft, mapVersion, scope, cancellationToken);
    }

    private static readonly string[] _anchors = ["feature:sharing"];

    private static StepInputContext Context(string input) => new(
        BlockId.From(input), BlockId.From("step.scope.gather"),
        new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["anchors"] = JsonSerializer.SerializeToElement(_anchors) },
        new Dictionary<FactKey, JsonElement>(), ContextInputFixtures.Web);

    [Theory]
    [InlineData("input.context.lenses", "lenses")]
    [InlineData("input.context.history", "history")]
    [InlineData("input.context.code", "code")]
    public async Task Each_gather_input_fills_its_section_and_typed_result_for_the_instances_repo(string input, string section)
    {
        await using var provider = Compose();
        var runner = provider.GetRequiredService<IStepInputRunner>();

        var filled = await runner.Fill(Context(input), 1, TestContext.Current.CancellationToken);

        Assert.False(filled.IsError, filled.IsError ? filled.Error.Message : string.Empty);
        Assert.False(string.IsNullOrWhiteSpace(filled.Value.Texts.Sections[section]));
        Assert.True(filled.Value.Texts.Result.HasValue);
        Assert.True(filled.Value.Metadata.Result is not null);
    }

    [Fact]
    public async Task The_shipped_runtimes_cover_every_input_of_the_scoping_recipe()
    {
        await using var provider = Compose();

        var runtimes = provider.GetRequiredService<StepInputRuntimes>();

        Assert.True(runtimes.Has(BlockId.From("input.context.lenses")));
        Assert.True(runtimes.Has(BlockId.From("input.context.history")));
        Assert.True(runtimes.Has(BlockId.From("input.context.code")));
    }
}
