using Aw.Kernel;
using Aw.Sindri.Ui.Api.Setup;

namespace Aw.Sindri.Ui.Api;

/// <summary>The setup read (<see cref="ISindriApi"/>, continued; setup-once spec §7): where "set up Sindri once" stands.</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/setup: the six steps with their states. A pure read: it starts nothing. A source that can't be read fails only its own step.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The steps, the API's refusal (a 404 from a Sindri before setup), or SND-UI-002/003.</returns>
    public Task<Result<SetupView>> Setup(Uri api, CancellationToken cancellationToken);
}
