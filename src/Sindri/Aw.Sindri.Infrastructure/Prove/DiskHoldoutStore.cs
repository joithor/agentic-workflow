using Aw.Kernel;
using Aw.Sindri.Core.Prove;

namespace Aw.Sindri.Infrastructure.Prove;

/// <summary><see cref="IHoldoutStore"/> on disk: <c language="text">$AW_SINDRI_HOME/holdout/&lt;line&gt;/&lt;phase&gt;/</c>, directories 0700 and files 0600 (line spec §9.1).</summary>
/// <param name="settings">Sindri's home.</param>
internal sealed class DiskHoldoutStore(SindriSettings settings) : IHoldoutStore
{
    /// <inheritdoc/>
    public ValueTask<Result<HoldoutSet>> Set(string line, string phase, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<Result<HoldoutSeal>> Seal(string line, string phase, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlySet<string>>> Spent(string line, string phase, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<Result<bool>> MarkSpent(string line, string phase, IReadOnlyCollection<string> scenarios, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>The phase's directory.</summary>
    /// <param name="line">The line's slug.</param>
    /// <param name="phase">The phase key.</param>
    /// <returns>The path.</returns>
    internal string Directory(string line, string phase) => Path.Combine(settings.Home, "holdout", line, phase);
}
