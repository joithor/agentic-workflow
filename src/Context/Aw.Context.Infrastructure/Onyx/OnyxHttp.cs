namespace Aw.Context.Infrastructure.Onyx;

/// <summary>
/// The one HttpClient Context uses for Onyx: no proxy and no redirects, so a call goes only to the profile's loopback
/// Onyx URL and never on to an address a reply names (spec §14 S-L2). Static, as HttpClient is meant to be shared.
/// </summary>
internal static class OnyxHttp
{
    public static HttpClient Client { get; } = new(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false });
}
