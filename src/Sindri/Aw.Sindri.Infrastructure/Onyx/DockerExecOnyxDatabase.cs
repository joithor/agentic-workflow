using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>
/// IOnyxDatabase over the docker CLI: `docker ps` finds the running `relational_db` container of Sindri's compose project by its labels, then `docker exec … psql` runs one fixed, read-only query in it
/// (the session is read-only through PGOPTIONS, and no value from outside reaches the command: the project name is checked as one Sindri made, the container id as hex). Arguments are a list, never a shell string.
/// </summary>
/// <param name="runner">Runs the docker CLI.</param>
internal sealed class DockerExecOnyxDatabase(IToolRunner runner) : IOnyxDatabase
{
    private const string ProjectLabel = "com.docker.compose.project";
    private const string ServiceLabel = "com.docker.compose.service";
    private const string Service = "relational_db";
    private const string Query = "select id from user_group where is_default and name = 'Admin'";
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);

    /// <inheritdoc />
    public Result<int?> DefaultAdminGroup(ComposeProject project, CancellationToken cancellationToken)
    {
        if (!ComposeProject.IsOurs(project.Name))
        {
            return SindriErrors.OnyxComposeFailed.With($"refusing to act on \"{project.Name}\": it isn't a compose project Sindri names");
        }

        var found = Run(["ps", "-q", "--filter", $"label={ProjectLabel}={project.Name}", "--filter", $"label={ServiceLabel}={Service}"], "ps", cancellationToken);
        if (found.IsError)
        {
            return found.Error;
        }

        var ids = found.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (ids.Length == 0)
        {
            return Results.Success<int?>(value: null);
        }

        if (ids.Length > 1 || !IsContainerId(ids[0]))
        {
            return SindriErrors.OnyxComposeFailed.With("`docker ps` didn't name exactly one Onyx database container for this home; check `docker ps`, then run `sindri onyx up` again");
        }

        var read = Run(["exec", "-e", "PGOPTIONS=-c default_transaction_read_only=on", ids[0], "psql", "-X", "-U", "postgres", "-tA", "-c", Query], "exec", cancellationToken);
        return read.IsError ? read.Error : Group(read.Value);
    }

    private static Result<int?> Group(string printed)
    {
        var rows = printed.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return rows.Length == 1 && int.TryParse(rows[0], NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
            ? Results.Success<int?>(id)
            : SindriErrors.OnyxKeyFailed.With("Onyx's database has no single default Admin group; this Onyx release may differ from the pinned one");
    }

    private static bool IsContainerId(string id) => id.Length is >= 12 and <= 64 && id.All(static c => char.IsAsciiHexDigitLower(c));

    private Result<string> Run(string[] arguments, string what, CancellationToken cancellationToken)
    {
        List<string> tail = [];
        var run = runner.Run("docker", arguments, _timeout, new ToolRunOptions(tail.Add), cancellationToken);
        return run.Outcome switch
        {
            ToolOutcome.Cancelled => throw new OperationCanceledException(cancellationToken),
            ToolOutcome.Missing => SindriErrors.OnyxDockerUnavailable.With("Docker isn't installed: install Docker Desktop (or Docker Engine), then run this again"),
            ToolOutcome.TimedOut => SindriErrors.OnyxDockerUnavailable.With("Docker isn't answering (a command hung): start or restart Docker Desktop (or the docker service) and wait until it is ready, then run this again"),
            ToolOutcome.Exited when run.ExitCode == 0 => run.Output,
            _ => SindriErrors.OnyxComposeFailed.With(string.Create(CultureInfo.InvariantCulture, $"`docker {what}` failed (exit {run.ExitCode}): {Printed(tail)}; fix what it says")),
        };
    }

    // The last lines the command printed on standard error, printable characters only, bounded.
    private static string Printed(List<string> lines)
    {
        var text = string.Join(" | ", lines.TakeLast(5).Select(static l => new string([.. l.Where(static c => !char.IsControl(c))]).Trim()).Where(static l => l.Length > 0));
        return text.Length == 0 ? "no output" : text.Length > 400 ? text[^400..] : text;
    }
}
