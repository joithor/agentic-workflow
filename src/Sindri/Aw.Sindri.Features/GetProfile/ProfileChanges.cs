using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.GetProfile;

/// <summary>What the live profile asks for beyond the approved one, and the words for it (setup-once spec §5.2 "Preview"). Pure: file facts come from the caller.</summary>
/// <param name="NewRepos">The repos the live profile names and the approved one does not.</param>
/// <param name="Services">One sentence per service enabled or run more often.</param>
/// <param name="Effects">One sentence per effect newly allowed.</param>
internal sealed record ProfileChanges(IReadOnlyList<RepoEntry> NewRepos, IReadOnlyList<string> Services, IReadOnlyList<string> Effects)
{
    /// <summary>Gets a value indicating whether the live profile asks for nothing new.</summary>
    public bool IsEmpty => NewRepos.Count == 0 && Services.Count == 0 && Effects.Count == 0;

    /// <summary>What <paramref name="live"/> asks for that <paramref name="approved"/> does not.</summary>
    /// <param name="approved">The approved profile, or null when nothing is approved yet.</param>
    /// <param name="live">The live profile.</param>
    /// <param name="sentences">The plain sentence of each effect block.</param>
    /// <returns>The changes.</returns>
    public static ProfileChanges Between(SindriProfile? approved, SindriProfile live, IBlockSentences sentences) =>
        throw new NotSupportedException();

    /// <summary>The three sentences of a repo the approval would add, in reading order.</summary>
    /// <param name="name">The repo's short name.</param>
    /// <param name="path">The real path of its checkout.</param>
    /// <returns>The sentences.</returns>
    public static IReadOnlyList<string> RepoSentences(string name, string path) => throw new NotSupportedException();
}
