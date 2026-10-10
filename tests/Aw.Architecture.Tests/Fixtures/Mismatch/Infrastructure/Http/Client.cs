namespace Aw.Architecture.Tests.Fixtures.Mismatch.Infrastructure.Http;

// Declared in an Infrastructure namespace, but (in the test's scope) compiled into the package's Core assembly.
internal sealed class Client(HttpClient http)
{
    public Uri? Base => http.BaseAddress;
}
