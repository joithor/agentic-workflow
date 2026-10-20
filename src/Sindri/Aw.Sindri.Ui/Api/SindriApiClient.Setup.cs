using Aw.Kernel;
using Aw.Sindri.Ui.Api.Setup;

namespace Aw.Sindri.Ui.Api;

/// <summary>The setup read (<see cref="SindriApiClient"/>, continued; setup-once spec §7).</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<SetupView>> Setup(Uri api, CancellationToken cancellationToken) => throw new NotSupportedException(SetupRoutes.Read);
}
