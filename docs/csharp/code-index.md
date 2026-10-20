# Aw.CodeIndex (C# build)

`Aw.CodeIndex` is Sindri's code index block: it indexes a repo's symbols, callers and callees, built in the background when the repo's trunk moves. Spec: `docs/superpowers/specs/2026-10-19-sindri-code-index-design.md`. This page is a stub from CI-1 (scaffold and contracts); later units fill it in (CI-11 completes it).

## Layout

| Project | Holds |
|---|---|
| `Aw.CodeIndex.Core` | The facade `ICodeIndex`, the records, the ports (`IRepoSnapshot`, `IIndexStore`, `ISymbolParser`, `IGraphRunner`, `IEmbedder`, `IRepoHeads`, `IHeavyJobLock`, `IBodyScrubber`), `CodeIndexSettings`, `CodeIndexErrors`, `CodeIndexRules` |
| `Aw.CodeIndex.Features` | The `BuildCodeIndex` slice (CI-6) |
| `Aw.CodeIndex.Infrastructure` | The adapters (CI-2 to CI-8) |
| `Aw.CodeIndex.Testing` | The fakes, the temp-repo builder (`GitRepoBuilder`) and the contract bases |
