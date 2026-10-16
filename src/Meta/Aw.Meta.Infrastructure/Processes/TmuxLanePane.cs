using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Processes;

/// <summary>
/// ILanePane over tmux. Every target is the exact session ("=name:", so a name that is a prefix of another matches nothing else;
/// send-keys wants the colon to resolve the pane), and the wake line is typed in literal mode ("-l"), so it is never read as key names.
/// </summary>
/// <param name="runner">Runs tmux.</param>
/// <param name="tmux">The tmux program (AW_TMUX, default tmux).</param>
/// <param name="socket">The tmux socket name (AW_META_TMUX_SOCKET), passed as "-L"; null for the default socket, which is what production uses.</param>
internal sealed class TmuxLanePane(ICommandRunner runner, string tmux, string? socket = null) : ILanePane
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    public async ValueTask<Result<PaneState?>> Probe(string session, CancellationToken cancellationToken)
    {
        var target = Target(session);
        var command = await Run(["display-message", "-p", "-t", target, "#{pane_current_command}"], cancellationToken).ConfigureAwait(false);
        if (command.IsError || command.Value.ExitCode != 0)
        {
            return Results.Success<PaneState?>(value: null);   // no tmux, or no such session: the lane is down
        }

        var clients = await Run(["list-clients", "-t", "=" + session, "-F", "x"], cancellationToken).ConfigureAwait(false);
        var count = clients.IsError || clients.Value.ExitCode != 0
            ? 0
            : clients.Value.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
        return Results.Success<PaneState?>(new PaneState(command.Value.Stdout.Trim(), count));
    }

    public async ValueTask<Result<Unit>> Wake(string session, string line, CancellationToken cancellationToken)
    {
        var target = Target(session);
        var typed = await Run(["send-keys", "-t", target, "-l", line], cancellationToken).ConfigureAwait(false);
        var entered = typed.IsError || typed.Value.ExitCode != 0 ? typed : await Run(["send-keys", "-t", target, "Enter"], cancellationToken).ConfigureAwait(false);
        return entered.IsError
            ? entered.Error
            : entered.Value.ExitCode == 0 ? Results.Ok() : MetaErrors.CommandFailed.With("tmux send-keys exited " + entered.Value.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture) + ": " + entered.Value.Stderr.Trim());
    }

    private static string Target(string session) => "=" + session + ":";

    private ValueTask<Result<CommandResult>> Run(string[] arguments, CancellationToken cancellationToken) =>
        runner.Run(new CommandRequest(socket is null ? [tmux, .. arguments] : [tmux, "-L", socket, .. arguments], _timeout), cancellationToken);
}
