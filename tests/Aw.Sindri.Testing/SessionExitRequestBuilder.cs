using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.SessionExit;

namespace Aw.Sindri.Testing;

/// <summary>Builds a SessionExitRequest with deterministic defaults.</summary>
public sealed class SessionExitRequestBuilder
{
    private LaunchId _launchId = A.FirstLaunchId;

    /// <summary>Sets LaunchId.</summary>
    /// <param name="value">The id.</param>
    /// <returns>This builder.</returns>
    public SessionExitRequestBuilder WithLaunchId(LaunchId value)
    {
        _launchId = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public SessionExitRequest Build() => new(_launchId);
}
