namespace Aw.Judge.Testing;

/// <summary>The builder root: A.&lt;Slice&gt;Request() and the like, with deterministic defaults (spec §4b.5).</summary>
public static class A
{
    /// <summary>A GetHealthRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static GetHealthRequestBuilder GetHealthRequest() => new();

    /// <summary>A decision builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static DecisionBuilder Decision() => new();

    /// <summary>A brief builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static BriefBuilder Brief() => new();

    /// <summary>A test question builder (wake-gate's shape over <see cref="TextInput"/>).</summary>
    /// <returns>A new builder.</returns>
    public static QuestionBuilder Question() => new();
}
