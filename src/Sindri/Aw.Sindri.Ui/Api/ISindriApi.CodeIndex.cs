using Aw.Kernel;
using Aw.Sindri.Ui.Api.CodeIndex;

namespace Aw.Sindri.Ui.Api;

/// <summary>The code index's read (<see cref="ISindriApi"/>, continued).</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/code-index/status: each repo's index state for the Health card. A query; it never carries X-Sindri-After.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The repos' indexes, or the refusal.</returns>
    public Task<Result<CodeIndexStatusView>> CodeIndexStatus(Uri api, CancellationToken cancellationToken);
}
