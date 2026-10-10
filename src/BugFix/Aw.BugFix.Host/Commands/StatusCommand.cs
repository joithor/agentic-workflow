using System.Text.Json;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Features.Status;

namespace Aw.BugFix.Host.Commands;

/// <summary>The 'status' command: binds its arguments, calls Status and maps the result to an exit code.</summary>
internal static class StatusCommand
{
    internal const string Name = "status";

    internal static readonly CommandSpec Spec = new("status --state <dir>", ["state"], [], []);

    internal static async Task<int> Run(CommandLine line, IServiceProvider services, JsonSerializerOptions json) => await CommandOutput.Call<StatusRequest, BugFixState>(services, json, new StatusRequest(line.StateDirectory)).ConfigureAwait(false);
}
