using Aw.Kernel;
using Aw.Sindri.Core.Prove;

namespace Aw.Sindri.Testing.Prove;

/// <summary>The in-memory <see cref="IHoldoutStore"/>: files by line and phase, read through <see cref="HoldoutFiles"/> as the disk store does; it can be made to refuse every write.</summary>
public sealed class InMemoryHoldoutStore : IHoldoutStore
{
    /// <summary>Whether every write fails, as an unwritable store does.</summary>
    public bool Unwritable { get; set; }

    /// <summary>Puts a phase's files in the store; a null text leaves that file out.</summary>
    /// <param name="line">The line's slug.</param>
    /// <param name="phase">The phase key.</param>
    /// <param name="setText">The set file's text.</param>
    /// <param name="sealText">The seal file's text.</param>
    public void Put(string line, string phase, string? setText, string? sealText = null) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<Result<HoldoutSet>> Set(string line, string phase, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<Result<HoldoutSeal>> Seal(string line, string phase, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlySet<string>>> Spent(string line, string phase, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<Result<bool>> MarkSpent(string line, string phase, IReadOnlyCollection<string> scenarios, CancellationToken cancellationToken) => throw new NotSupportedException();
}
