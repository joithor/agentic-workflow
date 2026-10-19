namespace Aw.Context.Core.Code;

/// <summary>The full counts behind the code lens's lists (before the 100-entry cut).</summary>
/// <param name="Files">Implementing files.</param>
/// <param name="Tests">Test files.</param>
/// <param name="Things">Data things.</param>
/// <param name="SchemaFiles">Schema files.</param>
/// <param name="FilesWithoutTest">Implementing files no test covers.</param>
/// <param name="Unlocated">Implementing files with no repo evidence, which the lexical search skips.</param>
public sealed record CodeLensCounts(int Files, int Tests, int Things, int SchemaFiles, int FilesWithoutTest, int Unlocated);
