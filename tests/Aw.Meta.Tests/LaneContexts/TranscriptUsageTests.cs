using Aw.Meta.Core.Lanes;

namespace Aw.Meta.Tests.LaneContexts;

public sealed class TranscriptUsageTests
{
    private static string Usage(string inner) => "{\"type\":\"assistant\",\"message\":{\"usage\":{" + inner + "}}}";

    [Fact, Trait("Lane", "unit")]
    public void The_context_is_input_plus_cache_read_plus_cache_creation_of_the_last_usage_line()
    {
        var lines = new[]
        {
            Usage("\"input_tokens\":10,\"cache_read_input_tokens\":1000,\"cache_creation_input_tokens\":100"),
            "{\"type\":\"user\"}",
            Usage("\"input_tokens\":3,\"cache_read_input_tokens\":400000,\"cache_creation_input_tokens\":87000,\"output_tokens\":900"),
            "{\"type\":\"user\",\"text\":\"later\"}",
        };

        Assert.Equal(487_003, TranscriptUsage.ContextTokens(lines));
    }

    [Fact, Trait("Lane", "unit")]
    public void Absent_cache_fields_count_as_zero() => Assert.Equal(42, TranscriptUsage.ContextTokens([Usage("\"input_tokens\":42")]));

    [Fact, Trait("Lane", "unit")]
    public void A_transcript_without_usage_has_no_context()
    {
        Assert.Null(TranscriptUsage.ContextTokens(["{\"type\":\"user\"}", "{\"type\":\"assistant\",\"message\":{}}"]));
        Assert.Null(TranscriptUsage.ContextTokens([]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_malformed_last_line_falls_back_to_the_previous_usage_line()
    {
        var lines = new[] { Usage("\"input_tokens\":7000"), "{\"message\":{\"usage\":{\"input_tokens\":99" };

        Assert.Equal(7000, TranscriptUsage.ContextTokens(lines));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_synthetic_all_zero_usage_line_is_skipped()
    {
        var lines = new[] { Usage("\"input_tokens\":5000"), Usage("\"input_tokens\":0,\"output_tokens\":0") };

        Assert.Equal(5000, TranscriptUsage.ContextTokens(lines));
    }
}
