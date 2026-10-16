using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Kernel;

namespace Aw.Evidence.Testing;

/// <summary>A scripted ICliRunner: an outcome per program path (exit 0, no output by default), programs that will not start, and every invocation recorded.</summary>
public sealed class FakeCliRunner : ICliRunner
{
    private readonly Dictionary<string, Func<CliInvocation, CliOutcome>> _programs = new(StringComparer.Ordinal);
    private readonly List<CliInvocation> _calls = [];

    /// <summary>Every invocation, in order.</summary>
    public IReadOnlyList<CliInvocation> Calls => _calls;

    /// <summary>Programs that will not start.</summary>
    public ISet<string> WontStart { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Makes <paramref name="executable"/> answer with <paramref name="outcome"/>.</summary>
    /// <param name="executable">The program path.</param>
    /// <param name="outcome">What it does.</param>
    /// <returns>This fake.</returns>
    public FakeCliRunner Program(string executable, CliOutcome outcome) => Program(executable, _ => outcome);

    /// <summary>Makes <paramref name="executable"/> answer with whatever <paramref name="behavior"/> makes of the invocation.</summary>
    /// <param name="executable">The program path.</param>
    /// <param name="behavior">What it does.</param>
    /// <returns>This fake.</returns>
    public FakeCliRunner Program(string executable, Func<CliInvocation, CliOutcome> behavior)
    {
        _programs[executable] = behavior;
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<CliOutcome>> RunAsync(CliInvocation invocation, CancellationToken cancellationToken)
    {
        _calls.Add(invocation);
        if (WontStart.Contains(invocation.Executable))
        {
            return ValueTask.FromResult<Result<CliOutcome>>(EvidenceErrors.RunFailed.With($"{invocation.Executable} could not start"));
        }

        var outcome = _programs.TryGetValue(invocation.Executable, out var behavior) ? behavior(invocation) : new CliOutcome(0, string.Empty, string.Empty);
        return ValueTask.FromResult<Result<CliOutcome>>(outcome);
    }
}
