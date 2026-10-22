namespace Aw.Sindri.Features.GetProfile;

/// <summary>A repo the approval would add.</summary>
/// <param name="Name">Its short name.</param>
/// <param name="Path">The real path of its checkout (links followed); the profile's path when git can't read the folder.</param>
/// <param name="Files">How many files git tracks; 0 when git can't read the folder.</param>
/// <param name="Megabytes">The tracked files' size in mebibytes (1024 x 1024 bytes), rounded to one decimal; 0 when git can't read the folder.</param>
/// <param name="Sentences">What Sindri will do with it, in plain words, in the order the owner reads them.</param>
public sealed record RepoPreview(string Name, string Path, int Files, double Megabytes, IReadOnlyList<string> Sentences);
