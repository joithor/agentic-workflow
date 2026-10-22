using Aw.Kernel;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The pre-push hook that makes a launched agent Step unable to push (line spec LN-LCH1).</summary>
internal static class PrePushGuard
{
    internal static ValueTask<Error?> Install(string workDir, CancellationToken cancellationToken) => throw new NotSupportedException();
}
