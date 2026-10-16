using Aw.Evidence.Core.Data;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class FakeApiTransportContractTests : ApiTransportContract
{
    private const string Origin = "http://fake.test";

    protected override Task<ApiUnderTest> ArrangeAsync(IReadOnlyDictionary<string, HttpReply> routes)
    {
        var fake = new FakeApiTransport();
        foreach (var (key, reply) in routes)
        {
            var space = key.IndexOf(' ', StringComparison.Ordinal);
            _ = fake.Reply(key[..space], Origin + key[(space + 1)..], reply);
        }

        return Task.FromResult(new ApiUnderTest(fake, Origin, () => [.. fake.Calls.Select(Describe)]));
    }

    protected override Task<ApiUnderTest> ArrangeUnreachableAsync()
    {
        var fake = new FakeApiTransport().Unreachable(Origin + "/notes");
        return Task.FromResult(new ApiUnderTest(fake, Origin, () => []));
    }

    private static string Describe(HttpCall call) =>
        $"{call.Method} {call.Url} {call.Body} {string.Join(' ', call.Headers.Select(static h => $"{h.Key}={h.Value}"))}";
}
