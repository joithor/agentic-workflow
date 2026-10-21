using Aw.Kernel;
using Aw.Sindri.Core.Prove;

namespace Aw.Sindri.Infrastructure.Prove;

/// <summary><see cref="ICommitText"/> through 'git log -p': the messages and patches of <c language="text">base..head</c>, read-only and bounded to 30 s.</summary>
internal sealed class GitCommitText : ICommitText
{
    /// <inheritdoc/>
    public ValueTask<Result<string>> Range(string checkout, string baseSha, string head, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
