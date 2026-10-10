using Aw.Evidence.Features.RunScript;

namespace Aw.Evidence.Testing;

/// <summary>Builds a RunScriptRequest with deterministic defaults.</summary>
public sealed class RunScriptRequestBuilder
{
    private readonly RunScriptRequest _request = new();

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public RunScriptRequest Build() => _request;
}
