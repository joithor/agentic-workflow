using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Infrastructure.Launch.Usage;

/// <summary>The 'claude-code' usage reader (stub until its tests pass).</summary>
/// <param name="root">The transcripts' root.</param>
internal sealed class ClaudeCodeUsage(string root) : IUsageReader
{
    /// <inheritdoc/>
    public string Name => "claude-code";

    /// <inheritdoc/>
    public bool Verified => true;

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ModelTokens>> Between(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) => throw new NotSupportedException(root);

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ModelTokens>> OfSession(SessionId session, CancellationToken cancellationToken) => throw new NotSupportedException(root);
}
