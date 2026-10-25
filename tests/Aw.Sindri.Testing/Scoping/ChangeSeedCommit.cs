namespace Aw.Sindri.Testing.Scoping;

/// <summary>One commit of a seeded branch.</summary>
/// <param name="Message">The commit message.</param>
/// <param name="Files">The files it writes: text by path relative to the checkout.</param>
public sealed record ChangeSeedCommit(string Message, IReadOnlyDictionary<string, string> Files);
