using Aw.CodeIndex.Core.Model;

namespace Aw.CodeIndex.Core.Facade;

/// <summary>One edge of a callers or callees walk, with its basis and confidence so a reader can tell 'no callers' from 'unknown'.</summary>
/// <param name="Level">1 to 3: how far from the asked symbol.</param>
/// <param name="Other">The symbol at the other end, or null when it is not in this repo or not resolved.</param>
/// <param name="Name">The called name, kept even when unresolved.</param>
/// <param name="Kind">Calls or inherits.</param>
/// <param name="Basis">Syntax or graphify.</param>
/// <param name="Confidence">How sure.</param>
/// <param name="Line">The line in the caller.</param>
/// <param name="Candidates">How many symbols the name matched ('1 of 300').</param>
/// <param name="Coverage">Full for C#, Partial for a graphify language, Unavailable for an unmeasured one.</param>
public sealed record CallEdge(int Level, SymbolHit? Other, string Name, EdgeKind Kind, EdgeBasis Basis, Confidence Confidence, int Line, int Candidates, Coverage Coverage);
