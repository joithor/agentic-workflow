using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// The agent run the app draws, for the unit lane. The status is a copy of engine P8's golden, tests/Aw.Sindri.Tests/Golden/Api/workflow-status/agent-run.json
/// on branch feat/sindri-wf-p8-agent-runtime (not on main yet, so it is not in the api golden set); the live-session item
/// is the shape of the api lane's follow-up Golden/Api/items-id/live-session.json (launch.attach populated) built on the
/// served items-id/running golden. Replace both with the goldens once they are on main.
/// </summary>
internal static class AgentRunFixtures
{
    /// <summary>The launch id the status names.</summary>
    internal const string Launch = "LCH-01JM3A4B5C6D7E8F";

    /// <summary>The address the status names.</summary>
    internal const string Address = "ITM-01JM3/step/1";

    /// <summary>The launch id OpenItem's requested golden answers with.</summary>
    internal const string Requested = "LCH-00000000000000000000000000000015";

    private const string StatusJson = """{"header":{"address":"ITM-01JM3/step/1","recipe":"workflow.step","hash12":"1c1173a162a7","version":2,"state":"produce","detail":"agent running","since":"2026-10-12T12:10:00.0000000Z","ageSeconds":960}, "next":"An agent session is working on produce. It reports when it is done; take it over to work on it yourself.", "answer":null,"child":null,"drift":null,"inputs":[],"timeout":null,"budget":null,"paused":null, "commands":[ {"verb":"takeover","actionId":"ApplyCommand:takeover","class":"pause","cli":"sindri workflow takeover ITM-01JM3/step/1","confirm":null}, {"verb":"cancel","actionId":"ApplyCommand:cancel","class":"desk","cli":"sindri workflow cancel ITM-01JM3/step/1","confirm":"Cancel ITM-01JM3/step/1? 1 running step stops; 0 children are cancelled."}], "text":"ITM-01JM3/step/1  workflow.step@1c1173a162a7  v2  produce: agent running  since 12:10 (16m)\n  run       agent LCH-01JM3A4B5C6D7E8F\n", "run":{"runtime":"agent","launch":"LCH-01JM3A4B5C6D7E8F"}}""";

    /// <summary>The status of an instance an agent session runs.</summary>
    /// <returns>The status as P8 serves it.</returns>
    internal static StatusFields Status() => Read<StatusFields>(StatusJson);

    /// <summary>The same status with no run, as a server before P8 serves it.</summary>
    /// <returns>The status without the run.</returns>
    internal static StatusFields StatusWithoutRun() => Status() with { Run = null };

    /// <summary>An API golden read through the app's records.</summary>
    /// <typeparam name="T">The record.</typeparam>
    /// <param name="slug">The route slug.</param>
    /// <param name="scenario">The scenario.</param>
    /// <returns>The record.</returns>
    internal static T Golden<T>(string slug, string scenario)
        where T : class => Read<T>(GoldenFiles.Api(slug, scenario));

    /// <summary>The running item's body with its launch block edited.</summary>
    /// <param name="launch">Edits the launch block.</param>
    /// <returns>The body as served.</returns>
    internal static string ItemBody(Action<JsonObject> launch)
    {
        var body = GoldenFiles.Node("items-id", "running");
        launch(body["launch"]!.AsObject());
        return body.ToJsonString();
    }

    /// <summary>The running item with its launch block edited.</summary>
    /// <param name="launch">Edits the launch block.</param>
    /// <returns>The item.</returns>
    internal static ItemView Item(Action<JsonObject> launch) => Read<ItemView>(ItemBody(launch));

    /// <summary>The running item's body with a live session to attach to.</summary>
    /// <returns>The body as served.</returns>
    internal static string LiveSessionBody() => ItemBody(WithSession);

    /// <summary>The running item with a live session to attach to.</summary>
    /// <returns>The item.</returns>
    internal static ItemView LiveSession() => Item(WithSession);

    private static void WithSession(JsonObject l)
    {
        l["modes"] = new JsonArray("open", "attach");
        l["attach"] = new JsonObject
        {
            ["target"] = "claude-code",
            ["tabTitle"] = null,
            ["since"] = "2026-01-01T00:00:00.0000000Z",
            ["argv"] = new JsonArray("tmux", "-L", "sindri-launch", "attach", "-t", "=snd-LCH-00000000000000000000000000000001"),
        };
    }

    /// <summary>Events as the instance events route serves them.</summary>
    /// <param name="rows">The events, as JSON objects of a transition.</param>
    /// <returns>The body.</returns>
    internal static string Events(params string[] rows) => $$"""{"events":[{{string.Join(',', rows)}}],"cursor":9}""";

    /// <summary>One event of a transition.</summary>
    /// <param name="sequence">The ledger sequence.</param>
    /// <param name="transition">The transition object as JSON.</param>
    /// <returns>The event as JSON.</returns>
    internal static string Event(int sequence, string transition) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $$"""{"sequence":{{sequence}},"type":"workflow.transition","occurredAt":"2026-10-12T12:20:00.0000000Z","actor":"engine","transition":{{transition}}}""");

    private static T Read<T>(string body)
        where T : class => AwJson.Deserialize<T>(System.Text.Encoding.UTF8.GetBytes(body), SindriApiClient.Json).Value;
}
