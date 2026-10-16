using Aw.Context.Core.Engine;

namespace Aw.Context.Features.PreviewReviewFile;

/// <summary>A review file to check before it is applied.</summary>
internal sealed record PreviewReviewFileRequest(string Path, ContextScope Scope);
