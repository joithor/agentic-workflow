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

    /// <summary>Runs one command line with empty stdin.</summary>
    public Task<CliRun> Run(params string[] args) => RunWithInput("", args);

    /// <summary>Runs one command line with <paramref name="stdin"/> on stdin.</summary>
    public async Task<CliRun> RunWithInput(string stdin, params string[] args)
    {
        var services = Composition.Compose(
            new ServiceCollection(),
            name => name switch { "AW_STATE_DIR" => state.Path, "AW_LOG_LEVEL" => "none", _ => null },
            Clock);
        var provider = services.BuildServiceProvider();
        await using (provider)
        {
            var capture = CommandOutput.Capture(stdin);
            var code = await CommandTable.Run(args, provider, AwJson.CreateOptions(JudgeJsonContext.Default));
            return new CliRun(code, capture.Stdout.ToString(), capture.Stderr.ToString());
        }
    }
}
