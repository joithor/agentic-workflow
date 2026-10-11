using System.Text.Json;
using Aw.BugFix.Features.RunTest;

namespace Aw.BugFix.Host.Commands;

/// <summary>The 'run-test' command: binds its arguments, calls RunTest and maps the result to an exit code.</summary>
internal static class RunTestCommand
{
    internal const string Name = "run-test";

    internal static readonly CommandSpec Spec = new("run-test --state <dir> --check <file> [--cwd <dir>] -- <command...>", ["state", "check"], ["cwd"], [], TakesCommand: true);

    internal static async Task<int> Run(CommandLine line, IServiceProvider services, JsonSerializerOptions json) => await CommandOutput.Call<RunTestRequest, RunTestResponse>(services, json, new RunTestRequest(line.StateDirectory, line.Values["check"], line.WorkingDirectory, line.Command)).ConfigureAwait(false);
}
