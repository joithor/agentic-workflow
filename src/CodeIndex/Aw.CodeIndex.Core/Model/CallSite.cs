namespace Aw.CodeIndex.Core.Model;

/// <summary>A raw syntax call or inheritance as the parser saw it, kept so edges can be re-resolved without re-parsing (the 'call_sites' table, spec §4.2).</summary>
/// <param name="Kind">Calls or inherits.</param>
/// <param name="Name">The called or inherited name.</param>
/// <param name="Arity">The argument count (the type-argument count for inherits).</param>
/// <param name="Receiver">'this', 'base', 'type:&lt;name&gt;' or 'other'.</param>
/// <param name="Line">The 1-based line.</param>
public sealed record CallSite(EdgeKind Kind, string Name, int Arity, string Receiver, int Line);
