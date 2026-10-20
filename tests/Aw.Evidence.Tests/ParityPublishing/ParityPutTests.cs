using Aw.Evidence.Features.ParityPut;
using Aw.Evidence.Testing;

namespace Aw.Evidence.Tests.ParityPublishing;

public sealed class ParityPutTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task ParityPut_builds_curl_with_every_signed_header_verbatim()
    {
        var files = new InMemoryRunFiles().With("/work/upload.json", TestJson.Utf8(
            """{"url":"https://uploads.example/put?sig=1","headers":{"Content-Type":"image/png","x-goog-meta":"a b"}}"""));

        var result = await new ParityPutService(files, TestJson.Options).Handle(new("/work/upload.json", "/run/p/a.png"), CancellationToken.None);

        Assert.Equal(
            ["curl", "-sS", "--fail", "-X", "PUT", "--data-binary", "@/runs/p/a.png", "-H", "Content-Type: image/png", "-H", "x-goog-meta: a b", "https://uploads.example/put?sig=1"],
            result.Value.Argv,
            StringComparer.Ordinal);
    }
}
