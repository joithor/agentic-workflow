# code-index fixtures (phase CI-0c)

Test assets of the Sindri code index (`docs/superpowers/specs/2026-10-19-sindri-code-index-design.md` section 14, CI-0c; the spec is PR #449). Data, not code: no project here is in `AgenticWorkflow.slnx`, no `Directory.Build.props` or glob reaches these `.cs` files (a solution build compiles none of them), and nothing in the app reads them. They are all synthetic; no app-specific name appears.

| Part | What it is for | Read by |
|---|---|---|
| `project/` | A small C# repo (12 files, 54 symbols) with known calls: plain, overloaded, interface-dispatch, extension, generic, local-function, lambda, nested, partial, base and library calls, so every confidence of spec 5.4 occurs | CI-4 (`CSharpParser`, `CallResolver` golden tests), CI-6 (the facade over a built fixture index) |
| `expected.json` | Every symbol (file, qualified name, kind, arity, span, signature) and every caller-to-callee edge with its confidence and ranked targets: 64 call groups, 91 edge rows (19 certain, 10 likely, 13 ambiguous, 22 unresolved). The `conventions` block states the choices the golden rests on | CI-4 golden tests |
| `verify.mjs` | Keeps `expected.json` honest against the sources (lines hold the names, spans fit, targets exist, counts add up). `node verify.mjs` | `scripts/tests/code-index-fixture.test.sh` |
| `hostile/` | The CI12 parse-safety files: `static/` (12 small ones: unterminated string, comment, verbatim string and directive, missing braces, garbage tokens, BOM and mixed line endings, bidi and non-ASCII identifiers, empty, comments only, usings only, self-recursion) and `generate.mjs` for those a repo cannot hold (below) | CI-4 parser-safety integration tests (spec 11), the CI12 gate |
| `quality-sample/pairs.json` | The 50 call-site to definition pairs of the quality probe (spec 11), 30 true and 20 false, from this repo's `src/`. Chosen for the resolver's weak spots (the `weakSpot` field), each with how the lane decided. **Not reviewed by the owner yet**: every row has `reviewed: false`; the owner reads the labels and flips it | the heavy-lane quality probe (CI-4 prints precision and recall, CI-6 reports it) |
| `quality-sample/check.mjs` | The sample's shape, and its texts against `git show <commit>` | `scripts/tests/code-index-fixture.test.sh` |

## The hostile files

`generate.mjs` writes the whole tree (the static files copied in, the generated ones, a relative symlink and `manifest.json`) into an empty directory, deterministically:

```bash
node tests/fixtures/code-index/hostile/generate.mjs --out "$TMP/hostile" [--cap-kb 512]
```

A C# test calls it once per test class (`Process.Start("node", ...)` with an argument list, into a temp directory it owns) and then reads `manifest.json`: one row per file with `path`, `size`, `outcome` and `note`. `--cap-kb` mirrors the profile's `index.maxFileKB`; the over-cap file is cap + 64 KB, `at-cap-boundary.cs` is just under it. To get a git repo, the test commits the tree into a temp repo the temp-repo builder makes (the symlink then becomes mode 120000).

| `outcome` | A parse run must reach |
|---|---|
| `too-deep` | `parsed = 0`, counted `too-deep`, the process alive (the three CI12 names: 100,000-term `a+b+...`, deeply nested parentheses and generics) |
| `bounded` | `too-deep` or a normal parse, inside the time limit, never a crash or hang |
| `survives` | no throw, no hang; symbols from the well-formed part are allowed, none required. `huge-line.cs`, `at-cap-boundary.cs` and `long-identifier.cs` are expected to parse (`After`, `Inside` found) |
| `skipped:too-large` | skipped before parsing (spec 5.1) |
| `skipped:symlink` | skipped at inventory |
| `over-ceiling:symbols` / `:call-sites` | that file fails with a count (spec 5.1 ceilings: 5,000 symbols, 20,000 call sites) |

## Choices (the spec is silent; CI-4 may change them with the golden)

Each one is in `expected.json`'s `conventions`; the ones that read as a limitation of the literal spec rule carry a `limitation` on the edge:

- Qualified names include the namespace; arity of a method is its parameter count without the `this` of an extension method, of a type its type-parameter count; a method group has arity `-1` (any).
- Receivers: `this` for an unqualified call, `base`, `type:<Name>` for a bare upper-case identifier (and object creations, predefined types), else `other`. Object creations match constructors only; a class with no declared constructor has none (`new Calculator()` is `unresolved`).
- Partial declarations are separate symbols, so `Tile : Rectangle` is `ambiguous` between the two parts. `base.Describe()` and an unqualified `Area()` stay `ambiguous` (spec 5.4 prefers no same-type candidate); `shapes.Select(...)` (LINQ) is `likely` to `Registry.Select`; an extension called in static form is `unresolved`. These are the resolver's weak spots, kept visible on purpose.
- The fixture has no manifest, so every file shares one project directory in the ranking. Auto-properties, fields, enum members and a constructor initializer (`: base(...)`) yield no symbol and no call site.
- The 50 pairs name a `commit` (trunk when they were labeled); the line numbers belong to it, so ``caller.text` and `callee.text` re-anchor them on later trunk. A true pair means the callee is the statically bound method or an implementation the call can dispatch to (`kind: dispatch`).
