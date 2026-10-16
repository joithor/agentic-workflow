using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Testing;

/// <summary>An <see cref="IJobKinds"/> that knows exactly the kinds it was given.</summary>
/// <param name="known">The kinds, in the order they are listed.</param>
public sealed class FixedJobKinds(params JobKind[] known) : IJobKinds
{
    /// <inheritdoc/>
    public IReadOnlyList<JobKind> Known { get; } = known;
}
