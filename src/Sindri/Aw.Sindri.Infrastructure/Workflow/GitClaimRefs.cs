using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Effects;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>The claim's marker ref through git (engine spec §12 'effect.claim.ref'; engine P6 unit (d)).</summary>
internal sealed class GitClaimRefs : IClaimRefs
{
    public ValueTask<Result<bool>> Mark(string repository, string reference, CancellationToken cancellationToken) =>
        throw new NotSupportedException("engine P6 unit (d): git update-ref");
}
