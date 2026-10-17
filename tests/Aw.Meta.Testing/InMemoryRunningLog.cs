using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory IRunningLog: the one fake, kept honest by RunningLogContract.</summary>
public sealed class InMemoryRunningLog : IRunningLog
{
    private readonly List<string> _lines = [];

    /// <summary>Every line appended, in order.</summary>
    public IReadOnlyList<string> Lines => _lines;

    /// <summary>Whether appends fail (an unwritable log).</summary>
    public bool FailAppends { get; set; }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Append(string line, CancellationToken cancellationToken)
    {
        if (FailAppends)
        {
            return Done.Failed<Unit>(MetaErrors.LogUnwritable.With("the running log is not writable"));
        }

        _lines.Add(line);
        return Done.Unit();
    }
}
