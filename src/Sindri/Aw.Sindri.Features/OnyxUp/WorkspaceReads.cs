using Aw.Kernel;

namespace Aw.Sindri.Features.OnyxUp;

internal static class WorkspaceReads
{
    internal static string? OrNull(this Result<string?> read) => read.IsSuccess ? read.Value : null;
}
