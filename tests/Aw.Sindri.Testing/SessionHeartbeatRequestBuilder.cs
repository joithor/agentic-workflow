using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.SessionHeartbeat;

namespace Aw.Sindri.Testing;

/// <summary>Builds a SessionHeartbeatRequest with deterministic defaults.</summary>
public sealed class SessionHeartbeatRequestBuilder
{
    private LaunchId _launchId = A.FirstLaunchId;

    /// <summary>Sets LaunchId.</summary>
    /// <param name="value">The id.</param>
    /// <returns>This builder.</returns>
    public SessionHeartbeatRequestBuilder WithLaunchId(LaunchId value)
    {
        _launchId = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public SessionHeartbeatRequest Build() => new(_launchId);
}
