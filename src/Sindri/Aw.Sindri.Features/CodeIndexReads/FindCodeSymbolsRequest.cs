using Aw.CodeIndex.Core.Facade;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>A symbol search (GET /sindri/code-index/symbols?query=&amp;repo=&amp;match=): by name, path, path#name or ref; exact unless the match is prefix.</summary>
/// <param name="Query">The text.</param>
/// <param name="Repo">One repo, or null for every repo in scope.</param>
/// <param name="Match">'exact' (the default) or 'prefix'.</param>
public sealed record FindCodeSymbolsRequest(string Query, string? Repo = null, string? Match = null) : IQuery<FindSymbolsAnswer>;
