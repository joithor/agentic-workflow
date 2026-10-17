using Aw.Forge.Features.ParseForgeLink;

namespace Aw.Forge.Testing;

/// <summary>Builds a ParseForgeLinkRequest with deterministic defaults.</summary>
public sealed class ParseForgeLinkRequestBuilder
{
    private readonly ParseForgeLinkRequest _request = new();

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public ParseForgeLinkRequest Build() => _request;
}
