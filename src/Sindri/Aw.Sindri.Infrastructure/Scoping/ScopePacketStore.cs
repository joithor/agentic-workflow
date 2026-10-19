using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Scoping;

/// <summary>
/// The candidate scope packet in the instance's run area, $AW_SINDRI_HOME/workflow/runs/&lt;instance&gt;/scope-packet/ (scoping spec §8.3),
/// hashed with aw-packets hash --kind scoping (AW_PACKETS_BIN, else aw-packets on the PATH) and read file by file.
/// </summary>
/// <param name="settings">Sindri's home.</param>
/// <param name="runner">Runs aw-packets.</param>
/// <param name="environment">Reads AW_PACKETS_BIN.</param>
internal sealed class ScopePacketStore(SindriSettings settings, IToolRunner runner, Func<string, string?> environment) : IScopePacketStore
{
    private const long MaxBytes = 4 * 1024 * 1024;
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(60);

    public ValueTask<Result<string>> Hash(WorkflowInstanceId instance, CancellationToken cancellationToken)
    {
        var directory = PacketDirectory(instance);
        if (!Directory.Exists(directory))
        {
            return ValueTask.FromResult<Result<string>>(NotThere(instance));
        }

        var program = environment("AW_PACKETS_BIN") is { Length: > 0 } given ? given : "aw-packets";
        var run = runner.Run(program, ["hash", "--kind", "scoping", directory], _timeout, options: null, cancellationToken);
        var hash = run.Output.Trim();
        Result<string> result = run.Outcome == ToolOutcome.Exited && run.ExitCode == 0 && hash.StartsWith("sha256:", StringComparison.Ordinal)
            ? hash
            : ScopingErrors.PacketChanged.With($"aw-packets hash failed ({program}: {run.Outcome}, exit {run.ExitCode.ToString(CultureInfo.InvariantCulture)}) (fix: build aw-packets and put it on the PATH, or set AW_PACKETS_BIN)");
        return ValueTask.FromResult(result);
    }

    public async ValueTask<Result<string>> ReadFile(WorkflowInstanceId instance, string name, CancellationToken cancellationToken)
    {
        var path = Path.Combine(PacketDirectory(instance), name);
        if (!string.Equals(Path.GetFileName(path), name, StringComparison.Ordinal) || !File.Exists(path) || new FileInfo(path).Length > MaxBytes)
        {
            return NotThere(instance);
        }

        return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
    }

    private string PacketDirectory(WorkflowInstanceId instance) => Path.Combine(settings.Home, "workflow", "runs", instance.Value, "scope-packet");

    private static Error NotThere(WorkflowInstanceId instance) => SindriErrors.ItemNotFound.With($"No scope packet in the run area of workflow instance {instance.Value}.");
}
