using System.Text.Json;
using Aw.BugFix.Features.AdvanceReproduce;

namespace Aw.BugFix.Host.Commands;

/// <summary>The 'advance reproduce' command: binds its arguments, calls AdvanceReproduce and maps the result to an exit code.</summary>
internal static class AdvanceReproduceCommand
{
    internal const string Name = "advance reproduce";

    internal static readonly CommandSpec Spec = new("advance reproduce --state <dir> --evidence <run.json> --check <file> [--cwd <dir>]", ["state", "evidence", "check"], ["cwd"], []);

    internal static async Task<int> Run(CommandLine line, IServiceProvider services, JsonSerializerOptions json) => await CommandOutput.Call<AdvanceReproduceRequest, AdvanceReproduceResponse>(services, json, new AdvanceReproduceRequest(line.StateDirectory, line.FullPath("evidence"), line.Values["check"], line.WorkingDirectory)).ConfigureAwait(false);
}
