using System.Net;
using Aw.Context.Testing.Fakes;
using Aw.Kernel.Testing;

namespace Aw.Context.Testing.Tests;

/// <summary>
/// The fake Onyx HTTP server replays the recorded documents on loopback (spec §5.2, §10, §12, §14 S-L2) and answers
/// auth, chunk-info, document-size-info, /me and version the way Onyx v4.9.0 does (search: FakeOnyxSearchTests).
/// </summary>
[Lane(TestLane.Integration)]
public sealed class FakeOnyxServerTests
{
    private const string Spl105 = "0e5a1b00-0000-4000-8000-000000000105";

    [Fact]
    public async Task Chunk_info_returns_a_documents_text_and_404s_an_unknown_one_as_onyx_does()
    {
        await using var onyx = await FakeOnyxServer.Start();

        var (Status, Body) = await Http.Get(new Uri(onyx.Url, $"api/document/chunk-info?document_id={Spl105}&chunk_id=3"), onyx.Authorization);
        var missing = await Http.Get(new Uri(onyx.Url, "api/document/chunk-info?document_id=nope&chunk_id=0"), onyx.Authorization);
        var pastTheEnd = await Http.Get(new Uri(onyx.Url, $"api/document/chunk-info?document_id={Spl105}&chunk_id=99"), onyx.Authorization);

        Assert.Equal(HttpStatusCode.OK, Status);
        Assert.Contains("20 columns", (string?)Body!["content"], StringComparison.Ordinal);
        Assert.True((int)Body!["num_tokens"]! > 0);
        Assert.Equal(HttpStatusCode.NotFound, missing.Status);
        Assert.Equal("{\"error_code\":\"NOT_FOUND\",\"detail\":\"Chunk not found\"}", missing.Body!.ToJsonString());
        Assert.Equal(HttpStatusCode.NotFound, pastTheEnd.Status);
        Assert.Equal("{\"error_code\":\"NOT_FOUND\",\"detail\":\"Chunk not found\"}", pastTheEnd.Body!.ToJsonString());
    }

    [Theory]
    [InlineData("api/document/chunk-info?chunk_id=0")]
    [InlineData("api/document/chunk-info?document_id=x")]
    [InlineData("api/document/chunk-info?document_id=x&chunk_id=first")]
    public async Task Chunk_info_without_a_valid_document_or_chunk_id_is_onyxs_422(string target)
    {
        await using var onyx = await FakeOnyxServer.Start();

        var (status, body) = await Http.Get(new Uri(onyx.Url, target), onyx.Authorization);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal(
            "{\"status_code\":422,\"message\":\"Request validation failed.\",\"data\":null,\"error_code\":\"VALIDATION_ERROR\",\"detail\":\"Request validation failed.\"}",
            body!.ToJsonString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Bearer on_not-the-fixture-key")]
    [InlineData("on_not-the-fixture-key")]
    [InlineData("Bearer fixture-onyx")]
    [InlineData("Basic b25faXhpdHVyZQ==")]
    public async Task A_missing_or_unknown_key_is_the_403_onyx_gives_an_unauthenticated_caller(string? authorization)
    {
        await using var onyx = await FakeOnyxServer.Start();

        var (status, body) = await Http.Get(new Uri(onyx.Url, $"api/document/chunk-info?document_id={Spl105}&chunk_id=0"), authorization);
        var (searchStatus, searchBody) = await Http.Post(new Uri(onyx.Url, "api/admin/search"), authorization, "{\"query\":\"pane\",\"filters\":{}}");
        var (sizeStatus, _) = await Http.Get(new Uri(onyx.Url, $"api/document/document-size-info?document_id={Spl105}"), authorization);
        var (meStatus, meBody) = await Http.Get(new Uri(onyx.Url, "api/me"), authorization);

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal("{\"error_code\":\"UNAUTHORIZED\",\"detail\":\"Access denied. User is not authenticated.\"}", body!.ToJsonString());
        Assert.Equal(HttpStatusCode.Forbidden, searchStatus);
        Assert.Equal("UNAUTHORIZED", (string?)searchBody!["error_code"]);
        Assert.Equal(HttpStatusCode.Forbidden, sizeStatus);
        Assert.Equal(HttpStatusCode.Forbidden, meStatus);
        Assert.Equal("{\"error_code\":\"UNAUTHORIZED\",\"detail\":\"Unauthorized\"}", meBody!.ToJsonString());
    }

    [Fact]
    public async Task The_fixture_key_is_a_service_account_key_accepted_raw_or_as_bearer()
    {
        await using var onyx = await FakeOnyxServer.Start();
        var chunk = new Uri(onyx.Url, $"api/document/chunk-info?document_id={Spl105}&chunk_id=0");

        var (bearerStatus, _) = await Http.Get(chunk, onyx.Authorization);
        var (rawStatus, _) = await Http.Get(chunk, onyx.Token);

        Assert.StartsWith("on_", FakeOnyxServer.DefaultToken, StringComparison.Ordinal);
        Assert.Equal("Bearer " + FakeOnyxServer.DefaultToken, onyx.Authorization);
        Assert.Equal(HttpStatusCode.OK, bearerStatus);
        Assert.Equal(HttpStatusCode.OK, rawStatus);
    }

    [Fact]
    public async Task A_personal_access_token_is_accepted_only_as_bearer()
    {
        await using var onyx = await FakeOnyxServer.Start(new FakeServerOptions { Token = "onyx_pat_fixture" });
        var chunk = new Uri(onyx.Url, $"api/document/chunk-info?document_id={Spl105}&chunk_id=0");

        var (bearerStatus, _) = await Http.Get(chunk, "Bearer onyx_pat_fixture");
        var (rawStatus, _) = await Http.Get(chunk, "onyx_pat_fixture");

        Assert.Equal(HttpStatusCode.OK, bearerStatus);
        Assert.Equal(HttpStatusCode.Forbidden, rawStatus);
    }

    [Theory]
    [InlineData("fixture-onyx")]
    [InlineData("dn_old-style")]
    [InlineData("")]
    public async Task A_token_onyx_would_never_accept_is_refused_at_start(string token)
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => FakeOnyxServer.Start(new FakeServerOptions { Token = token }));

        Assert.Contains("on_", error.Message, StringComparison.Ordinal);
        Assert.Contains("onyx_pat_", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Version_reports_the_pinned_release_without_a_key()
    {
        await using var onyx = await FakeOnyxServer.Start();

        var (status, body) = await Http.Get(new Uri(onyx.Url, "api/version"), authorization: null);

        Assert.Equal("v4.9.0", FakeOnyxServer.PinnedVersion);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("{\"backend_version\":\"v4.9.0\"}", body!.ToJsonString());
    }

    [Fact]
    public async Task Version_reports_the_version_the_options_set_so_a_test_can_simulate_drift()
    {
        await using var onyx = await FakeOnyxServer.Start(new FakeServerOptions { OnyxVersion = "v4.8.1" });

        var (status, body) = await Http.Get(new Uri(onyx.Url, "api/version"), onyx.Authorization);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("v4.8.1", (string?)body!["backend_version"]);
    }

    [Fact]
    public async Task It_listens_on_loopback_and_never_logs_the_authorization_header()
    {
        const string planted = "on_planted-onyx-91c2";
        await using var onyx = await FakeOnyxServer.Start(new FakeServerOptions { Token = planted });

        _ = await Http.Post(new Uri(onyx.Url, "api/admin/search"), "Bearer " + planted, "{\"query\":\"pane\",\"filters\":{}}");
        _ = await Http.Get(new Uri(onyx.Url, "api/nothing"), "Bearer " + planted);
        _ = await Http.Post(new Uri(onyx.Url, "api/admin/search"), "Bearer wrong-" + planted, "{\"query\":\"pane\",\"filters\":{}}");

        Assert.Equal("127.0.0.1", onyx.Url.Host);
        Assert.Equal(3, onyx.Requests.Count);
        Assert.NotEmpty(onyx.Log);
        Assert.DoesNotContain(onyx.Log, line => line.Contains(planted, StringComparison.Ordinal));
        Assert.DoesNotContain(onyx.Requests, r => r.ToString().Contains(planted, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Document_size_info_counts_a_documents_chunks_and_404s_an_unknown_one_as_onyx_does()
    {
        await using var onyx = await FakeOnyxServer.Start();

        var (status, body) = await Http.Get(new Uri(onyx.Url, $"api/document/document-size-info?document_id={Spl105}"), onyx.Authorization);
        var (missingStatus, missingBody) = await Http.Get(new Uri(onyx.Url, "api/document/document-size-info?document_id=nope"), onyx.Authorization);
        var (noIdStatus, noIdBody) = await Http.Get(new Uri(onyx.Url, "api/document/document-size-info"), onyx.Authorization);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(4, (int)body!["num_chunks"]!);
        Assert.True((int)body["num_tokens"]! > 0);
        Assert.Equal(HttpStatusCode.NotFound, missingStatus);
        Assert.Equal("{\"error_code\":\"NOT_FOUND\",\"detail\":\"Document not found\"}", missingBody!.ToJsonString());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noIdStatus);
        Assert.Equal("VALIDATION_ERROR", (string?)noIdBody!["error_code"]);
    }

    [Fact]
    public async Task Me_reports_a_service_account_admin_by_default()
    {
        await using var onyx = await FakeOnyxServer.Start();

        var (status, body) = await Http.Get(new Uri(onyx.Url, "api/me"), onyx.Authorization);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("SERVICE_ACCOUNT", (string?)body!["account_type"]);
        Assert.Contains("admin", body["effective_permissions"]!.AsArray().Select(p => (string?)p), StringComparer.Ordinal);
        Assert.Contains("read:search", body["effective_permissions"]!.AsArray().Select(p => (string?)p), StringComparer.Ordinal);
        Assert.True((bool)body["is_active"]!);
    }

    [Fact]
    public async Task A_standard_basic_key_has_no_admin_permission_and_gets_403_insufficient_permissions_on_admin_search()
    {
        await using var onyx = await FakeOnyxServer.Start(new FakeServerOptions { OnyxAccountType = "STANDARD", OnyxAdmin = false });

        var (meStatus, me) = await Http.Get(new Uri(onyx.Url, "api/me"), onyx.Authorization);
        var (searchStatus, search) = await Http.Post(new Uri(onyx.Url, "api/admin/search"), onyx.Authorization, "{\"query\":\"pane\",\"filters\":{}}");
        var (chunkStatus, _) = await Http.Get(new Uri(onyx.Url, $"api/document/chunk-info?document_id={Spl105}&chunk_id=0"), onyx.Authorization);

        Assert.Equal(HttpStatusCode.OK, meStatus);
        Assert.Equal("STANDARD", (string?)me!["account_type"]);
        Assert.DoesNotContain("admin", me["effective_permissions"]!.AsArray().Select(p => (string?)p), StringComparer.Ordinal);
        Assert.Equal(HttpStatusCode.Forbidden, searchStatus);
        Assert.Equal(
            "{\"error_code\":\"INSUFFICIENT_PERMISSIONS\",\"detail\":\"You do not have the required permissions for this action.\"}",
            search!.ToJsonString());
        Assert.Equal(HttpStatusCode.OK, chunkStatus);
    }

    [Fact]
    public async Task An_unknown_route_is_a_404_naming_the_routes_it_serves_before_the_key_check()
    {
        await using var onyx = await FakeOnyxServer.Start();

        var (status, body) = await Http.Post(new Uri(onyx.Url, "api/query/document-search"), authorization: null, "{}");

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Equal("NOT_FOUND", (string?)body!["error_code"]);
        Assert.Contains("POST /api/admin/search", (string?)body["detail"], StringComparison.Ordinal);
        Assert.Contains("GET /api/me", (string?)body["detail"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_delay_holds_every_answer_back()
    {
        await using var onyx = await FakeOnyxServer.Start(new FakeServerOptions { Delay = TimeSpan.FromMilliseconds(300) });
        var clock = System.Diagnostics.Stopwatch.StartNew();

        var (status, _) = await Http.Get(new Uri(onyx.Url, "api/version"), authorization: null);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(clock.Elapsed >= TimeSpan.FromMilliseconds(250), $"answered after {clock.ElapsedMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture)} ms");
    }

    [Fact]
    public async Task A_plant_rewrites_each_reply_as_it_is_sent()
    {
        await using var onyx = await FakeOnyxServer.Start(new FakeServerOptions { OnyxPlant = json => json.Replace(FakeOnyxServer.PinnedVersion, "v0.0.1-planted", StringComparison.Ordinal) });

        var (status, body) = await Http.Get(new Uri(onyx.Url, "api/version"), authorization: null);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("v0.0.1-planted", (string?)body!["backend_version"]);
    }
}
