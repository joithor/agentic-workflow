namespace Aw.Sindri.Core.Scoping;

/// <summary>A brief file the scoping station read (scoping spec §8.2).</summary>
/// <param name="Path">The path as the scope input named it, relative to a root.</param>
/// <param name="Text">The file's text.</param>
/// <param name="Version">'sha256:' and the first twelve hex digits of the text's hash, which the brief's source reference carries.</param>
public sealed record BriefFile(string Path, string Text, string Version);
