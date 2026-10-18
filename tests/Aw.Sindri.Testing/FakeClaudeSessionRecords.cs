using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Testing;

/// <summary>Claude's session records in memory.</summary>
public sealed class FakeClaudeSessionRecords : IClaudeSessionRecords
{
    private readonly List<ClaudeRecord> _records = [];
    private bool _failing;

    /// <summary>Adds a record.</summary>
    /// <param name="record">The record.</param>
    /// <returns>These records.</returns>
    public FakeClaudeSessionRecords With(ClaudeRecord record)
    {
        _records.Add(record);
        return this;
    }

    /// <summary>Makes <see cref="Read"/> fail.</summary>
    public void Fail() => _failing = true;

    /// <inheritdoc/>
    public Result<IReadOnlyList<ClaudeRecord>> Read(CancellationToken cancellationToken) =>
        _failing ? SindriErrors.SessionSourceFailed.With("claude-records-unreadable") : Results.Success<IReadOnlyList<ClaudeRecord>>([.. _records]);
}
