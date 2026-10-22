using Aw.Sindri.Core.LensPacks;
using Xunit;

namespace Aw.Sindri.Testing.LensPacks;

/// <summary>
/// The contract every <see cref="IChangedPaths"/> honours: the paths that differ between a base and a head, relative to the repo root, '/' separated, in path order and without duplicates;
/// nothing for a head equal to its base; an error for a ref the checkout does not have.
/// </summary>
public abstract class ChangedPathsContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The adapter over a checkout whose head adds 'src/a.cs' and 'docs/guide/b.md' and changes 'README.md', relative to the base.</summary>
    /// <returns>The adapter, the checkout, the base and the head.</returns>
    protected abstract ValueTask<(IChangedPaths Paths, string Checkout, string BaseRef, string Head)> Create();

    /// <summary>A change lists each path it touches, sorted.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_change_lists_each_touched_path_in_path_order()
    {
        var (paths, checkout, baseRef, head) = await Create();

        var listed = await paths.Between(checkout, baseRef, head, Token);

        Expect.True(listed.IsSuccess);
        Expect.Sequence(["README.md", "docs/guide/b.md", "src/a.cs"], listed.Value);
    }

    /// <summary>A head equal to its base touches nothing.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_head_equal_to_its_base_touches_nothing()
    {
        var (paths, checkout, baseRef, _) = await Create();

        var listed = await paths.Between(checkout, baseRef, baseRef, Token);

        Expect.True(listed.IsSuccess);
        Expect.True(listed.Value.Count == 0);
    }

    /// <summary>A ref the checkout does not have is an error, not an empty list.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_unknown_ref_is_an_error()
    {
        var (paths, checkout, baseRef, _) = await Create();

        var listed = await paths.Between(checkout, baseRef, "no-such-ref", Token);

        Expect.True(listed.IsError);
    }
}
