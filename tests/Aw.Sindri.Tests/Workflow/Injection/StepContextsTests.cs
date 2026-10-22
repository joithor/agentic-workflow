using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using StepRun = Aw.Sindri.Infrastructure.Workflows.StepRun;

namespace Aw.Sindri.Tests.Workflow.Injection;

/// <summary>
/// The in-process Step's context (plan "How the text reaches the Step", W6): sections and typed results from the kept texts, a
/// refill on a miss that must match the recorded digests, and an empty, never null, InputResults.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class StepContextsTests
{
    private static readonly BlockId _step = BlockId.From("step.test");
    private static readonly BlockId _input = BlockId.From("input.test.a");
    private static readonly StepRun _run = new("T-1/s/1", "checks", "1.1");

    private static StepContext Seed => new(_step, new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<FactKey, JsonElement>(), InjectionSections.Empty);

    private static StepInputContext Context => new(_input, _step, new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<FactKey, JsonElement>());

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    private static FilledOutput Output(string text, string? result = null)
    {
        var resultEntry = result is null ? null : new InjectionResultEntry(result.Length, StepInputRunner.Sha(result));
        return new FilledOutput(
            new FilledInput(_input, 1, [new InjectionEntry("notes", _input, System.Text.Encoding.UTF8.GetByteCount(text), StepInputRunner.Sha(text))], resultEntry),
            new FilledTexts(new Dictionary<string, string>(StringComparer.Ordinal) { ["notes"] = text }, result is null ? null : Json(result)));
    }

    private static FilledOutput OutputAt(int index, string text, string result)
    {
        var entry = new InjectionEntry("notes", _input, System.Text.Encoding.UTF8.GetByteCount(text), StepInputRunner.Sha(text));
        return new FilledOutput(
            new FilledInput(_input, index, [entry], new InjectionResultEntry(result.Length, StepInputRunner.Sha(result))),
            new FilledTexts(new Dictionary<string, string>(StringComparer.Ordinal) { ["notes"] = text }, Json(result)));
    }

    private sealed class FakeRunner(Func<FilledOutput> fill, Func<int, FilledOutput>? fillAt = null) : IStepInputRunner
    {
        public int Calls { get; private set; }

        public ValueTask<Result<FilledOutput>> Fill(StepInputContext context, int index, CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult<Result<FilledOutput>>(fillAt is null ? fill() : fillAt(index));
        }
    }

    [Fact]
    public async Task StepContext_injection_has_the_text_for_an_IStep_runtime()
    {
        var texts = new MemoryInjectionTexts();
        var kept = Output("kept text", "{\"count\":2}");
        texts.Put(new InjectionKey(_run.Address, _run.State, _run.Token, 1), kept.Texts);
        var runner = new FakeRunner(() => throw new InvalidOperationException("a hit must not run the input"));

        var context = await new StepContexts(texts, runner).For(_run, Seed, [new InjectionInputSource(kept.Metadata, Context)], TestContext.Current.CancellationToken);

        Assert.Equal("kept text", context.Value.Injection.Sections["notes"]);
        Assert.Equal(2, context.Value.InputResults[_input.Value].GetProperty("count").GetInt32());
        Assert.Equal(0, runner.Calls);
    }

    [Fact]
    public async Task InputResults_is_empty_never_null_without_inputs_and_omits_an_input_with_no_result()
    {
        var texts = new MemoryInjectionTexts();
        var plain = Output("no result");
        texts.Put(new InjectionKey(_run.Address, _run.State, _run.Token, 1), plain.Texts);
        var contexts = new StepContexts(texts, new FakeRunner(() => plain));

        var none = await contexts.For(_run, Seed, [], TestContext.Current.CancellationToken);
        var without = await contexts.For(_run, Seed, [new InjectionInputSource(plain.Metadata, Context)], TestContext.Current.CancellationToken);

        Assert.Empty(none.Value.InputResults);
        Assert.Empty(without.Value.InputResults);
        Assert.NotNull(Seed.InputResults);
    }

    [Fact]
    public async Task The_same_input_twice_in_one_Step_fills_both_and_each_refills_against_its_own_text()
    {
        var texts = new MemoryInjectionTexts();
        var first = OutputAt(1, "first text", "{\"count\":1}");
        var second = OutputAt(2, "second text", "{\"count\":2}");
        var runner = new FakeRunner(() => throw new InvalidOperationException("filled by index below"), static index => index == 1 ? OutputAt(1, "first text", "{\"count\":1}") : OutputAt(2, "second text", "{\"count\":2}"));
        var sources = new[] { new InjectionInputSource(first.Metadata, Context), new InjectionInputSource(second.Metadata, Context) };

        var cold = await new StepContexts(texts, runner).For(_run, Seed, sources, TestContext.Current.CancellationToken);
        var warm = await new StepContexts(texts, new FakeRunner(() => throw new InvalidOperationException("a hit must not run the input"))).For(_run, Seed, sources, TestContext.Current.CancellationToken);

        Assert.True(cold.IsSuccess);
        Assert.Equal(2, runner.Calls);
        foreach (var context in new[] { cold.Value, warm.Value })
        {
            Assert.Equal(2, context.InputResults.Count);   // one result per entry: the second use does not overwrite the first
            Assert.Equal([1, 2], context.InputResults.OrderBy(static r => r.Key, StringComparer.Ordinal).Select(static r => r.Value.GetProperty("count").GetInt32()));
        }

        Assert.Equal("first text", Assert.IsType<FilledTexts>(texts.Get(new InjectionKey(_run.Address, _run.State, _run.Token, 1))).Sections["notes"]);
        Assert.Equal("second text", Assert.IsType<FilledTexts>(texts.Get(new InjectionKey(_run.Address, _run.State, _run.Token, 2))).Sections["notes"]);
    }

    [Fact]
    public async Task A_miss_refills_and_serves_when_the_digests_match()
    {
        var texts = new MemoryInjectionTexts();
        var recorded = Output("same text", "{\"count\":2}");
        var runner = new FakeRunner(() => Output("same text", "{\"count\":2}"));

        var context = await new StepContexts(texts, runner).For(_run, Seed, [new InjectionInputSource(recorded.Metadata, Context)], TestContext.Current.CancellationToken);

        Assert.Equal("same text", context.Value.Injection.Sections["notes"]);
        Assert.Equal(1, runner.Calls);
        Assert.NotNull(texts.Get(new InjectionKey(_run.Address, _run.State, _run.Token, 1)));
    }

    [Fact]
    public async Task A_miss_that_refills_differently_is_SND_WF_018_and_keeps_nothing()
    {
        var texts = new MemoryInjectionTexts();
        var recorded = Output("recorded text");
        var runner = new FakeRunner(() => Output("other text"));

        var context = await new StepContexts(texts, runner).For(_run, Seed, [new InjectionInputSource(recorded.Metadata, Context)], TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowErrors.InputChanged.Code, context.Error.Code);
        Assert.DoesNotContain("other text", context.Error.Message, StringComparison.Ordinal);
        Assert.Null(texts.Get(new InjectionKey(_run.Address, _run.State, _run.Token, 1)));
    }

    [Fact]
    public async Task A_typed_result_that_differs_is_SND_WF_018_too()
    {
        var recorded = Output("same text", "{\"count\":2}");
        var runner = new FakeRunner(() => Output("same text", "{\"count\":3}"));

        var context = await new StepContexts(new MemoryInjectionTexts(), runner).For(_run, Seed, [new InjectionInputSource(recorded.Metadata, Context)], TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowErrors.InputChanged.Code, context.Error.Code);
    }
}
