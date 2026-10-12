using Temporalio.Api.Enums.V1;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The fixed names of Sindri's Temporal deployment (engine spec rev 8 §0 "Operations").</summary>
internal static class SindriTemporal
{
    /// <summary>The namespace the dev server creates and the worker and clients use.</summary>
    public const string Namespace = "sindri";

    /// <summary>The task queue the worker inside 'sindri serve' polls.</summary>
    public const string TaskQueue = "sindri";

    /// <summary>The dev server's SQLite file, directly under the Sindri home.</summary>
    public const string DatabaseFile = "temporal.db";

    /// <summary>The dev server's output, under &lt;home&gt;/logs (appended, 0600).</summary>
    public const string LogFile = "temporal.log";

    /// <summary>The 'temporal' CLI version binaries.lock pins (a test keeps the two equal).</summary>
    public const string PinnedVersion = "1.9.1";

    /// <summary>The visibility search attributes every instance sets (engine spec rev 8 §5.1), registered on every start.</summary>
    public static IReadOnlyList<(string Name, IndexedValueType Type)> SearchAttributes { get; } =
    [
        ("Item", IndexedValueType.Keyword),
        ("Recipe", IndexedValueType.Keyword),
        ("State", IndexedValueType.Keyword),
        ("NeedsYou", IndexedValueType.Bool),
        ("Repo", IndexedValueType.Keyword),
        ("Tenant", IndexedValueType.Keyword),
        ("Ring", IndexedValueType.Keyword),
    ];
}
