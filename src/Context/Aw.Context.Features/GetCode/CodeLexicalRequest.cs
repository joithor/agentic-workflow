using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;

namespace Aw.Context.Features.GetCode;

/// <summary>What the lexical step needs of a request: the graph half of the view, the map read, the settings, the caller's scope and the cleaner of map names.</summary>
/// <param name="View">The graph-half view.</param>
/// <param name="Map">The map the caller may read.</param>
/// <param name="Settings">The checked settings.</param>
/// <param name="Scope">The profile's repos and the caller.</param>
/// <param name="Clean">Cleans one map name before it becomes a search term.</param>
internal sealed record CodeLexicalRequest(CodeView View, VisibleMap Map, ContextSettings Settings, ContextScope Scope, Func<string, string> Clean);
