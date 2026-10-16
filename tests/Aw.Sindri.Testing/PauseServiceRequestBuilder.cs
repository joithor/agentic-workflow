using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.PauseService;

namespace Aw.Sindri.Testing;

/// <summary>Builds a PauseServiceRequest with deterministic defaults: the heartbeat service.</summary>
public sealed class PauseServiceRequestBuilder
{
    private ServiceName _name = ServiceName.From("heartbeat");

    /// <summary>Sets the service.</summary>
    /// <param name="value">The service.</param>
    /// <returns>This builder.</returns>
    public PauseServiceRequestBuilder ForService(ServiceName value)
    {
        _name = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public PauseServiceRequest Build() => new(_name);
}
