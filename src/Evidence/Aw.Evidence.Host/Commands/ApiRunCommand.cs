using System.Text.Json;

namespace Aw.Evidence.Host.Commands;

/// <summary>The 'api-run' command: binds its arguments, calls ApiRun and prints the summary exactly as written (exit 0 passed, 2 failed, 1 refused, 4 errored).</summary>
internal static class ApiRunCommand
{
    internal const string Name = "api-run";
    internal const string Shape =
        "api-run <script.json> <run-dir> --at base|head [--repo dir] [--app-build sha] [--host url] [--allow-preview-host] [--config file] [--lock-timeout s]";

    internal static Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
