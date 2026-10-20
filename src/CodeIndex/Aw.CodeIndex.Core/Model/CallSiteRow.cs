using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Model;

/// <summary>A stored call site with its caller, as the resolver reads them back.</summary>
/// <param name="Caller">The symbol the call is in.</param>
/// <param name="Site">The call site.</param>
public sealed record CallSiteRow(SymbolRef Caller, CallSite Site);
