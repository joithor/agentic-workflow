using Aw.Kernel;

namespace Aw.Sindri.Core.Views;

/// <summary>The queries' read port over the views (a read-only connection); it never creates the views store.</summary>
public interface IViewReader
{
    /// <summary>The row under <paramref name="key"/> in <paramref name="view"/>, or null.</summary>
    /// <typeparam name="TRow">The row record.</typeparam>
    /// <param name="view">A <see cref="ViewNames"/> name.</param>
    /// <param name="key">The row's key.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The row, if any.</returns>
    public ValueTask<Result<TRow?>> Get<TRow>(string view, string key, CancellationToken cancellationToken)
        where TRow : class;

    /// <summary>Every row in <paramref name="view"/>, ordered by key.</summary>
    /// <typeparam name="TRow">The row record.</typeparam>
    /// <param name="view">A <see cref="ViewNames"/> name.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The rows.</returns>
    public ValueTask<Result<IReadOnlyList<TRow>>> List<TRow>(string view, CancellationToken cancellationToken)
        where TRow : class;

    /// <summary>The projector_state row, or null when the views store doesn't exist (doctor reads it while stopped).</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The state, if any.</returns>
    public ValueTask<Result<ProjectorState?>> State(CancellationToken cancellationToken);
}
