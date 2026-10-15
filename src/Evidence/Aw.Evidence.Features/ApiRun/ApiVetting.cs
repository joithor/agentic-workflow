using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Settings;
using Aw.Kernel;

namespace Aw.Evidence.Features.ApiRun;

/// <summary>The guards that run before any lock: the host, the header variables and the golden files.</summary>
internal sealed class ApiVetting(IEvidenceSettings settings, ISecretsSource secrets, ApiRunFiles files)
{
    internal IEvidenceSettings Settings { get; } = settings;

    internal ISecretsSource Secrets { get; } = secrets;

    internal ApiRunFiles Files { get; } = files;

    internal ValueTask<Result<VettedRun>> VetAsync(ApiRunRequest request, ApiScript script, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
