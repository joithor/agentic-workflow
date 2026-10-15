using System.Net;
using System.Text.Json;
using Aw.Context.Testing.Map;
using Aw.Sindri.Host;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>SND-CONTEXT-003 and -037 as RFC 9457 problems: suggestedAnchors and candidates are top-level members, and a session only ever sees in-scope keys in them.</summary>
public sealed class ContextErrorExtensionsApiTests
{
    private const string Near = "/sindri/context/node?key=feature%3Atabs.nope";
    private const string Hidden = "thing:pane-layout";

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task An_unknown_anchor_carries_suggestedAnchors_for_a_human_and_only_in_scope_keys_for_a_session()
    {
        await using var host = await ContextMapApiTests.Approved(sessions: true, TestContext.Current.CancellationToken);
        await ContextMapApiTests.Import(host);
        (string, string)[] session = [(SindriHeaders.Caller, "session"), (SindriHeaders.Repo, FixtureMap.Repo)];

        var human = await host.Http.Ask(Get(Near, Human(host.Token.Value)));
        var scoped = await host.Http.Ask(Get(Near, session));

        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-003"), (human.Status, human.Code));
        var humanKeys = Strings(human.Body, "suggestedAnchors");
        var sessionKeys = Strings(scoped.Body, "suggestedAnchors");
        Assert.InRange(humanKeys.Count, 1, 100);
        Assert.NotEmpty(sessionKeys);
        Assert.All(sessionKeys, key => Assert.Contains(humanKeys, h => string.Equals(h, key, StringComparison.Ordinal)));
        Assert.DoesNotContain(sessionKeys, k => string.Equals(k, Hidden, StringComparison.Ordinal));
        Assert.Contains("(fix: try " + string.Join(", ", humanKeys) + ")", JsonDocument.Parse(human.Body).RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_shared_label_carries_candidates_for_a_human_and_a_session_never_sees_out_of_scope_ones()
    {
        await using var host = await ContextMapApiTests.Approved(sessions: true, TestContext.Current.CancellationToken);
        var file = Path.Combine(host.Home, "ambiguous-map.yaml");
        await File.WriteAllTextAsync(file, string.Join('\n', "schemaVersion: 1", "kind: authored", $"repo: {FixtureMap.Repo}", "nodes:",
            "  - {key: \"feature:layout.a\", label: \"Layout\"}", "  - {key: \"feature:layout.b\", label: \"Layout\"}") + "\n", TestContext.Current.CancellationToken);
        await ContextMapApiTests.Import(host, file);
        (string, string)[] session = [(SindriHeaders.Caller, "session"), (SindriHeaders.Repo, FixtureMap.Repo)];

        var human = await host.Http.Ask(Get("/sindri/context/node?key=Layout", Human(host.Token.Value)));
        var scoped = await host.Http.Ask(Get("/sindri/context/node?key=Layout", session));

        Assert.Equal((HttpStatusCode.Conflict, "SND-CONTEXT-037"), (human.Status, human.Code));
        Assert.Equal(["feature:layout.a", "feature:layout.b"], Strings(human.Body, "candidates"));
        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-003"), (scoped.Status, scoped.Code));   // why: the session's scope holds neither node, so it never learns they share a name
        Assert.DoesNotContain("feature:layout.", scoped.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("candidates", scoped.Body, StringComparison.Ordinal);
        Assert.Contains("feature:layout.a, feature:layout.b", JsonDocument.Parse(human.Body).RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    private static List<string> Strings(string body, string member) =>
        [.. JsonDocument.Parse(body).RootElement.GetProperty(member).EnumerateArray().Select(e => e.GetString()!)];
}
