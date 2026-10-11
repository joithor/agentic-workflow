using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Judge.Host;
using Aw.Judge.Host.Commands;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests;

/// <summary>Runs the judge command line in process, as Program.cs does, on a scratch state directory, capturing its streams.</summary>
/// <param name="state">The AW_STATE_DIR.</param>
public sealed class JudgeCli(TempStateDirectory state)
{
    /// <summary>The clock Compose gets; it starts at the Kernel's test epoch.</summary>
    public FakeTimeProvider Clock { get; } = new(KnownInstants.Epoch);

    /// <summary>Environment variables beyond AW_STATE_DIR and AW_LOG_LEVEL (e.g. AW_SESSION_ID).</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>When set, the providers the questions use instead of the ones Compose builds (the default chain routes them).</summary>
    public IReadOnlyList<IModelProvider>? Providers { get; init; }

    /// <summary>Runs one command line with empty stdin.</summary>
    public Task<CliRun> Run(params string[] args) => RunWithInput("", args);

    /// <summary>Runs one command line with <paramref name="stdin"/> on stdin.</summary>
    public async Task<CliRun> RunWithInput(string stdin, params string[] args)
    {
        var services = Composition.Compose(
            new ServiceCollection(),
            name => name switch { "AW_STATE_DIR" => state.Path, "AW_LOG_LEVEL" => "none", _ => Environment.GetValueOrDefault(name) },
            Clock);
        if (Providers is { } providers)
        {
            _ = services.AddSingleton(new ProviderSet(providers, ChainSpec.Default));   // the last registration wins
        }

        var provider = services.BuildServiceProvider();
        await using (provider)
        {
            var capture = CommandOutput.Capture(stdin);
            var code = await CommandTable.Run(args, provider, AwJson.CreateOptions(JudgeJsonContext.Default));
            return new CliRun(code, capture.Stdout.ToString(), capture.Stderr.ToString());
        }
    }
}
