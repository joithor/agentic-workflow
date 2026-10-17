using Aw.Context.Core;
using Aw.Context.Core.Results;
using Aw.Context.Core.Review;

namespace Aw.Context.Tests.Review;

public sealed class PromoteReadinessTests
{
    private static ReviewEntry Entry() =>
        new(SampleDrafts.Key("feature:a"), Link: null, ReviewGroup.Model, "A", Provenance.Model, 0.5, 1, [], Parent: null);

    [Fact, Trait("Lane", "unit")]
    public void No_draft_is_046()
    {
        var ready = PromoteReadiness.Check(queue: null);

        Assert.Equal(ContextErrors.PromoteRefused.Code, ready.Error.Code);
        Assert.Contains("no draft", ready.Error.Message, StringComparison.Ordinal);
        Assert.Contains("context map propose <file>", ready.Error.Message, StringComparison.Ordinal);
        Assert.Contains("context map build", ready.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_draft_with_undecided_items_is_046_and_names_how_many()
    {
        var ready = PromoteReadiness.Check(ReviewQueueView.Of("v14@7c3e1a2", [Entry(), Entry()], decided: 3));

        Assert.Equal(ContextErrors.PromoteRefused.Code, ready.Error.Code);
        Assert.Contains("2 undecided", ready.Error.Message, StringComparison.Ordinal);
        Assert.Contains("v14@7c3e1a2", ready.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_fully_decided_draft_is_ready()
    {
        var queue = ReviewQueueView.Of("v14@7c3e1a2", [], decided: 5);

        Assert.Same(queue, PromoteReadiness.Check(queue).Value);
    }
}
