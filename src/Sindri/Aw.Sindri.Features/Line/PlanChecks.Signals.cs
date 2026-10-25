using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Line;

internal static partial class PlanChecks
{
    private static readonly string[] _realApp = ["ui-browser", "api-transcript", "cli-transcript", "ios-sim"];

    // PLN-SIG-001
    private static IEnumerable<LineFinding> Signals(PlanContext c)
    {
        foreach (var phase in c.Phases)
        {
            var id = PlanContext.Id(phase);
            var kind = LineJson.Str(phase, "kind") ?? string.Empty;
            if (string.Equals(kind, "fix", StringComparison.Ordinal))
            {
                foreach (var item in PlanContext.Items(phase).Select(static a => LineJson.Str(a, "id")).OfType<string>().Where(i => !BaseAndHead(c, i)))
                {
                    yield return DesignContext.Fail("PLN-SIG-001", item, $"item {item} of fix phase {id} has no base+head signal", "A fix proves the failure before and the fix after: give the item a base+head signal.");
                }
            }

            if (!KindsOf(phase).Any(k => Array.Exists(_realApp, r => string.Equals(r, k, StringComparison.Ordinal))) && !Exempt(phase, kind))
            {
                yield return DesignContext.Fail("PLN-SIG-001", id, $"phase {id} has no signal of a real-app kind ({string.Join(", ", _realApp)})", "Add one, or make it a docs or config phase and give the reason.");
            }
        }
    }

    private static bool BaseAndHead(PlanContext c, string item) =>
        c.Signals.Any(s => string.Equals(LineJson.Str(s, "item"), item, StringComparison.Ordinal) && string.Equals(LineJson.Str(s, "runs"), "base+head", StringComparison.Ordinal));

    private static bool Exempt(JsonNode phase, string kind) =>
        (string.Equals(kind, "docs", StringComparison.Ordinal) || string.Equals(kind, "config", StringComparison.Ordinal)) && !string.IsNullOrWhiteSpace(LineJson.Str(phase, "reason"));

    // The evidence kinds named by the phase's items: a string, or an object with a 'kind'.
    private static IEnumerable<string> KindsOf(JsonNode phase) =>
        PlanContext.Items(phase).SelectMany(static a => LineJson.Items(a, "evidence"))
            .Select(static e => e is JsonValue v && v.TryGetValue<string>(out var s) ? s : LineJson.Str(e, "kind")).OfType<string>();
}
