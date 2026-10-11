using System.Text.Json;
using Aw.BugFix.Features.RunUi;

namespace Aw.BugFix.Host.Commands;

/// <summary>The 'run-ui' command: binds its arguments, calls RunUi and maps the result to an exit code.</summary>
internal static class RunUiCommand
{
    internal const string Name = "run-ui";

    internal static readonly CommandSpec Spec = new("run-ui --state <dir> --check <script.json> [--cwd <dir>]", ["state", "check"], ["cwd"], []);

    internal static async Task<int> Run(CommandLine line, IServiceProvider services, JsonSerializerOptions json) => await CommandOutput.Call<RunUiRequest, RunUiResponse>(services, json, new RunUiRequest(line.StateDirectory, line.Values["check"], line.WorkingDirectory)).ConfigureAwait(false);
}
