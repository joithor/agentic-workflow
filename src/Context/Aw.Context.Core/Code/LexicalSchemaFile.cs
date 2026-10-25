using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Code;

/// <summary>A schema file that names a thing.</summary>
/// <param name="Thing">The thing.</param>
/// <param name="Path">The schema file.</param>
/// <param name="Matched">The form of the thing's name that matched.</param>
public sealed record LexicalSchemaFile(NodeKey Thing, string Path, string Matched);
