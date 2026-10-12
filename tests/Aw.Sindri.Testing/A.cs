namespace Aw.Sindri.Testing;

/// <summary>The builder root: A.&lt;Slice&gt;Request() and the like, with deterministic defaults (spec §4b.5).</summary>
public static class A
{
    /// <summary>A caller context builder: a session over HTTP unless told otherwise.</summary>
    /// <returns>A new builder.</returns>
    public static CallerContextBuilder Caller() => new();

    /// <summary>A ListCommandsRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static ListCommandsRequestBuilder ListCommandsRequest() => new();

    /// <summary>A ListServicesRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static ListServicesRequestBuilder ListServicesRequest() => new();

    /// <summary>A GetProfileRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static GetProfileRequestBuilder GetProfileRequest() => new();

    /// <summary>A RebuildViewsRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static RebuildViewsRequestBuilder RebuildViewsRequest() => new();
}
