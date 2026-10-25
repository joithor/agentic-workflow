using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>The concept card's top terms and the glossary (contract provisional, additive): terms show on the card, count in the concept search, and an older server's answer is unchanged.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextConceptTermsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ContextConceptsView One(params string[]? terms) =>
        AConcepts.Ok([AConcepts.Concept("concept:scheduling", "Self scheduling", "People book slots.", terms: terms)]);

    [Fact]
    public async Task The_card_shows_the_first_three_terms_and_how_many_more_there_are()
    {
        var page = await ConceptPage.Shown(ConceptPage.Serving(One("shift", "swap", "roster", "rota", "leave")));

        Expect.Equal(("Terms: shift · swap · roster +2", true), (page.Concepts.Tiles[0].TermsText, page.Concepts.Tiles[0].HasTerms));
    }

    [Fact]
    public async Task Three_terms_or_fewer_show_all_of_them_with_no_count()
    {
        var page = await ConceptPage.Shown(ConceptPage.Serving(One("shift", "swap")));

        Expect.Equal("Terms: shift · swap", page.Concepts.Tiles[0].TermsText);
    }

    [Fact]
    public async Task A_concept_without_terms_shows_none()
    {
        var page = await ConceptPage.Shown(ConceptPage.Serving(One()));

        Expect.Equal((string.Empty, false), (page.Concepts.Tiles[0].TermsText, page.Concepts.Tiles[0].HasTerms));
    }

    [Fact]
    public async Task An_older_servers_answer_with_no_terms_or_glossary_field_reads_as_before()
    {
        const string body = """
            {"apiVersion":1,"repo":"r","mapVersion":"v1","state":"ok",
             "concepts":[{"id":"concept:a","name":"Alpha","summary":"First.","source":"derived","size":{"features":1,"records":0,"decisions":0,"code":2},"citations":[]}],
             "other":{"id":"concept:other","name":"Other","size":{"features":0,"records":0,"decisions":0,"code":0}},"links":[]}
            """;
        var api = new FakeSindriApi { ContextConceptsAnswer = _ => new RouteAnswer(HttpStatusCode.OK, body) };

        var page = await ConceptPage.Shown(api);

        Expect.Equal(("Alpha", false, 0), (page.Concepts.Tiles[0].Name, page.Concepts.Tiles[0].HasTerms, page.Concepts.Glossary.Count));
    }

    [Fact]
    public async Task A_term_that_matches_keeps_the_concept_like_a_name_or_summary_match()
    {
        var view = AConcepts.Ok(
            [AConcepts.Concept("concept:scheduling", "Self scheduling", "People book slots.", terms: ["rotation"]), AConcepts.Concept("concept:billing", "Billing", "Invoices.")],
            links: []);
        var page = await ConceptPage.Shown(ConceptPage.Serving(view));

        page.AnchorText = "rotation";
        await page.Submit(Token);

        Expect.Sequence(["Self scheduling"], page.Concepts.Tiles.Select(t => t.Name));
    }

    [Fact]
    public async Task The_glossary_is_held_with_its_optional_fields_tolerated()
    {
        var view = AConcepts.WithGlossary(One("shift"), new ConceptGlossaryEntry("shift", "A block of work.", ["concept:scheduling"]), new ConceptGlossaryEntry("rota"));
        var page = await ConceptPage.Shown(ConceptPage.Serving(view));

        Expect.Sequence(["shift", "rota"], page.Concepts.Glossary.Select(g => g.Term));
        Expect.Equal(("A block of work.", null), (page.Concepts.Glossary[0].Definition, page.Concepts.Glossary[1].Definition));
    }
}
