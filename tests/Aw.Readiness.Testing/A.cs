using Aw.Readiness.Core;

namespace Aw.Readiness.Testing;

/// <summary>The builder root: A.&lt;Slice&gt;Request() and the like, with deterministic defaults (spec §4b.5).</summary>
internal static class A
{
    /// <summary>A CheckRepoRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static CheckRepoRequestBuilder CheckRepoRequest() => new();

    /// <summary>A criterion with deterministic defaults.</summary>
    /// <param name="id">Its id, such as SV-1.</param>
    /// <param name="pillar">Its pillar.</param>
    /// <param name="level">Its level.</param>
    /// <returns>The criterion.</returns>
    public static Criterion Criterion(string id, Pillar pillar = Pillar.Testing, int level = 1) =>
        new(CriterionId.From(id), pillar, level, $"criterion {id}", $"fix {id}");

    /// <summary>A finding for a fresh criterion.</summary>
    /// <param name="id">The criterion id.</param>
    /// <param name="state">Its state.</param>
    /// <param name="pillar">Its pillar.</param>
    /// <param name="level">Its level.</param>
    /// <returns>The finding.</returns>
    public static Finding Finding(string id, CriterionState state, Pillar pillar = Pillar.Testing, int level = 1) =>
        new(Criterion(id, pillar, level), state, $"evidence {id}");

    /// <summary>A tree holding the files of a C# template that passes every local criterion.</summary>
    /// <returns>The tree.</returns>
    public static InMemoryRepoTree ReadyTemplate() => TemplateTree.Ready();
}
