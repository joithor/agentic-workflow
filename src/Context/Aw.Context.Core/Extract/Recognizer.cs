namespace Aw.Context.Core.Extract;

/// <summary>One form the extractor knows: which files it reads and what it finds in them. Adding a form is adding a row to <see cref="Recognizers.All"/>.</summary>
/// <param name="Name">What it recognizes ('command-ids').</param>
/// <param name="Applies">Whether it reads the file.</param>
/// <param name="Find">What it finds in the file.</param>
public sealed record Recognizer(string Name, Func<RepoFile, bool> Applies, Func<RepoFile, IEnumerable<Found>> Find);
