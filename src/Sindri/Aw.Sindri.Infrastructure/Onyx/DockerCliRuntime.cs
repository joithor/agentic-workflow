using System.Globalization;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Secrets;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>IContainerRuntime over the docker CLI: four read-only commands, each with a 5 s timeout, arguments as a list (never a shell string).</summary>
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

        var serverVersion = Text(server);
        cancellationToken.ThrowIfCancellationRequested();
        var compose = Docker(["compose", "version", "--short"]);
        var composeVersion = Text(compose);
        cancellationToken.ThrowIfCancellationRequested();
        long? memory = null;
        string? dataRoot = null;
        if (serverVersion is not null)
        {
            var info = Text(Docker(["info", "--format", "{{.MemTotal}}"]));
            memory = long.TryParse(info, NumberStyles.None, CultureInfo.InvariantCulture, out var bytes) ? bytes : null;
            dataRoot = Text(Docker(["info", "--format", "{{.DockerRootDir}}"])) is { } root && Path.IsPathRooted(root) && !root.Any(char.IsControl) ? root : null;
        }

        return new ContainerRuntimeFacts(
            CliFound: true, DaemonAnswers: serverVersion is not null, ServerVersion: serverVersion, ComposeV2: composeVersion is not null, ComposeVersion: composeVersion, MemoryBytes: memory, DataRoot: dataRoot);
    }

    private ToolRun Docker(string[] arguments) => runner.Run("docker", arguments, _timeout);

    private static string? Text(ToolRun run)
        => run is { Outcome: ToolOutcome.Exited, ExitCode: 0 } && run.Output.Trim() is { Length: > 0 } text ? text : null;
}
