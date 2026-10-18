using Aw.Meta.Core.Coordination;

namespace Aw.Meta.Tests.LaneContexts;

public sealed class PaneSubmitterTests
{
    private const string Rule = "────────────";

    [Fact, Trait("Lane", "unit")]
    public void Text_left_in_the_input_box_means_the_enter_was_swallowed()
    {
        var capture = $"output\n{Rule}\n❯ Read and continue from /home/x/digests/t1-api-2026-10-17.md\n{Rule}\n";

        Assert.True(PaneSubmitter.InputHolds(capture, "Read and continue from /home/x/digests/t1-api-2026-10-17.md"));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_empty_input_box_or_text_above_the_rule_means_it_was_submitted()
    {
        var capture = $"❯ Read and continue from /home/x/digests/t1-api-2026-10-17.md\n● working\n{Rule}\n❯ \n{Rule}\n";

        Assert.False(PaneSubmitter.InputHolds(capture, "Read and continue from /home/x/digests/t1-api-2026-10-17.md"));
        Assert.False(PaneSubmitter.InputHolds("no prompt at all", "Read and continue"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_wrapped_line_in_the_input_box_is_still_found()
    {
        var capture = $"{Rule}\n❯ Read and continue from /home/x/di\n  gests/t1-api-2026-10-17.md\n{Rule}\n";

        Assert.True(PaneSubmitter.InputHolds(capture, "Read and continue from /home/x/digests/t1-api-2026-10-17.md"));
    }
}
