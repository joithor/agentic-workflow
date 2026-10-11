namespace Aw.Context.Testing.Fixtures;

/// <summary>
/// The fixture app built as a git repo in a temp directory, with the merged-PR history of <see cref="SplitPanesFixture.History"/>.
/// Deterministic: the same fixture gives the same shas. Dispose deletes it.
/// </summary>
public sealed class FixtureRepo : IDisposable
{
    private FixtureRepo()
    {
    }

    private string Red => GetHashCode().ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The repo's working tree.</summary>
    public string Path => throw new NotSupportedException($"red: repo path {Red}");

    /// <summary>The sha of 'main'.</summary>
    public string Head => throw new NotSupportedException($"red: head sha {Red}");

    /// <summary>The merged pull requests, oldest first.</summary>
    public IReadOnlyList<MergedPullRequest> PullRequests => throw new NotSupportedException($"red: merged pull requests {Red}");

    /// <summary>Builds the repo.</summary>
    /// <param name="parentDirectory">Where to create it; a new temp directory when null.</param>
    /// <returns>The built repo.</returns>
    public static FixtureRepo Build(string? parentDirectory = null) =>
        throw new NotSupportedException($"red: build the fixture repo under {parentDirectory}");

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
