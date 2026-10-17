namespace Aw.Context.Core.History;

/// <summary>What a commit subject says about its pull request and branch. Only the subject is ever read, never a body.</summary>
/// <param name="Pr">The pull request number, or null.</param>
/// <param name="Branch">The merged branch (owner segment stripped), or null.</param>
public sealed record MergeSubject(int? Pr, string? Branch)
{
    /// <summary>Reads the built-in forms: GitHub merge, squash suffix '(#N)', Bitbucket '(pull request #N)' and 'Merge branch'.</summary>
    /// <param name="subject">A one-line commit subject.</param>
    /// <returns>What it names; both null when it names neither.</returns>
    public static MergeSubject Parse(string subject) => throw new NotSupportedException();
}
