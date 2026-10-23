using Aw.Kernel;
using Aw.Sindri.Ui.Api.CodeIndex;

namespace Aw.Sindri.Ui.Api;

/// <summary>The code index's read (<see cref="SindriApiClient"/>, continued).</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<CodeIndexStatusView>> CodeIndexStatus(Uri api, CancellationToken cancellationToken) =>
        Get<CodeIndexStatusView>(api, "/sindri/code-index/status", cancellationToken);
}
