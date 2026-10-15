namespace Aw.Context.Tests.Onyx;

/// <summary>A request the scripted handler saw.</summary>
/// <param name="Method">The method.</param>
/// <param name="Uri">The full URI.</param>
/// <param name="HadAuthorization">Whether it carried an Authorization header.</param>
internal sealed record SeenRequest(HttpMethod Method, Uri? Uri, bool HadAuthorization);
