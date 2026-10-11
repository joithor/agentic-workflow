using System.Net;
using System.Text.Json.Nodes;
using Aw.Context.Testing.Fakes;
using Aw.Kernel.Testing;

namespace Aw.Context.Testing.Tests;

/// <summary>The fake Linear GraphQL server replays the recorded project on loopback (spec §10, §14 S-L2, D18).</summary>
[Lane(TestLane.Integration)]
public sealed class FakeLinearServerTests
{
    private const string IssuesQuery =
        "query Issues($filter: IssueFilter, $first: Int, $after: String, $orderBy: PaginationOrderBy) { " +
        "issues(filter: $filter, first: $first, after: $after, orderBy: $orderBy) { " +
        "nodes { id identifier updatedAt comments { nodes { body } } attachments { nodes { url } } } " +
        "pageInfo { hasNextPage endCursor } } }";

    [Fact]
    public async Task Issues_replay_every_recorded_issue_with_comments_and_pull_request_attachments()
    {
        await using var linear = await FakeLinearServer.Start();

        var issues = await Issues(linear, []);

        Assert.Equal(["SPL-101", "SPL-102", "SPL-103", "SPL-104", "SPL-105", "SPL-106"], Identifiers(issues));
        Assert.Equal(3, issues["nodes"]![4]!["comments"]!["nodes"]!.AsArray().Count);
        Assert.EndsWith("/pull/5", (string?)issues["nodes"]![4]!["attachments"]!["nodes"]![0]!["url"], StringComparison.Ordinal);
        Assert.False((bool)issues["pageInfo"]!["hasNextPage"]!);
    }

    [Fact]
    public async Task Issues_filter_by_team_key_and_updated_at()
    {
        await using var linear = await FakeLinearServer.Start();
        var filter = new JsonObject
        {
            ["team"] = new JsonObject { ["key"] = new JsonObject { ["eq"] = "SPL" } },
            ["updatedAt"] = new JsonObject { ["gt"] = "2026-07-22T10:20:00.000Z" },
        };

        Assert.Equal(["SPL-105", "SPL-106"], Identifiers(await Issues(linear, new JsonObject { ["filter"] = filter.DeepClone() })));

        filter["team"]!["key"]!["eq"] = "OTHER";
        Assert.Empty(Identifiers(await Issues(linear, new JsonObject { ["filter"] = filter })));
    }

    [Fact]
    public async Task Issues_page_with_first_and_after_in_updated_at_order()
    {
        await using var linear = await FakeLinearServer.Start();

        var first = await Issues(linear, new JsonObject { ["first"] = 4, ["orderBy"] = "updatedAt" });
        var rest = await Issues(linear, new JsonObject { ["first"] = 4, ["orderBy"] = "updatedAt", ["after"] = (string?)first["pageInfo"]!["endCursor"] });

        Assert.Equal(["SPL-101", "SPL-102", "SPL-103", "SPL-104"], Identifiers(first));
        Assert.True((bool)first["pageInfo"]!["hasNextPage"]!);
        Assert.Equal(["SPL-105", "SPL-106"], Identifiers(rest));
        Assert.False((bool)rest["pageInfo"]!["hasNextPage"]!);
    }

    [Fact]
    public async Task Issue_is_found_by_uuid_or_identifier_and_a_missing_one_is_a_graphql_error()
    {
        await using var linear = await FakeLinearServer.Start();
        const string query = "query One($id: String!) { issue(id: $id) { id identifier } }";

        var (Status, Body) = await Post(linear, query, new JsonObject { ["id"] = "SPL-104" });
        var byId = await Post(linear, query, new JsonObject { ["id"] = (string?)Body!["data"]!["issue"]!["id"] });
        var missing = await Post(linear, "{ issue(id: \"SPL-999\") { id } }", variables: null);

        Assert.Equal("SPL-104", (string?)byId.Body!["data"]!["issue"]!["identifier"]);
        Assert.Equal(HttpStatusCode.OK, missing.Status);
        Assert.Contains("SPL-999", (string?)missing.Body!["errors"]![0]!["message"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Projects_documents_and_project_updates_are_served()
    {
        await using var linear = await FakeLinearServer.Start();
        const string query = "{ projects { nodes { id } } documents { nodes { title project { id } } } projectUpdates(first: 1) { nodes { body } pageInfo { hasNextPage } } }";

        var data = (await Post(linear, query, variables: null)).Body!["data"]!;

        _ = Assert.Single(data["projects"]!["nodes"]!.AsArray());
        Assert.Equal("PRD: Split panes", (string?)data["documents"]!["nodes"]![0]!["title"]);
        Assert.True((bool)data["projectUpdates"]!["pageInfo"]!["hasNextPage"]!);
    }

    [Fact]
    public async Task An_unknown_root_field_or_a_mutation_is_a_clear_400()
    {
        await using var linear = await FakeLinearServer.Start();

        var (Status, Body) = await Post(linear, "{ viewer { id } }", variables: null);
        var mutation = await Post(linear, "mutation { issueCreate(input: {}) { success } }", variables: null);

        Assert.Equal(HttpStatusCode.BadRequest, Status);
        var message = (string?)Body!["errors"]![0]!["message"];
        Assert.Contains("\"viewer\"", message, StringComparison.Ordinal);
        Assert.Contains("issues", message, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, mutation.Status);
        Assert.Contains("read-only", (string?)mutation.Body!["errors"]![0]!["message"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_filter_comparing_a_date_with_a_non_date_is_a_clear_400()
    {
        await using var linear = await FakeLinearServer.Start();
        var filter = new JsonObject { ["updatedAt"] = new JsonObject { ["gt"] = "P1W" } };

        var (status, body) = await Post(linear, IssuesQuery, new JsonObject { ["filter"] = filter });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("P1W", (string?)body!["errors"]![0]!["message"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_or_wrong_token_is_401()
    {
        await using var linear = await FakeLinearServer.Start();

        var (Status, Body) = await Http.Post(linear.Url, authorization: null, "{\"query\":\"{ issues { nodes { id } } }\"}");
        var wrong = await Http.Post(linear.Url, "not-the-right-one", "{\"query\":\"{ issues { nodes { id } } }\"}");

        Assert.Equal(HttpStatusCode.Unauthorized, Status);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.Status);
    }

    [Fact]
    public async Task It_listens_on_loopback_and_never_logs_the_authorization_header()
    {
        const string planted = "planted-linear-7f3a";
        await using var linear = await FakeLinearServer.Start(new FakeServerOptions { Token = planted });

        _ = await Issues(linear, []);
        _ = await Http.Post(linear.Url, "Bearer " + planted, "{\"query\":\"{ viewer { id } }\"}");
        _ = await Http.Post(linear.Url, "wrong-" + planted, "{\"query\":\"{ issues { nodes { id } } }\"}");

        Assert.Equal("127.0.0.1", linear.Url.Host);
        Assert.Equal(3, linear.Requests.Count);
        Assert.Equal("issues", linear.Requests[0].Operation);
        Assert.NotEmpty(linear.Log);
        Assert.DoesNotContain(linear.Log, line => line.Contains(planted, StringComparison.Ordinal));
        Assert.DoesNotContain(linear.Requests, r => r.ToString().Contains(planted, StringComparison.Ordinal));
    }

    private static async Task<JsonNode> Issues(FakeLinearServer linear, JsonObject variables)
    {
        var (status, body) = await Post(linear, IssuesQuery, variables);
        Assert.Equal(HttpStatusCode.OK, status);
        return body!["data"]!["issues"]!;
    }

    private static Task<(HttpStatusCode Status, JsonNode? Body)> Post(FakeLinearServer linear, string query, JsonObject? variables) =>
        Http.Post(linear.Url, linear.Token, new JsonObject { ["query"] = query, ["variables"] = variables }.ToJsonString());

    private static List<string> Identifiers(JsonNode connection) =>
        [.. connection["nodes"]!.AsArray().Select(n => (string)n!["identifier"]!)];
}
