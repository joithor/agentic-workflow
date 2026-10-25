using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Model;

/// <summary>A symbol still to embed and its stored, scrubbed text (the 'bodies' table).</summary>
/// <param name="Symbol">The symbol.</param>
/// <param name="Name">Its name.</param>
/// <param name="Signature">Its scrubbed signature.</param>
/// <param name="Body">Its scrubbed body.</param>
public sealed record EmbedCandidate(SymbolRef Symbol, string Name, string Signature, string Body);
