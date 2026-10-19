namespace Aw.Context.Core.Code;

/// <summary>Which lists of the code lens were cut.</summary>
/// <param name="Files">Implementing files over 100.</param>
/// <param name="Tests">Test files over 100.</param>
/// <param name="Things">Things over 100.</param>
/// <param name="SchemaFiles">Schema files over 100, or over 20 for a thing.</param>
/// <param name="SearchedFiles">More implementing files than the lexical search uses (50).</param>
public sealed record CodeLensTruncated(bool Files, bool Tests, bool Things, bool SchemaFiles, bool SearchedFiles);
