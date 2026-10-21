using Aw.Kernel;
using Aw.Sindri.Ui.Api.Setup;

namespace Aw.Sindri.Ui.Api;

/// <summary>The setup read (<see cref="SindriApiClient"/>, continued; setup-once spec §7).</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<SetupView>> Setup(Uri api, CancellationToken cancellationToken) => Get<SetupView>(api, SetupRoutes.Read, cancellationToken, view: true);

    /// <inheritdoc />
    public Task<Result<JobStarted>> AddRepo(Uri api, AddRepoRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<JobStarted>> RegisterMcp(Uri api, RegisterMcpRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<JobStarted>> SetupOnyx(Uri api, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<JobStarted>> ConnectOnyx(Uri api, OnyxLogin login, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<OnyxDeferView>> DeferOnyx(Uri api, OnyxDeferRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
