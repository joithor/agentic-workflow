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
    private readonly Lock _gate = new();

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

    private readonly List<(Func<CommandRequest, bool> Match, Func<CommandRequest, Task<CommandResult>> Result)> _script = [];

    /// <summary>Answers every command whose argument vector, joined by spaces, contains <paramref name="text"/> (earlier rules win).</summary>
    /// <param name="text">What the command line contains.</param>
    /// <param name="stdout">The output.</param>
    /// <param name="exitCode">The exit code.</param>
    /// <returns>This fake.</returns>
    public InMemoryCommandRunner When(string text, string stdout = "", int exitCode = 0) =>
        When(text, _ => new CommandResult(exitCode, stdout, exitCode == 0 ? string.Empty : "failed: " + text, TimedOut: false));

    /// <summary>Answers every command whose argument vector, joined by spaces, contains <paramref name="text"/> with a computed result.</summary>
    /// <param name="text">What the command line contains.</param>
    /// <param name="result">Computes the result from the request.</param>
    /// <returns>This fake.</returns>
    public InMemoryCommandRunner When(string text, Func<CommandRequest, CommandResult> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return When(text, request => Task.FromResult(result(request)));
    }

    /// <summary>Answers every command whose argument vector, joined by spaces, contains <paramref name="text"/> with a result that may take time (a step that has to be released by the test).</summary>
    /// <param name="text">What the command line contains.</param>
    /// <param name="result">Computes the result from the request.</param>
    /// <returns>This fake.</returns>
    public InMemoryCommandRunner When(string text, Func<CommandRequest, Task<CommandResult>> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _script.Add((request => string.Join(' ', request.Argv).Contains(text, StringComparison.Ordinal), result));
        return this;
    }

    /// <summary>The command lines run (argument vectors joined by spaces), in order.</summary>
    public IReadOnlyList<string> Lines => [.. Requests.Select(r => string.Join(' ', r.Argv))];

    /// <summary>Every request run, in order.</summary>
    public IReadOnlyList<CommandRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<CommandResult>> Run(CommandRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            _requests.Add(request);
        }

        var argv = request.Argv;
        if (_script.FirstOrDefault(rule => rule.Match(request)) is { Result: { } answer })
        {
            return await answer(request);
        }

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
