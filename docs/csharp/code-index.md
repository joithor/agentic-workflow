# Aw.CodeIndex (C# build)

`Aw.CodeIndex` is Sindri's code index block: it indexes a repo's symbols, callers and callees, built in the background when the repo's trunk moves. Spec: `docs/superpowers/specs/2026-10-19-sindri-code-index-design.md`. This page is a stub from CI-1 (scaffold and contracts); later units fill it in (CI-11 completes it).

## Layout

| Project | Holds |
|---|---|
| `Aw.CodeIndex.Core` | The facade `ICodeIndex`, the records, the ports (`IRepoSnapshot`, `IIndexStore`, `ISymbolParser`, `IGraphRunner`, `IEmbedder`, `IRepoHeads`, `IHeavyJobLock`, `IBodyScrubber`), `CodeIndexSettings`, `CodeIndexErrors`, `CodeIndexRules` |
| `Aw.CodeIndex.Features` | The `BuildCodeIndex` slice (CI-6) |
| `Aw.CodeIndex.Infrastructure` | The adapters (CI-2 to CI-8) |
| `Aw.CodeIndex.Testing` | The fakes, the temp-repo builder (`GitRepoBuilder`) and the contract bases |

## Errors

Area `SND-CODEIDX` (`CodeIndexErrors` in `Aw.CodeIndex.Core`); 014 to 029 are reserved. Layer codes (006 to 009) are carried in a layer row and the build report and fail only the call that depends on the layer.

| Code | Kind | Meaning |
|---|---|---|
| `SND-CODEIDX-001` | Invalid | the `index:` section has a bad key or value (the message starts with the key path), or a repo name is not file-safe |
| `SND-CODEIDX-002` | NotFound | the repo path is not a git repo, git refuses it as unsafe, or the commit is not in it |
| `SND-CODEIDX-003` | Invalid | the tracked input is over `index.maxTotalMB` |
| `SND-CODEIDX-004` | Unavailable | every heavy-job slot stayed busy past the wait (a job result code only; the job ends deferred) |
| `SND-CODEIDX-005` | Invalid | the embedding URL is not loopback, or the model is a cloud model |
| `SND-CODEIDX-006` | Unavailable | the embedding server failed or answered in an unexpected shape (a layer code) |
| `SND-CODEIDX-007` | Unavailable | no network sandbox for graphify (a layer code) |
| `SND-CODEIDX-008` | Unavailable | graphify is missing, failed, timed out, or wrote no usable or too large graph (a layer code) |
| `SND-CODEIDX-009` | Unavailable | a tool or model does not match `context/code-index.lock` (a layer code) |
| `SND-CODEIDX-010` | Unavailable | the index file is damaged; it is deleted and rebuilt on the next tick |
| `SND-CODEIDX-011` | NotFound | the symbol ref names no symbol in this index (also a repo outside the caller's scope) |
| `SND-CODEIDX-012` | Invalid | a query is empty, too broad, or over a limit |
| `SND-CODEIDX-013` | NotFound | no index for this repo yet (also a repo outside the caller's scope) |

## Contracts (CI-1)

- Typed values: `RepoName` (file-safe, at most 64 characters), `CommitSha` (40 lower-case hex, never an abbreviated SHA: an adapter resolves and passes full SHAs), `SymbolRef` (`<repo>:<path>#<qualified name>`).
- Every Callers/Callees row carries `coverage` (`full` for C#, `partial` for a graphify language, `unavailable` for an unmeasured one), and the answer carries one too (`CallCoverage.Of`): any partial row, or a partial language, marks the whole answer partial, and an empty partial answer reads "none found", never "none".
- Ports, each documented with what an implementation must guarantee: `IRepoSnapshot` (+ `IBlobReader`), `IIndexStore` (+ `IIndexReader`, `IIndexBuild`), `ISymbolParser`, `IGraphRunner`, `IEmbedder`, `IRepoHeads`, `IHeavyJobLock`, `IBodyScrubber`.
- The facade `ICodeIndex`: `Build`, `Status`, `Heads`, `ProbeLayers`, `FindSymbols`, `FileSymbols`, `Callers`, `Callees`, `Similar`, `Neighbors`; every call takes a `CodeIndexScope` (the caller's repos, each with its effective settings, and the caller).
- `Aw.CodeIndex.Testing`: `GitRepoBuilder` / `TempGitRepo` (git init, commits, symlinks, a submodule entry, a path swapped after the commit, a promisor clone), `FakeRepoSnapshot`, `FakeIndexStore`, `FakeRepoHeads`, `FakeHeavyJobLock`, `FakeBodyScrubber`, `FakeSymbolParser`. The contract bases come with the adapters (CI-2, CI-3, ...).
- `Aw.CodeIndex.Infrastructure` is `AwAotCompatible=false`: the C# parser (CI-4) uses Roslyn, which is not trim or NativeAOT safe.
