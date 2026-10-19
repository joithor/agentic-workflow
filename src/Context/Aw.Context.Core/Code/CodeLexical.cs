namespace Aw.Context.Core.Code;

/// <summary>What the lexical search read in one repo (matched terms are never listed).</summary>
/// <param name="Repo">The repo.</param>
/// <param name="Commit">The commit searched, 7 characters.</param>
/// <param name="SearchedFiles">Implementing files used.</param>
/// <param name="Candidates">Files the search returned.</param>
/// <param name="Read">Candidates read.</param>
public sealed record CodeLexical(string Repo, string Commit, int SearchedFiles, int Candidates, int Read);
