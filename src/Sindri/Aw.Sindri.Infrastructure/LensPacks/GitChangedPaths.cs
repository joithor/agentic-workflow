using Aw.Kernel;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Infrastructure.Workflow;

namespace Aw.Sindri.Infrastructure.LensPacks;

/// <summary><see cref="IChangedPaths"/> through git: <c language="text">git diff --name-only -z base...head</c>, the change since the two branched apart.</summary>
internal sealed class GitChangedPaths : IChangedPaths
{
    public async ValueTask<Result<IReadOnlyList<string>>> Between(string checkout, string baseRef, string head, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(checkout);
        ArgumentNullException.ThrowIfNull(baseRef);
        ArgumentNullException.ThrowIfNull(head);
        var run = await GitProcess.Run(checkout, ["diff", "--name-only", "-z", "--end-of-options", $"{baseRef}...{head}"], cancellationToken).ConfigureAwait(false);
        if (run.Exit != 0)
        {
            return WorkflowErrors.EffectFailed.With($"git diff {baseRef}...{head} failed in {checkout}: {run.Error.Trim()}");
        }

        IReadOnlyList<string> paths = [.. run.Output.Split('\0', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        return Results.Success(paths);
    }
}
