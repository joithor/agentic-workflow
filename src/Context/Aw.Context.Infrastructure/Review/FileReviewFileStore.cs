using System.Text.Json;
using Aw.Context.Core.Review;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Review;

/// <summary>Review files: JSON written under the exports folder by temp file and rename at 0600, and YAML or JSON read from outside every git worktree.</summary>
/// <param name="settings">Where the exports folder is.</param>
/// <param name="options">The AwJson options over the review types.</param>
internal sealed class FileReviewFileStore(ContextStoreSettings settings, JsonSerializerOptions options) : IReviewFileStore
{
    public ValueTask<Result<string>> Write(string? path, string defaultName, ReviewFile file, CancellationToken cancellationToken) =>
        throw Pending.Because(settings, options);

    public ValueTask<Result<ReviewFile>> Read(string path, CancellationToken cancellationToken) =>
        throw Pending.Because();
}
