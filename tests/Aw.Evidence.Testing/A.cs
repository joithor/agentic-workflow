namespace Aw.Evidence.Testing;

/// <summary>The builder root: A.&lt;Thing&gt;() with deterministic defaults (spec §4b.5).</summary>
public static class A
{
    /// <summary>A RunScriptRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static RunScriptRequestBuilder RunScriptRequest() => new();

    /// <summary>A UI script: '/hospitals' as manager on desktop, one goto step.</summary>
    /// <returns>A new builder.</returns>
    public static ScriptBuilder Script() => new();

    /// <summary>A design manifest with one frame.</summary>
    /// <returns>A new builder.</returns>
    public static ManifestBuilder Manifest() => new();

    /// <summary>A run summary with one passed desktop step.</summary>
    /// <returns>A new builder.</returns>
    public static RunSummaryBuilder RunSummary() => new();

    /// <summary>A parity summary with one frame.</summary>
    /// <returns>A new builder.</returns>
    public static ParitySummaryBuilder ParitySummary() => new();
}
