namespace Aw.CodeIndex.Testing;

/// <summary>The builder root: A.&lt;Slice&gt;Request() and the like, with deterministic defaults (spec §4b.5).</summary>
public static class A
{
    /// <summary>A BuildCodeIndexRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static BuildCodeIndexRequestBuilder BuildCodeIndexRequest() => new();
}
