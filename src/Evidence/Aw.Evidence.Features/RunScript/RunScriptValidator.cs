using Aw.Evidence.Core;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Ids;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.RunScript;

internal sealed class RunScriptValidator : IValidator<RunScriptRequest>
{
    public Result<Unit> Validate(RunScriptRequest value)
    {
        string?[] optional = [value.Baseline, value.CacheManifest];
        if (!IsAbsolute(value.ScriptPath) || !IsAbsolute(value.RunDirectory) || optional.Any(static p => p is not null && !IsAbsolute(p)))
        {
            return EvidenceErrors.Invalid.With("script, run directory, baseline and cache paths must be absolute");
        }

        if (!RunId.TryFrom(RunPath.NameOf(value.RunDirectory), out _))
        {
            return EvidenceErrors.Invalid.With("the run directory needs a name (it is the run id)");
        }

        return value.LockTimeout < TimeSpan.Zero ? EvidenceErrors.Invalid.With("the lock timeout must not be negative") : Unit.Value;
    }

    private static bool IsAbsolute(string path) => path.StartsWith('/');
}
