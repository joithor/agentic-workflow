using Aw.Evidence.Core.Data;
using Aw.Evidence.Testing;

namespace Aw.Evidence.Tests.Data;

public sealed class RecordedTextTests
{
    private static FakeTextScrubber Scrubber() => new FakeTextScrubber().HidesName("password").HidesName("password_hash").HidesName("api_tokens");

    [Fact, Trait("Lane", "unit")]
    public void A_json_object_with_a_duplicate_key_keeps_its_ordinary_members_and_hides_the_secret_one()
    {
        var recorded = RecordedText.Of(Scrubber(), """[{"id":1,"id":2,"password":"pw-1234567"}]""", RecordedText.OutputLimit);

        Assert.Contains("\"id\":2", recorded, StringComparison.Ordinal);
        Assert.DoesNotContain("pw-1234567", recorded, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_body_with_a_repeated_key_hides_every_secret_named_member_wherever_it_is()
    {
        const string Body = """{"a":1,"a":2,"password_hash":9876543,"api_tokens":["tok-AAA111","tok-BBB222"],"nested":{"x":1,"x":2,"password":"pw-PLANTED-1"},"list":[{"password_hash":"hash-PLANTED-2","k":1,"k":2}],"password":"pw-PLANTED-3"}""";

        var recorded = RecordedText.Of(Scrubber(), Body, RecordedText.OutputLimit);

        foreach (var value in new[] { "9876543", "tok-AAA111", "tok-BBB222", "pw-PLANTED-1", "hash-PLANTED-2", "pw-PLANTED-3" })
        {
            Assert.DoesNotContain(value, recorded, StringComparison.Ordinal);
        }

        Assert.Contains("\"a\":2", recorded, StringComparison.Ordinal);
        Assert.Contains("\"x\":2", recorded, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Text_that_is_not_json_is_still_scrubbed_by_value()
    {
        var recorded = RecordedText.Of(new FakeTextScrubber().Hides("pw-1234567"), "{not json pw-1234567", RecordedText.OutputLimit);

        Assert.DoesNotContain("pw-1234567", recorded, StringComparison.Ordinal);
    }
}
