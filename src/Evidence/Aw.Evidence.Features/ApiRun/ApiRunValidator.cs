using Aw.Evidence.Core;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Ids;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.ApiRun;

internal sealed class ApiRunValidator : IValidator<ApiRunRequest>
{
    public Result<Unit> Validate(ApiRunRequest value)
    {
        string?[] optional = [value.ConfigPath];
        if (!IsAbsolute(value.ScriptPath) || !IsAbsolute(value.RunDirectory) || !IsAbsolute(value.Repo) || optional.Any(static p => p is not null && !IsAbsolute(p)))
        {
            return EvidenceErrors.Invalid.With("script, run directory, repo and config paths must be absolute");
        }

        if (!RunId.TryFrom(RunPath.NameOf(value.RunDirectory), out _))
        {
            return EvidenceErrors.Invalid.With("the run directory needs a name (it is the run id)");
        }

        return value.LockTimeout < TimeSpan.Zero ? EvidenceErrors.Invalid.With("the lock timeout must not be negative") : Unit.Value;
    }

    private static bool IsAbsolute(string path) => path.StartsWith('/');
}
