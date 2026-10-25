namespace Aw.Context.Core.Code;

/// <summary>The data side of the code lens.</summary>
/// <param name="Things">The things the own scope reads or writes.</param>
/// <param name="Files">The migration and schema files that name them.</param>
public sealed record CodeData(IReadOnlyList<CodeThing> Things, IReadOnlyList<CodeDataFile> Files);
