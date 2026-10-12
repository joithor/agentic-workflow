using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// Where the API stores a fact's content before the start or the desk report that references it (port; engine spec rev
/// 8.1 §5.3, §6.1): content travels from the CLI as bytes, at most <see cref="MaxBytes"/> each, and is kept owner-only
/// (0600) by its SHA-256. The server never opens a client path.
/// </summary>
public interface IFactStore
{
    /// <summary>The largest fact content a CLI may send: 1 MiB (spec §6.1).</summary>
    public const int MaxBytes = 1024 * 1024;

    /// <summary>Stores <paramref name="content"/> under its SHA-256; storing the same content again changes nothing.</summary>
    /// <param name="content">The fact's content, at most <see cref="MaxBytes"/>.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The stored reference ('fact:sha256:&lt;hex&gt;') and its SHA-256, or an error when it can't be written.</returns>
    public ValueTask<Result<FactValue>> Put(ReadOnlyMemory<byte> content, CancellationToken cancellationToken);
}
