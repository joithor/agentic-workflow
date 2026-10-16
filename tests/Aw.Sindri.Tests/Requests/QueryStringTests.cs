using Aw.Kernel.Testing;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Requests;

/// <summary>The query string the item, request, decision and context verbs send.</summary>
[Lane(TestLane.Unit)]
public sealed class QueryStringTests
{
    [Fact]
    public void No_value_is_no_query() => Expect.Equal(string.Empty, QueryString.Of([("state", null), ("item", null)]));

    [Fact]
    public void The_first_pair_starts_with_a_question_mark_and_the_rest_with_an_ampersand() =>
        Expect.Equal("?state=open&item=ITM-1", QueryString.Of([("state", "open"), ("repo", null), ("item", "ITM-1")]));

    [Fact]
    public void Values_are_percent_encoded() =>
        Expect.Equal("?q=a%20b%26c%3D%C3%A5", QueryString.Of([("q", "a b&c=\u00e5")]));
}
