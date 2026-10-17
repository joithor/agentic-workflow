using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ResumeService;

namespace Aw.Sindri.Testing;

/// <summary>Builds a ResumeServiceRequest with deterministic defaults: the heartbeat service.</summary>
public sealed class ResumeServiceRequestBuilder
{
    private ServiceName _name = ServiceName.From("heartbeat");

    /// <summary>Sets the service.</summary>
    /// <param name="value">The service.</param>
    /// <returns>This builder.</returns>
    public ResumeServiceRequestBuilder ForService(ServiceName value)
    {
        _name = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public ResumeServiceRequest Build() => new(_name);
}
