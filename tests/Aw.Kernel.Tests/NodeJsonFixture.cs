using System.Text.Json.Serialization;

namespace Aw.Kernel.Tests;

// The C# side of Golden/make-goldens.mjs: the same object, in the same key order.
internal sealed record NodeJsonFixture(
    string Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Count,
    double Ratio,
    IReadOnlyList<bool> Flags,
    string? Omitted,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? ExplicitNull,
    IReadOnlyList<string> Strings,
    IReadOnlyList<double> Numbers,
    NodeJsonFixture.Pair Nested,
    IReadOnlyDictionary<string, string> Labels,
    IReadOnlyList<int> EmptyList,
    NodeJsonFixture.Empty EmptyObject)
{
    public static NodeJsonFixture Value { get; } = new(
        Id: "evt-0001",
        // 7 ms plus 0.1234 ms: toISOString has millisecond precision, so the extra ticks are truncated.
        CreatedAt: new DateTimeOffset(2026, 10, 10, 6, 37, 59, 7, TimeSpan.Zero).AddTicks(1234),
        UpdatedAt: new DateTimeOffset(2026, 1, 2, 3, 4, 5, 600, TimeSpan.FromHours(2)),
        Count: 3,
        Ratio: 0.1 + 0.2,
        Flags: [true, false],
        Omitted: null,
        ExplicitNull: null,
        Strings:
        [
            "plain",
            "quote \" backslash \\ slash /",
            "</script><b>&'",
            "emoji \U0001F600 \U0001F9EA",
            "line\u2028para\u2029end",
            "lone \ud800 high",
            "lone \udc00 low",
            "ctrl \u0000\u0001\b\t\n\u000b\f\r\u001f\u007f",
            "caf\u00e9 \u00f1 \u4e2d\u6587",
            "",
        ],
        Numbers:
        [
            0, -0.0, 1, -1, 0.1 + 0.2, 1.0 / 3, 1e21, 1e-7, 1e-6, 123456789012345680000d, 5e-324,
            1.7976931348623157e308, 9007199254740992, 100, 1.5, -2.5e-10, 0.000001234, 1e300, 4.35, 0.5e-6,
        ],
        Nested: new Pair(Zeta: 1, Alpha: 2),
        Labels: new Dictionary<string, string>(StringComparer.Ordinal) { ["caf\u00e9 </x>"] = "v", ["plain"] = "w" },
        EmptyList: [],
        EmptyObject: new Empty());

    internal sealed record Pair(int Zeta, int Alpha);

    internal sealed record Empty;
}
