using System.Text.Json;
using Aw.BugFix.Core.Ids;
using Aw.BugFix.Features.RecordRun;

namespace Aw.BugFix.Host.Commands;

/// <summary>The 'record-run' command: binds its arguments, calls RecordRun and maps the result to an exit code.</summary>
internal static class RecordRunCommand
{
    internal const string Name = "record-run";

    internal static readonly CommandSpec Spec = new("record-run <candidate> --state <dir> --evidence <run.json>", ["state", "evidence"], [], [], Positionals: 1);

    internal static async Task<int> Run(CommandLine line, IServiceProvider services, JsonSerializerOptions json)
    {
        if (!CandidateId.TryFrom(line.Positionals[0], out var id))
        {
            return CommandOutput.Usage($"the candidate must be an id like c1 (got \"{line.Positionals[0]}\")", Spec.Usage);
        }

        return await CommandOutput.Call<RecordRunRequest, RecordRunResponse>(services, json, new RecordRunRequest(line.StateDirectory, id, line.FullPath("evidence"))).ConfigureAwait(false);
    }
}
