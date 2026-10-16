using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.RunService;

namespace Aw.Sindri.Testing;

/// <summary>Builds a RunServiceRequest with deterministic defaults: the heartbeat service.</summary>
public sealed class RunServiceRequestBuilder
{
    private ServiceName _name = ServiceName.From("heartbeat");

    /// <summary>Sets the service.</summary>
    /// <param name="value">The service.</param>
    /// <returns>This builder.</returns>
    public RunServiceRequestBuilder ForService(ServiceName value)
    {
        _name = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public RunServiceRequest Build() => new(_name);
}
