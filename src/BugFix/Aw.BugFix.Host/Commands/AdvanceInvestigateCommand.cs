using System.Text.Json;
using Aw.BugFix.Features.AdvanceInvestigate;

namespace Aw.BugFix.Host.Commands;

/// <summary>The 'advance investigate' command: binds its arguments, calls AdvanceInvestigate and maps the result to an exit code.</summary>
internal static class AdvanceInvestigateCommand
{
    internal const string Name = "advance investigate";

    internal static readonly CommandSpec Spec = new("advance investigate --state <dir> --evidence <handoff.md>", ["state", "evidence"], [], []);

    internal static async Task<int> Run(CommandLine line, IServiceProvider services, JsonSerializerOptions json) => await CommandOutput.Call<AdvanceInvestigateRequest, AdvanceInvestigateResponse>(services, json, new AdvanceInvestigateRequest(line.StateDirectory, line.FullPath("evidence"))).ConfigureAwait(false);
}
