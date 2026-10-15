using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The IRunningLog contract: the same tests run against the in-memory fake and every real adapter.</summary>
public abstract class RunningLogContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>A log that starts empty (its file and directory may not exist yet).</summary>
    /// <returns>The log.</returns>
    protected abstract IRunningLog Create();

    /// <summary>The lines the log holds, one per line of its file.</summary>
    /// <param name="log">The log made by <see cref="Create"/>.</param>
    /// <returns>The lines.</returns>
    protected abstract IReadOnlyList<string> Read(IRunningLog log);

    /// <summary>Appended lines are kept in order, each on its own line, and earlier lines survive.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Lines_are_kept_in_order()
    {
        var log = Create();

        var one = await log.Append("- MERGE 2026-10-15 04:00: #190 merged", _none);
        var two = await log.Append("- NOTE 2026-10-15 04:01: ok ✓", _none);

        Expect.That(one.IsSuccess && two.IsSuccess, "appends succeed");
        Expect.That(Read(log).SequenceEqual(["- MERGE 2026-10-15 04:00: #190 merged", "- NOTE 2026-10-15 04:01: ok ✓"], StringComparer.Ordinal), "both lines, in order");
    }
}
