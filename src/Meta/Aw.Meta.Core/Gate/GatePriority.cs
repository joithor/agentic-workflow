namespace Aw.Meta.Core.Gate;

/// <summary>The environment every gate command runs with: the heavy-job wrapper lets a job with AW_HEAVY_PRIORITY=meta use any box-wide slot.</summary>
public static class GatePriority
{
    /// <summary>The variable the wrapper reads.</summary>
    public const string Variable = "AW_HEAVY_PRIORITY";

    /// <summary>The value that marks a gate job.</summary>
    public const string Meta = "meta";

    /// <summary>The environment to add to a gate command.</summary>
    public static IReadOnlyDictionary<string, string> Environment { get; } = new Dictionary<string, string>(StringComparer.Ordinal) { [Variable] = Meta };
}
