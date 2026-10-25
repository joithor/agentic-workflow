using Aw.CodeIndex.Core.Facade;
using Aw.Context.Core.Trust;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>
/// What a session may read (code-index spec §7.1, the context spec's 'Egress by caller'): the repo text of an answer, which is data and never instructions, comes fenced
/// as untrusted. A ref is left as it is, because the session passes it back; a human's answer is plain text.
/// </summary>
internal static class CodeIndexEgress
{
    internal static FindSymbolsAnswer For(FindSymbolsAnswer answer, bool session)
        => session ? answer with { Hits = [.. answer.Hits.Select(Fenced)] } : answer;

    internal static CodeCallsView For(CodeCallsView view, bool session)
        => session ? view with { Edges = [.. view.Edges.Select(static e => e with { Other = e.Other is { } other ? Fenced(other) : null, Name = Wrap(e.Name) })] } : view;

    private static SymbolHit Fenced(SymbolHit hit) => hit with { Name = Wrap(hit.Name), File = Wrap(hit.File), Signature = Wrap(hit.Signature) };

    private static string Wrap(string text) => UntrustedFence.Wrap("code-index", author: null, text);
}
