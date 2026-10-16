using System.Globalization;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Secrets;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>IContainerRuntime over the docker CLI: three read-only commands, each with a 5 s timeout, arguments as a list (never a shell string).</summary>
internal sealed class DockerCliRuntime(IToolRunner runner) : IContainerRuntime
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    public ContainerRuntimeFacts Inspect(CancellationToken cancellationToken)
    {
        var server = Docker(["version", "--format", "{{.Server.Version}}"]);
        if (server.Outcome == ToolOutcome.Missing)
        {
            return new ContainerRuntimeFacts(CliFound: false, DaemonAnswers: false, ServerVersion: null, ComposeV2: false, ComposeVersion: null, MemoryBytes: null, DataRoot: null);
        }

        var serverText = Text(server);
        cancellationToken.ThrowIfCancellationRequested();
        var compose = Docker(["compose", "version", "--short"]);
        var composeText = Text(compose);
        cancellationToken.ThrowIfCancellationRequested();
        var (memory, dataRoot) = serverText is null ? (null, null) : Info(Text(Docker(["info", "--format", "{{.MemTotal}}|{{.DockerRootDir}}"])));

        return new ContainerRuntimeFacts(
            CliFound: true, DaemonAnswers: serverText is not null, ServerVersion: Version(serverText), ComposeV2: composeText is not null, ComposeVersion: Version(composeText), MemoryBytes: memory, DataRoot: dataRoot);
    }

    // One `docker info` line, MemTotal|DockerRootDir, split at the first '|' (a path may hold one, a byte count never does). Each half is guarded
    // on its own: memory is a plain non-negative number, the data root an absolute path with no control character; anything else reads as unknown.
    private static (long? Memory, string? DataRoot) Info(string? line)
    {
        var bar = line?.IndexOf('|', StringComparison.Ordinal) ?? -1;
        if (line is null || bar < 0)
        {
            return (null, null);
        }

        var memory = long.TryParse(line.AsSpan(0, bar), NumberStyles.None, CultureInfo.InvariantCulture, out var bytes) ? bytes : (long?)null;
        var root = line[(bar + 1)..];
        return (memory, Path.IsPathRooted(root) && !root.Any(char.IsControl) ? root : null);
    }

    private ToolRun Docker(string[] arguments) => runner.Run("docker", arguments, _timeout);

    // A version is shown in a row on the terminal, so only a token of [0-9A-Za-z.+_-], 1 to 64 long, is passed on; anything else (escape sequences, spaces) reads as unreadable.
    private static string? Version(string? text) => text is not null && text.Length <= 64 && text.All(static c => c is (>= '0' and <= '9') or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or '.' or '+' or '_' or '-') ? text : null;

    private static string? Text(ToolRun run)
        => run is { Outcome: ToolOutcome.Exited, ExitCode: 0 } && run.Output.Trim() is { Length: > 0 } text ? text : null;
}
