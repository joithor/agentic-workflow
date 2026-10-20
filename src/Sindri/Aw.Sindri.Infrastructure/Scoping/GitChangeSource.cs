using Aw.Kernel;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Infrastructure.Scoping;

/// <summary>The change port over git (and gh for a PR number).</summary>
internal sealed class GitChangeSource : IChangeSource
{
    public ValueTask<Result<ChangeRecord>> Read(string checkout, string reference, ChangeRead read, CancellationToken cancellationToken) => throw new NotSupportedException();
}
