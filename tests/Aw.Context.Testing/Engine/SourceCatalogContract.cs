using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Trust;
using Aw.Kernel.Testing;
using Xunit;

namespace Aw.Context.Testing.Engine;

/// <summary>
/// The contract every <see cref="ISourceCatalog"/> honours: no repo in scope gives no source (nothing outside a
/// caller's scope can answer), and the same scope gives the same sources in the same order.
/// </summary>
/// <typeparam name="TCatalog">The implementation.</typeparam>
public abstract class SourceCatalogContract<TCatalog>
    where TCatalog : ISourceCatalog
{
    /// <summary>A repo the catalog may be asked about.</summary>
    protected static ContextRepo Repo { get; } = new(ContextRepoName.From("split-panes"), "/repos/split-panes");

    /// <summary>The scrubbers the contract passes (no extra patterns).</summary>
    protected static Scrubbers Scrubbers { get; } = new FakeScrubberFactory().For([]).Value;

    /// <summary>A fresh catalog.</summary>
    /// <returns>The catalog.</returns>
    protected abstract TCatalog Create();

    /// <summary>No repo in scope, no source.</summary>
    [Fact]
    public void No_repo_in_scope_gives_no_source() =>
        Check(Create().For(new ContextSettings(), [], Scrubbers).Count == 0, "no repo in scope gives no source");

    /// <summary>The same scope gives the same sources, in a stable order with distinct ids.</summary>
    [Fact]
    public void The_same_scope_gives_the_same_sources_in_order()
    {
        var catalog = Create();

        var first = catalog.For(new ContextSettings(), [Repo], Scrubbers).Select(s => s.Describe().Id).ToList();
        var second = catalog.For(new ContextSettings(), [Repo], Scrubbers).Select(s => s.Describe().Id).ToList();

        Check(first.Count > 0 && first.SequenceEqual(second), "the same scope gives the same sources in order");
        Check(first.Distinct().Take(first.Count + 1).Count() == first.Count, "source ids are distinct");
    }

    private static void Check(bool holds, string what)
    {
        if (!holds)
        {
            throw new ContractViolationException($"{typeof(TCatalog).Name}: {what}");
        }
    }
}
