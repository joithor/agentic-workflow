using Aw.Evidence.Core;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Ids;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.Parity;

internal sealed class ParityValidator : IValidator<ParityRequest>
{
    public Result<Unit> Validate(ParityRequest value)
    {
        if (!value.ManifestPath.StartsWith('/') || !value.RunDirectory.StartsWith('/'))
        {
            return EvidenceErrors.Invalid.With("manifest and run directory paths must be absolute");
        }

        if (!RunId.TryFrom(RunPath.NameOf(value.RunDirectory), out _))
        {
            return EvidenceErrors.Invalid.With("the run directory needs a name (it is the run id)");
        }

        return value.LockTimeout < TimeSpan.Zero ? EvidenceErrors.Invalid.With("the lock timeout must not be negative") : Unit.Value;
    }
}
