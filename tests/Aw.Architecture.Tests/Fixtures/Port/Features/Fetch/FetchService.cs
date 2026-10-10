namespace Aw.Architecture.Tests.Fixtures.Port.Features.Fetch;

internal sealed class FetchService(HttpClient http)
{
    public Uri? Base => http.BaseAddress;
}
