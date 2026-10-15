using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>
/// The in-memory ICommandRunner: the one fake, kept honest by CommandRunnerContract. It understands argument vectors
/// built by its scenario methods (<see cref="Prints"/>, <see cref="Exits"/>, <see cref="Hangs"/>, <see cref="Missing"/>).
/// </summary>
public sealed class InMemoryCommandRunner : ICommandRunner
{
    private const string Marker = "fake-command";

    private readonly List<CommandRequest> _requests = [];

    /// <summary>A command that prints <paramref name="text"/> and exits 0.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The argument vector.</returns>
    public static IReadOnlyList<string> Prints(string text) => [Marker, "prints", text];

    /// <summary>A command that exits with <paramref name="code"/>.</summary>
    /// <param name="code">The exit code.</param>
    /// <returns>The argument vector.</returns>
    public static IReadOnlyList<string> Exits(int code) => [Marker, "exits", code.ToString(CultureInfo.InvariantCulture)];

    /// <summary>A command that never finishes on its own.</summary>
    /// <returns>The argument vector.</returns>
    public static IReadOnlyList<string> Hangs() => [Marker, "hangs"];

    /// <summary>A program that does not exist.</summary>
    /// <returns>The argument vector.</returns>
    public static IReadOnlyList<string> Missing() => [Marker, "missing"];

    /// <summary>Every request run, in order.</summary>
    public IReadOnlyList<CommandRequest> Requests => _requests;

    /// <inheritdoc />
    public async ValueTask<Result<CommandResult>> Run(CommandRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _requests.Add(request);
        var argv = request.Argv;
        if (argv.Count < 2 || argv[0] != Marker || argv[1] == "missing")
        {
            return MetaErrors.CommandNotStarted.With("program not found: " + (argv.Count > 0 ? argv[0] : string.Empty));
        }

        if (argv[1] == "hangs")
        {
            await Task.Delay(request.Timeout, cancellationToken);
            return new CommandResult(-1, string.Empty, string.Empty, TimedOut: true);
        }

        var text = argv[1] == "prints" ? argv[2] : string.Empty;
        if (request.LogFile is { } log)
        {
            await File.AppendAllTextAsync(log, text, cancellationToken);
        }

        return new CommandResult(argv[1] == "exits" ? int.Parse(argv[2], CultureInfo.InvariantCulture) : 0, text, string.Empty, TimedOut: false);
    }
}
