using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Tests.GetInstance;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.Layout;

/// <summary>What an instance is about is named by the kind of the first input's VALUE (API spec §3.8 'subject'): an item by its id, a link by its tracker key or URL, a file by its name, a text by its first line, a payload by its id and title.</summary>
[Lane(TestLane.Unit)]
public sealed class SubjectRefsTests
{
    private static readonly FactKey _fact = FactKey.From("fact:in");
    private static readonly ItemKey _item = ItemKey.From("ITM-01JL2");

    private static InterpretedRecipe Recipe(string? type, string? kind = null, string? name = null) =>
        StatusCases.Recipe with
        {
            Recipe = EngineFixture.Step with { Inputs = [new RecipeInput(_fact, "Input", type, kind ?? type)] },
            Inputs = name is null ? [] : new Dictionary<FactKey, InputFact> { [_fact] = new(name, EngineFixture.Fact("x")) },
        };

    [Fact]
    public void An_item_is_its_id_for_the_key_and_the_label() => Assert.Equal(new SubjectRef("item", "ITM-01JL2", "ITM-01JL2", Href: null), SubjectRefs.Of(Recipe("item"), _item));

    [Fact]
    public void A_file_is_its_name_for_the_key_and_the_label() => Assert.Equal(new SubjectRef("file", "spec.md", "spec.md", Href: null), SubjectRefs.Of(Recipe("file", name: "spec.md"), _item));

    [Theory]
    [InlineData("Summarize the week\nand then some", "Summarize the week", "Summarize the week")]
    [InlineData("\n\n  second line after blanks \nthird", "second line after blanks", "second line after blanks")]
    public void A_text_is_its_first_line(string content, string key, string label) => Assert.Equal((key, label), SubjectRefs.Of(Recipe("text"), _item, content) is { } subject ? (subject.Key, subject.Label) : default);

    [Fact]
    public void A_long_first_line_is_cut_to_40_for_the_key_and_80_for_the_label()
    {
        var line = new string('a', 100);

        var subject = SubjectRefs.Of(Recipe("text"), _item, line)!;

        Assert.Equal((new string('a', 40), new string('a', 80)), (subject.Key, subject.Label));
    }

    [Fact]
    public void A_cut_never_splits_a_surrogate_pair()
    {
        var line = new string('a', 39) + "\U0001F600" + "tail";   // the emoji's two units sit at 39 and 40, across the key's cut at 40

        var subject = SubjectRefs.Of(Recipe("text"), _item, line)!;

        Assert.Equal(new string('a', 39), subject.Key);
        Assert.Equal(line, subject.Label);
    }

    [Theory]
    [InlineData("https://linear.app/acme/issue/ENG-142/crash-on-launch", "ENG-142", "ENG-142")]
    [InlineData("https://example.com/docs/page", "https://example.com/docs/page", "https://example.com/docs/page")]
    [InlineData("""{"url":"https://example.com/p/9","title":"Design brief"}""", "https://example.com/p/9", "Design brief")]
    [InlineData("""{"url":"https://example.com/p/9","key":"APP-7","title":"Design brief"}""", "APP-7", "Design brief")]
    public void A_link_is_its_tracker_key_else_its_url_and_labelled_by_its_title_else_its_key(string content, string key, string label)
    {
        var subject = SubjectRefs.Of(Recipe("link"), _item, content)!;

        Assert.Equal((key, label), (subject.Key, subject.Label));
    }

    [Theory]
    [InlineData("""{"id":"P-2211","title":"Outbox retry storm"}""", "P-2211", "Outbox retry storm")]
    [InlineData("""{"number":8812,"summary":"TypeError in tabs.layout"}""", "8812", "TypeError in tabs.layout")]
    [InlineData("""{"other":1}""", "fact:in", "fact:in")]
    [InlineData("not json", "fact:in", "fact:in")]
    public void A_payload_is_its_id_or_number_and_its_title_or_summary_else_the_fact_key(string content, string key, string label)
    {
        var subject = SubjectRefs.Of(Recipe("payload", kind: "webhook"), _item, content)!;

        Assert.Equal(("webhook", key, label), (subject.Kind, subject.Key, subject.Label));
    }

    [Theory]
    [InlineData("link")]
    [InlineData("payload")]
    public void A_first_input_with_a_type_and_no_declared_kind_has_a_null_subject_kind_equal_to_the_input_kind(string type)
    {
        var recipe = StatusCases.Recipe with { Recipe = EngineFixture.Step with { Inputs = [new RecipeInput(_fact, "Input", type, Kind: null)] } };
        var view = InstanceViews.Of(EngineFixture.At(recipe.Recipe, "produce"), recipe, StatusCases.Now, timeoutAt: null, new InstanceViewContext());

        var subject = SubjectRefs.Of(recipe, _item, """{"id":"P-1","title":"A title"}""")!;

        Assert.Null(subject.Kind);
        Assert.Equal(Assert.Single(view.Inputs).Kind, subject.Kind);
        Assert.Equal(("P-1", "A title"), (subject.Key, subject.Label));
    }

    [Fact]
    public void Content_that_was_not_read_leaves_a_text_named_by_its_fact_key_and_file_name()
    {
        var subject = SubjectRefs.Of(Recipe("text", name: "request.txt"), _item)!;

        Assert.Equal(("fact:in", "request.txt"), (subject.Key, subject.Label));
    }

    [Theory]
    [InlineData("text", true)]
    [InlineData("link", true)]
    [InlineData("payload", true)]
    [InlineData("item", false)]
    [InlineData("file", false)]
    public void Only_a_text_a_link_and_a_payload_need_their_content_read(string type, bool needs) => Assert.Equal(needs, SubjectRefs.NeedsContent(Recipe(type)));

    [Fact]
    public void A_recipe_without_an_input_has_no_subject() => Assert.Null(SubjectRefs.Of(StatusCases.Recipe with { Recipe = EngineFixture.Step with { Inputs = [] } }, _item));

    [Fact]
    public void An_undeclared_type_is_the_declared_default_text_in_the_subject_the_slot_and_the_fact_row()
    {
        var recipe = Recipe(type: null);
        var view = InstanceViews.Of(EngineFixture.At(recipe.Recipe, "produce"), recipe, StatusCases.Now, timeoutAt: null, new InstanceViewContext());

        Assert.Equal("text", view.Subject?.Kind);
        Assert.Equal("text", Assert.Single(view.Inputs).Kind);
        Assert.Equal(FactType.Text, view.Facts.First(f => f.Direction == FactDirection.Input).Type);
        Assert.True(SubjectRefs.NeedsContent(recipe));
        Assert.Equal("first line", SubjectRefs.Of(recipe, _item, "first line\nsecond")?.Key);
    }
}
