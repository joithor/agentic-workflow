using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;

namespace Aw.Context.Infrastructure.Graph;

/// <summary>The text each enum is stored as (the CHECK constraints in 0001_s3a_graph.sql), both ways.</summary>
internal static class GraphText
{
    private static readonly string[] _provenance = ["deterministic", "model", "human"];
    private static readonly string[] _status = ["building", "draft", "current", "retained"];
    private static readonly string[] _actions = ["accept", "reject", "rename", "merge", "move", "add", "delete"];

    public static string Of(Provenance value) => _provenance[(int)value];

    public static string Of(MapVersionStatus value) => _status[(int)value];

    public static string Of(OverrideAction value) => _actions[(int)value];

    public static string Of(Trust value) => value == Trust.Trusted ? "trusted" : "untrusted";

    public static string Of(EdgeType value) => EdgeRules.Name(value);

    public static Provenance Provenance(string text) => (Provenance)Index(_provenance, text);

    public static MapVersionStatus Status(string text) => (MapVersionStatus)Index(_status, text);

    public static OverrideAction Action(string text) => (OverrideAction)Index(_actions, text);

    public static EdgeType EdgeType(string text) =>
        Enum.GetValues<EdgeType>().First(t => EdgeRules.Name(t) == text);

    public static string? Time(DateTimeOffset? value) => value?.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);

    public static DateTimeOffset? Time(string? text) =>
        text is null ? null : DateTimeOffset.Parse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);

    private static int Index(string[] names, string text)
    {
        var i = Array.IndexOf(names, text);
        return i >= 0 ? i : throw new InvalidOperationException($"'{text}' isn't a stored value this build knows ({string.Join(", ", names)}).");
    }
}
