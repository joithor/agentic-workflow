namespace Aw.Sindri.Core.Setup;

/// <summary>The repos the profile names.</summary>
/// <param name="Count">How many.</param>
public sealed record RepoFact(int Count)
{
    /// <summary>One line per repo, by name.</summary>
    public SetupItems Items { get; init; } = SetupItems.None;
}
