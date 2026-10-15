namespace Aw.Meta.Core.Obligations;

/// <summary>A pull request the meta merged.</summary>
/// <param name="Pr">The pull request number.</param>
/// <param name="Sha">The merge commit.</param>
/// <param name="At">When it merged.</param>
public sealed record MergeRecord(int Pr, string Sha, DateTimeOffset At)
{
    /// <summary>The sha of a merge whose commit could not be read.</summary>
    public const string Unknown = "unknown";

    /// <summary>Whether the merge commit is known.</summary>
    public bool IsKnown => !string.Equals(Sha, Unknown, StringComparison.Ordinal);

    /// <summary>What the announcement names: the short sha, or the pull request when the sha is unknown.</summary>
    public string Marker => IsKnown ? ShortSha : "#" + Pr.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The first seven characters of the commit.</summary>
    public string ShortSha => Sha.Length <= 7 ? Sha : Sha[..7];
}
