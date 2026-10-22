namespace Aw.Context.Infrastructure.Linear;

/// <summary>The one HttpClient Context uses for Linear: no proxy and no redirects, so a call goes only to the profile's Linear URL and never on to an address a reply names (spec §14 S-L2).</summary>
internal static class LinearHttp
{
    public static HttpClient Client { get; } = new(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false });
}
