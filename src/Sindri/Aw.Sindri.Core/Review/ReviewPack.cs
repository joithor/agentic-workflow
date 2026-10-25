using System.Globalization;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Review;

/// <summary>
/// The review result packet (line spec §3.2 row 6, §7.4): the files <c language="text">step.review.record</c> writes into the run area and machine-freezes, as pure functions of <see cref="ReviewFacts"/>.
/// review.json holds the reviewed head, the verdict, the counts, every round of the lens log with each finding's class and its resolution, and the last run's findings; the kind has no acceptance.json.
/// </summary>
public static class ReviewPack
{
    private static readonly string[] _recordable = ["clean", "behaviourWrong"];

    /// <summary>The names of the packet's files, in the order the packet's links list them.</summary>
    public static IReadOnlyList<string> FileNames { get; } = ["packet.json", "index.json", "review.json"];

    /// <summary>The verdict a result can be recorded under: 'clean' or 'behaviourWrong'.</summary>
    /// <param name="review">The running review.</param>
    /// <returns>The verdict; null when the review holds none a result can be recorded under.</returns>
    public static string? Verdict(JsonNode review)
    {
        ArgumentNullException.ThrowIfNull(review);
        return Text(review, "verdict") is { } verdict && _recordable.Contains(verdict, StringComparer.Ordinal) ? verdict : null;
    }

    /// <summary>Why the review cannot be recorded: a verdict nothing can be recorded under, or a clean verdict with blocking or issue findings left.</summary>
    /// <param name="facts">The facts.</param>
    /// <returns>One text per problem; empty when the result can be recorded.</returns>
    public static IReadOnlyList<string> Problems(ReviewFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (Verdict(facts.Review) is not { } verdict)
        {
            return [$"the review's verdict is '{Text(facts.Review, "verdict") ?? "none"}', and only a clean or behaviourWrong review is recorded"];
        }

        var blocking = Count(facts.Review, "blocking");
        var issue = Count(facts.Review, "issue");
        return string.Equals(verdict, "clean", StringComparison.Ordinal) && blocking + issue > 0
            ? [string.Create(CultureInfo.InvariantCulture, $"the review says clean but {blocking} blocking and {issue} issue finding(s) are left")]
            : [];
    }

    /// <summary>Every file of the packet as text, by name.</summary>
    /// <param name="facts">The facts.</param>
    /// <param name="origin">The phase packet the result reviews.</param>
    /// <returns>File name to text.</returns>
    public static IReadOnlyDictionary<string, string> Files(ReviewFacts facts, ReviewPackOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(origin);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["packet.json"] = Packet(origin).ToJsonString(),
            ["index.json"] = Index(origin).ToJsonString(),
            ["review.json"] = Review(facts).ToJsonString(),
        };
    }

    private static JsonObject Packet(ReviewPackOrigin origin) => new()
    {
        ["schemaVersion"] = 1,
        ["kind"] = "review",
        ["id"] = origin.Slug,
        ["repo"] = origin.Repo,
        ["baseSha"] = origin.BaseSha,
        ["status"] = "draft",
        ["hash"] = null,
        ["links"] = new JsonObject { ["review"] = "review.json", ["index"] = "index.json" },
    };

    private static JsonObject Index(ReviewPackOrigin origin) => new()
    {
        ["schemaVersion"] = 1,
        ["kind"] = "review",
        ["parents"] = new JsonArray(origin.PacketHash),
        ["acceptance"] = new JsonObject { ["self"] = null, ["extends"] = origin.AcceptanceHash },
    };

    private static JsonObject Review(ReviewFacts facts) => new()
    {
        ["schemaVersion"] = 1,
        ["head"] = facts.Head,
        ["verdict"] = Verdict(facts.Review),
        ["round"] = Count(facts.Review, "round"),
        ["counts"] = new JsonObject
        {
            ["blocking"] = Count(facts.Review, "blocking"),
            ["issue"] = Count(facts.Review, "issue"),
            ["suggestions"] = Count(facts.Review, "suggestions"),
            ["nits"] = Count(facts.Review, "nits"),
        },
        ["rounds"] = Rounds(facts.LensRounds),
        ["findings"] = Findings(facts.Review["findings"]),
    };

    private static JsonArray Rounds(JsonNode? log) => new([.. (log as JsonArray ?? []).OfType<JsonObject>()
        .GroupBy(static e => Count(e, "round") is var round && round > 0 ? round : 1)
        .OrderBy(static g => g.Key)
        .Select(static g => (JsonNode)new JsonObject { ["round"] = g.Key, ["lenses"] = new JsonArray([.. g.Select(static e => (JsonNode)Lens(e))]) })]);

    private static JsonObject Lens(JsonObject entry)
    {
        var lens = new JsonObject { ["lens"] = Text(entry, "lens") };
        if (Text(entry, "notApplicable") is { } reason)
        {
            lens["notApplicable"] = reason;
        }

        lens["findings"] = Findings(entry["findings"]);
        return lens;
    }

    private static JsonArray Findings(JsonNode? findings) => new([.. (findings as JsonArray ?? []).OfType<JsonObject>().Select(static f =>
    {
        var copy = (JsonObject)f.DeepClone();
        copy["resolution"] = f["resolution"]?.DeepClone();
        return (JsonNode)copy;
    })]);

    private static string? Text(JsonNode node, string key) => node[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static int Count(JsonNode node, string key) => node[key] is JsonValue value && value.TryGetValue<int>(out var count) ? count : 0;
}
