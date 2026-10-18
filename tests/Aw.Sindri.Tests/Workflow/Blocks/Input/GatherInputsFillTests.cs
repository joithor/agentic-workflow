using System.Text.Json;
using Aw.Context.Core.Facade;
using Aw.Kernel.Testing;
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
        .AddScoped<IContextLenses>(static _ => new ContextLensesInputTests.StubLenses(a => ContextInputFixtures.SmallSet([.. a])))
        .AddScoped<IContextHistory>(static _ => new ContextHistoryInputTests.StubHistory(() => ContextHistoryTextTests.View()))
        .AddScoped<IContextCode>(static _ => new ContextCodeInputTests.StubCode(() => ContextCodeVerbsTests.View()))
        .AddSingleton<IApprovedProfile>(new ContextInputFixtures.Profiles(ContextInputFixtures.Profile()))
        .BuildServiceProvider();

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
        Assert.NotNull(filled.Value.Texts.Result);
        Assert.NotNull(filled.Value.Metadata.Result);
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
