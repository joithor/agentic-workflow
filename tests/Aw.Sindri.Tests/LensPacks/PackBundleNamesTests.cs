using Aw.Sindri.Core.LensPacks;

namespace Aw.Sindri.Tests.LensPacks;

/// <summary>LN-WF5: which Step takes which bundle (line spec §5.1, §5.2).</summary>
public sealed class PackBundleNamesTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("step.design.draft", "style")]
    [InlineData("step.design.apply", "style")]
    [InlineData("step.design.revise", "style")]
    [InlineData("step.lens.run", "lenses")]
    public void A_Step_may_take_the_pack_it_works_from(string block, string bundle) =>
        Assert.Null(PackBundleNames.Problem(block, bundle));

    [Theory, Trait("Lane", "unit")]
    [InlineData("step.lens.run", "style", "can only bundle 'lenses', not 'style'")]
    [InlineData("step.design.draft", "lenses", "can only bundle 'style', not 'lenses'")]
    [InlineData("step.design.draft", "other", "takes 'style' or 'lenses', not 'other'")]
    public void A_Step_may_not_take_the_other_pack_or_a_word_that_names_none(string block, string bundle, string problem) =>
        Assert.Equal(problem, PackBundleNames.Problem(block, bundle));

    [Theory, Trait("Lane", "unit")]
    [InlineData("style", "style")]
    [InlineData("lenses", "lens")]
    [InlineData("other", null)]
    public void A_bundle_lands_in_its_own_folder(string bundle, string? folder) =>
        Assert.Equal(folder, PackBundleNames.DirectoryOf(bundle));
}
