using Aw.Context.Core;
using Aw.Kernel;

namespace Aw.Context.Tests;

public sealed class ContextErrorsTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_catalogue_is_valid() => Assert.True(ErrorCatalogue.Validate(ContextErrors.All).IsSuccess);

    [Fact, Trait("Lane", "unit")]
    public void S4_allocates_040_and_042_to_048_and_leaves_041_retired_and_049_reserved()
    {
        var codes = ContextErrors.All.Select(e => e.Code).Where(c => string.CompareOrdinal(c, "SND-CONTEXT-040") >= 0).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(
            ["SND-CONTEXT-040", "SND-CONTEXT-042", "SND-CONTEXT-043", "SND-CONTEXT-044", "SND-CONTEXT-045", "SND-CONTEXT-046", "SND-CONTEXT-047", "SND-CONTEXT-048"],
            codes);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_heavy_lock_and_an_unreadable_repo_are_tool_errors_and_the_rest_of_S4_are_attention()
    {
        Assert.DoesNotContain(ContextErrors.HeavyLockBusy.Code, ContextErrors.Attention);
        Assert.DoesNotContain(ContextErrors.ExtractionUnreadable.Code, ContextErrors.Attention);
        ErrorDefinition[] attention =
            [ContextErrors.LabelingUnavailable, ContextErrors.ReviewFileNoMatch, ContextErrors.NoDraftMap, ContextErrors.RepoOutsideScope, ContextErrors.PromoteRefused, ContextErrors.ReviewTargetStale];
        Assert.All(attention, e => Assert.Contains(e.Code, ContextErrors.Attention));
    }
}
