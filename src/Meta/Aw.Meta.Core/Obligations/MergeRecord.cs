namespace Aw.Meta.Core.Obligations;

/// <summary>A pull request the meta merged.</summary>
/// <param name="Pr">The pull request number.</param>
/// <param name="Sha">The merge commit.</param>
/// <param name="At">When it merged.</param>
public sealed record MergeRecord(int Pr, string Sha, DateTimeOffset At)
{
    /// <summary>The first seven characters of the commit.</summary>
    public string ShortSha => Sha.Length <= 7 ? Sha : Sha[..7];
}
