using Aw.Evidence.Core.Scripts;
using Aw.Evidence.Core.Steps;
using Aw.Evidence.Core.Visual;
using Aw.Evidence.Features.RunScript;
using Aw.Evidence.Testing;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Evidence.Tests.RunScript;

/// <summary>The run slice over in-memory fakes: one shared page, the script at the builder's default path.</summary>
internal sealed class RunScriptHarness
{
    public RunScriptHarness()
    {
        Browser.PageFor = _ => Page;
    }

    public FakePage Page { get; } = new();

    public FakeBrowser Browser { get; } = new();

    public InMemoryRunFiles Files { get; } = new();

    public InMemoryMachineLocks Locks { get; } = new();

    public InMemoryVerdictCacheStore Cache { get; } = new();

    public FakeImageCodec Codec { get; } = new();

    public FakeJudge Judge { get; set; } = FakeJudge.Unavailable;

    public Dictionary<string, string> Settings { get; } = new(StringComparer.Ordinal);

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));

    public byte[] WithScript(UiScript script)
    {
        var bytes = AwJson.Serialize(script, TestJson.Options);
        _ = Files.With(RunScriptRequestBuilder.ScriptPath, bytes);
        return bytes;
    }

    public ValueTask<Kernel.Result<RunScriptResponse>> Run(RunScriptRequest request)
    {
        var json = TestJson.Options;
        var settings = new FakeSecrets(Settings);
        var run = new ScriptRun(Browser, new StepRunner(Judge, Clock), Files, settings);
        var verdict = new RunVerdict(new VisualGate(Files, Codec, Judge, Clock, json), Cache, settings, json);
        var service = new ValidatingService<RunScriptRequest, RunScriptResponse>(
            new RunScriptService(new ScriptFiles(Files, json), Locks, run, verdict, Clock), new RunScriptValidator());
        return service.Handle(request, CancellationToken.None);
    }
}
