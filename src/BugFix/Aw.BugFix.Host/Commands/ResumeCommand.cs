using System.Text.Json;
using Aw.BugFix.Features.Resume;

namespace Aw.BugFix.Host.Commands;

/// <summary>The 'resume' command: binds its arguments, calls Resume and maps the result to an exit code.</summary>
internal static class ResumeCommand
{
    internal const string Name = "resume";

    internal static readonly CommandSpec Spec = new("resume --state <dir>", ["state"], [], []);

    internal static async Task<int> Run(CommandLine line, IServiceProvider services, JsonSerializerOptions json) => await CommandOutput.Call<ResumeRequest, ResumeResponse>(services, json, new ResumeRequest(line.StateDirectory)).ConfigureAwait(false);
}
