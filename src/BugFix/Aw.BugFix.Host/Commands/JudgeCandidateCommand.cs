using System.Text.Json;
using Aw.BugFix.Core.Ids;
using Aw.BugFix.Features.JudgeCandidate;

namespace Aw.BugFix.Host.Commands;

/// <summary>The 'judge' command: binds its arguments, calls JudgeCandidate and maps the result to an exit code.</summary>
internal static class JudgeCandidateCommand
{
    internal const string Name = "judge";

    internal static readonly CommandSpec Spec = new("judge <candidate> --state <dir>", ["state"], [], [], Positionals: 1);

    internal static async Task<int> Run(CommandLine line, IServiceProvider services, JsonSerializerOptions json)
    {
        if (!CandidateId.TryFrom(line.Positionals[0], out var id))
        {
            return CommandOutput.Usage($"the candidate must be an id like c1 (got \"{line.Positionals[0]}\")", Spec.Usage);
        }

        return await CommandOutput.Call<JudgeCandidateRequest, JudgeCandidateResponse>(services, json, new JudgeCandidateRequest(line.StateDirectory, id)).ConfigureAwait(false);
    }
}
