using Aw.Context.Core.Engine;

namespace Aw.Context.Features.ApplyReviewFile;

/// <summary>Applies a review file; entries that match nothing are listed as skipped.</summary>
internal sealed record ApplyReviewFileRequest(string Path, ContextScope Scope);
