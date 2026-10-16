using Aw.Evidence.Core.Data;
using Aw.Evidence.Testing;

namespace Aw.Evidence.Tests.Data;

public sealed class RecordedTextTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_json_object_with_a_duplicate_key_is_recorded_as_text_not_a_crash()
    {
        var scrubber = new FakeTextScrubber().HidesName("password");

        var recorded = RecordedText.Of(scrubber, """[{"id":1,"id":2,"password":"pw-1234567"}]""", RecordedText.OutputLimit);

        Assert.Contains("\"id\":2", recorded, StringComparison.Ordinal);
    }
}
