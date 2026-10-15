using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.ApplyCommand;

/// <summary>Runs a top-level short form (not under 'workflow') in process, as WorkflowsHarness runs the long ones.</summary>
internal static class Short
{
    /// <summary>Runs <paramref name="args"/>; without a control plane to ask, the client is wired to a probe that finds none.</summary>
    internal static async Task<(int Exit, string Stderr)> Run(IServiceProvider services, string? signal, params string[] args)
    {
        var json = SliceHarness.Json;
        var stderr = new StringWriter();
        var output = new Output(new StringWriter(), stderr, json);
        var probe = services.GetService<IControlPlaneProbe>() ?? new FakeControlPlaneProbe();
        var tokens = services.GetService<IHumanTokenStore>() ?? new InMemoryHumanTokenStore();
        using var client = new ControlPlaneClient(probe, json,
            CallerHeaders.For(new AgentSession(signal, Terminal: true, Prover: false), tokens, static _ => ValueTask.FromResult<RepoName?>(null)), TimeSpan.FromSeconds(30));
        var desk = new RecipeDesk(client, new StringReader(string.Empty), static _ => Task.FromResult<Kernel.Error?>(null));
        var exit = await WorkflowCommandTable.RunShort(args, services, output, desk, TestContext.Current.CancellationToken);
        return (exit, stderr.ToString());
    }
}
