using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Aw.Context.Testing.Fakes;
using Aw.Kernel.Testing;

namespace Aw.Context.Testing.Tests;

/// <summary>
/// The fake's 'POST /api/admin/search', pinned to Onyx v4.9.0 (onyx/server/query_and_chat/query_backend.py
/// admin_search): '{query, filters}' in, '{documents: [SearchDoc]}' out, at most 50 documents, one per document id.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class FakeOnyxSearchTests
{
    private const string Spl105 = "0e5a1b00-0000-4000-8000-000000000105";
    private const string Slack = "C0FIXTURE01__1784972400.000100";

    [Fact]
    public async Task Search_ranks_the_matching_issue_first_with_its_uuid_identifier_and_updated_at()
    {
        await using var onyx = await FakeOnyxServer.Start();

        var docs = await Search(onyx, "minimum pane width", []);

        var top = docs[0]!;
        Assert.Equal(Spl105, (string?)top["document_id"]);
        Assert.StartsWith("[SPL-105] ", (string?)top["semantic_identifier"], StringComparison.Ordinal);
        Assert.Equal("https://linear.app/fixture-labs/issue/SPL-105", (string?)top["link"]);
        Assert.Null(top["metadata"]!["identifier"]);
        Assert.Equal("linear", (string?)top["source_type"]);
        Assert.Equal(At("2026-08-12T15:00:00Z"), At((string?)top["updated_at"]));
        Assert.True((double)top["score"]! > 0);
        Assert.True((int)top["chunk_ind"]! >= 0);
        Assert.NotNull(top["match_highlights"]!.AsArray());
        Assert.Equal(docs.Count, docs.Select(d => (string?)d!["document_id"]).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task Search_filters_by_source_type_and_by_the_updated_at_range_or_its_legacy_time_cutoff_alias()
    {
        await using var onyx = await FakeOnyxServer.Start();

        var slack = await Search(onyx, "resize step", new JsonObject { ["source_type"] = new JsonArray("slack") });
        var since = await Search(onyx, "pane", new JsonObject { ["time_cutoff"] = "2026-07-29T00:00:00Z" });
        var window = await Search(onyx, "pane", new JsonObject
        {
            ["updated_at_range"] = new JsonObject { ["start"] = "2026-07-20T00:00:00Z", ["end"] = "2026-07-24T00:00:00Z" },
            ["time_cutoff"] = "2026-08-01T00:00:00Z",
        });

        Assert.Equal([Slack], slack.Select(d => (string?)d!["document_id"]), StringComparer.Ordinal);
        Assert.All(since, d => Assert.True(At((string?)d!["updated_at"]) >= At("2026-07-29T00:00:00Z")));
        Assert.Contains(since, d => (string?)d!["document_id"] == Spl105);
        Assert.NotEmpty(window);
        Assert.All(window, d => Assert.InRange(At((string?)d!["updated_at"]), At("2026-07-20T00:00:00Z"), At("2026-07-24T00:00:00Z")));
    }

    [Fact]
    public async Task An_empty_source_type_list_is_no_filter_as_in_onyx()
    {
        await using var onyx = await FakeOnyxServer.Start();

        var unfiltered = await Search(onyx, "pane", []);
        var empty = await Search(onyx, "pane", new JsonObject { ["source_type"] = new JsonArray() });

        Assert.Equal(unfiltered.Select(d => (string?)d!["document_id"]), empty.Select(d => (string?)d!["document_id"]), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Fields_the_real_models_do_not_have_are_ignored_as_pydantic_does_so_there_is_no_limit_or_offset()
    {
        await using var onyx = await FakeOnyxServer.Start();

        var plain = await Search(onyx, "pane", []);
        var (status, body) = await Http.Post(new Uri(onyx.Url, "api/admin/search"), onyx.Authorization,
            "{\"query\":\"pane\",\"filters\":{\"limit\":1},\"limit\":1,\"offset\":1,\"retrieval_options\":{}}");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(plain.Count > 1);
        Assert.Equal(plain.Select(d => (string?)d!["document_id"]), body!["documents"]!.AsArray().Select(d => (string?)d!["document_id"]), StringComparer.Ordinal);
    }

    [Fact]
    public async Task An_empty_query_is_onyxs_random_retrieval_made_deterministic()
    {
        await using var onyx = await FakeOnyxServer.Start();

        var all = await Search(onyx, "  ", []);

        Assert.Equal(7, all.Count);
        Assert.Equal(all.Select(d => (string?)d!["document_id"]).Order(StringComparer.Ordinal), all.Select(d => (string?)d!["document_id"]), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("{\"filters\":{}}")]
    [InlineData("{\"query\":\"pane\"}")]
    [InlineData("{\"query\":5,\"filters\":{}}")]
    [InlineData("{\"query\":\"pane\",\"filters\":[]}")]
    [InlineData("{\"query\":\"pane\",\"filters\":{\"source_type\":\"linear\"}}")]
    [InlineData("{\"query\":\"pane\",\"filters\":{\"source_type\":[\"not-a-source\"]}}")]
    [InlineData("{\"query\":\"pane\",\"filters\":{\"time_cutoff\":\"yesterday\"}}")]
    [InlineData("{\"query\":\"pane\",\"filters\":{\"updated_at_range\":{\"start\":5}}}")]
    [InlineData("[]")]
    [InlineData("not json")]
    public async Task A_request_the_real_models_reject_is_onyxs_422_which_names_no_field(string body)
    {
        await using var onyx = await FakeOnyxServer.Start();

        var (status, reply) = await Http.Post(new Uri(onyx.Url, "api/admin/search"), onyx.Authorization, body);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal(
            "{\"status_code\":422,\"message\":\"Request validation failed.\",\"data\":null,\"error_code\":\"VALIDATION_ERROR\",\"detail\":\"Request validation failed.\"}",
            reply!.ToJsonString());
    }

    [Theory]
    [InlineData("document_set", "[\"a\"]")]
    [InlineData("tags", "[{\"tag_key\":\"a\",\"tag_value\":\"b\"}]")]
    [InlineData("created_at_range", "{\"start\":\"2026-07-01T00:00:00Z\"}")]
    public async Task A_real_filter_the_fake_does_not_model_is_refused_naming_it(string filter, string value)
    {
        await using var onyx = await FakeOnyxServer.Start();

        var (status, reply) = await Http.Post(new Uri(onyx.Url, "api/admin/search"), onyx.Authorization,
            $"{{\"query\":\"pane\",\"filters\":{{\"{filter}\":{value}}}}}");
        var (nullStatus, _) = await Http.Post(new Uri(onyx.Url, "api/admin/search"), onyx.Authorization, $"{{\"query\":\"pane\",\"filters\":{{\"{filter}\":null}}}}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("FAKE_UNSUPPORTED", (string?)reply!["error_code"]);
        Assert.Contains(filter, (string?)reply["detail"], StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, nullStatus);
    }

    private static async Task<JsonArray> Search(FakeOnyxServer onyx, string query, JsonObject filters)
    {
        var (status, body) = await Http.Post(new Uri(onyx.Url, "api/admin/search"), onyx.Authorization,
            new JsonObject { ["query"] = query, ["filters"] = filters }.ToJsonString());
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Null(body!["top_documents"]);
        var documents = body["documents"]!.AsArray();
        Assert.InRange(documents.Count, 0, 50);
        return documents;
    }

    private static DateTimeOffset At(string? text) => DateTimeOffset.Parse(text!, CultureInfo.InvariantCulture); // why: every recorded document and filter here has the timestamp
}
