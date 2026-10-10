using System.Text.Json;
using Aw.BugFix.Features.Init;

namespace Aw.BugFix.Host.Commands;

/// <summary>The 'init' command: binds its arguments, calls Init and maps the result to an exit code.</summary>
internal static class InitCommand
{
    internal const string Name = "init";

    internal static readonly CommandSpec Spec = new("init --state <dir> --ticket <ticket.json>", ["state", "ticket"], [], []);

    internal static async Task<int> Run(CommandLine line, IServiceProvider services, JsonSerializerOptions json) => await CommandOutput.Call<InitRequest, InitResponse>(services, json, new InitRequest(line.StateDirectory, line.FullPath("ticket"))).ConfigureAwait(false);
}
