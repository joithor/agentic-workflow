import re
root='/home/thor/repos/aw-s9a/src/Context/'
def sub(p,a,b,count=1):
    s=open(root+p).read()
    assert a in s,(p,a)
    open(root+p,'w').write(s.replace(a,b,count))
# CodeLexicalSearch: share two helpers
sub('Aw.Context.Features/GetCode/CodeLexicalSearch.cs','    private static ContextRepo? RepoOf(','    internal static ContextRepo? RepoOf(')
sub('Aw.Context.Features/GetCode/CodeLexicalSearch.cs','    private static bool IsSha(','    internal static bool IsSha(')
# CodeTexts
sub('Aw.Context.Features/GetCode/CodeTexts.cs','''        Data = new CodeData(''','''        Callers = Symbols(view.Callers, text),
        Callees = Symbols(view.Callees, text),
        Data = new CodeData(''')
s=open(root+'Aw.Context.Features/GetCode/CodeTexts.cs').read()
s=s.rstrip()[:-1].rstrip()+'''

    // Names, signatures and paths of the index are repo text: one clean line each, like the paths above.
    private static CodeSymbols Symbols(CodeSymbols symbols, MapText text) =>
        symbols with { Items = [.. symbols.Items.Select(e => e with { Symbol = Site(e.Symbol, text) })] };

    internal static SymbolSite Site(SymbolSite site, MapText text) => site with
    {
        Ref = text.Line(site.Ref),
        Name = text.Line(site.Name),
        Kind = text.Line(site.Kind),
        Path = text.Line(site.Path),
        Signature = site.Signature is null ? null : text.Line(site.Signature),
    };
}
'''
open(root+'Aw.Context.Features/GetCode/CodeTexts.cs','w').write(s)
sub('Aw.Context.Features/GetCode/CodeTexts.cs','using Aw.Context.Core.Map;','using Aw.Context.Core.Map;\nusing Aw.Context.Core.Symbols;')
# GetCodeService
p='Aw.Context.Features/GetCode/GetCodeService.cs'
sub(p,'CodeLexicalSearch lexical, IScrubberFactory scrubbers)','CodeLexicalSearch lexical, CodeSymbolSearch symbols, IScrubberFactory scrubbers)')
sub(p,'''        return CodeTexts.Clean(CodeLensMerge.With(graphHalf.Value, found), text);''','''        var merged = CodeLensMerge.With(graphHalf.Value, found);
        var indexed = await symbols.Find(merged, read.Value, settings.Value, request.Scope, cancellationToken).ConfigureAwait(false);
        return CodeTexts.Clean(CodeLensSymbols.With(merged, indexed), text);''')
sub(p,'the lexical half by <see cref="CodeLexicalSearch"/> at the map\'s commit,','the lexical half by <see cref="CodeLexicalSearch"/> at the map\'s commit, the callers and callees by <see cref="CodeSymbolSearch"/> from the code index,')
# GetFileMapService
p='Aw.Context.Features/GetFileMap/GetFileMapService.cs'
sub(p,'internal sealed class GetFileMapService(MapReader reader, IRepoReader repos)','internal sealed class GetFileMapService(MapReader reader, IRepoReader repos, ICodeSymbolSource symbols)')
sub(p,'''        return FileMapBuilder.Build(new FileMapBasis(map.Value.Snapshot.Tag, tree.Value.Sha), query, tree.Value.Files, map.Value.Graph);
    }
''','''        var built = FileMapBuilder.Build(new FileMapBasis(map.Value.Snapshot.Tag, tree.Value.Sha), query, tree.Value.Files, map.Value.Graph);
        return built.IsError ? built.Error : built.Value with { Symbols = await Symbols(repo.Value, map.Value.Snapshot.Version, built.Value, cancellationToken).ConfigureAwait(false) };
    }

    // The symbols column (S-9a): what the code index says about the files this listing shows, as of the map version's commit. A directory row has none; a read that fails is a column that says so, never an error of the route.
    private async ValueTask<LensSymbols> Symbols(ContextRepo repo, MapVersion version, FileMapView view, CancellationToken cancellationToken)
    {
        var asOf = repo.Name == version.Repo && Features.GetCode.CodeLexicalSearch.IsSha(version.Commit) ? version.Commit : null;
        var head = await symbols.Heads(repo.Name, asOf, cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            return Down(head.Error.Code);
        }

        var paths = view.Entries.Where(e => e.Type == FileEntryType.File).Select(e => e.Path).Take(CodeRules.MaxSymbolFiles).ToList();
        if (paths.Count == 0 || !CodeIndexReading.Readable(head.Value))
        {
            return CodeIndexReading.FileMap(head.Value, []);
        }

        var files = await symbols.FileSymbols(repo.Name, paths, asOf, cancellationToken).ConfigureAwait(false);
        return files.IsError ? Down(files.Error.Code) : CodeIndexReading.FileMap(files.Value.Head, files.Value.Rows);
    }

    private static LensSymbols Down(string code) => LensSymbols.Unavailable with { Reason = "index unavailable", Code = code };
''')
sub(p,'using Aw.Context.Core.Map;','using Aw.Context.Core.Code;\nusing Aw.Context.Core.Map;\nusing Aw.Context.Core.Symbols;')
# registration
p='Aw.Context.Features/ContextFeaturesServiceCollectionExtensions.cs'
sub(p,'        .AddScoped<CodeLexicalSearch>()','        .AddScoped<CodeLexicalSearch>()\n        .AddScoped<CodeSymbolSearch>()')
sub(p,'        services.TryAddSingleton<ILabeler, DisabledLabeler>();','        services.TryAddSingleton<ICodeSymbolSource, NoCodeSymbolSource>();   // the host that wires the code index (Sindri\'s Host adapter) replaces it\n        services.TryAddSingleton<ILabeler, DisabledLabeler>();')
sub(p,'using Aw.Context.Core.Subgraph;','using Aw.Context.Core.Subgraph;\nusing Aw.Context.Core.Symbols;')
