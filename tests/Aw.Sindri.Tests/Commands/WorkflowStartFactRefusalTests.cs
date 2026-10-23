using System.Net;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri workflow start' with a fact the start refuses (a 'fact:scopeInput' without its repo): the refusal's message reaches standard error and the exit is 2.</summary>
public sealed class WorkflowStartFactRefusalTests : IDisposable
{
    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TempFile _fact = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
        _fact.Dispose();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_refused_fact_is_exit_2_with_the_refusal_on_standard_error()
    {
        await File.WriteAllTextAsync(_fact.Path, """{"kind":"brief","path":"briefs/expiry.md"}""", TestContext.Current.CancellationToken);
        var refusal = """{"type":"about:blank","title":"SND-CLI-003","status":400,"detail":"fact:scopeInput (scope.json) is not valid for workflow.scoping: /repo: required (fix: write fact:scopeInput as one of the shapes in schemas/scoping/scope-input.json)"}""";
        var api = new RoutedApi().OnRaw("/sindri/workflow/instances", HttpStatusCode.BadRequest, refusal);
        var client = new ControlPlaneClient(_probe, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Signal: null, Terminal: true, Prover: false), new InMemoryHumanTokenStore(), _ => ValueTask.FromResult((RepoName?)null)), handler: api);
        var desk = new RecipeDesk(client, TextReader.Null, static _ => Task.FromResult<Kernel.Error?>(null));
        await using var services = new ServiceCollection().AddSingleton(TimeProvider.System).BuildServiceProvider();

        var code = await WorkflowStartCommand.Run(["workflow.scoping", "--item", "T-1", "--fact", "fact:scopeInput=" + _fact.Path], desk, services, new Output(_out, _error, SliceHarness.Json), TestContext.Current.CancellationToken);

        Assert.Equal(2, code);
        Assert.Contains("fact:scopeInput", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("/repo: required", _error.ToString(), StringComparison.Ordinal);
    }

    private sealed class TempFile : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aw-fact-" + System.IO.Path.GetRandomFileName() + ".json");

        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }
}
