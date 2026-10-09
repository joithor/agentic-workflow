# Sindri code index

One index per repo at `$AW_STATE_DIR/sindri/index/<repo>.db`, built from tracked files only (`git ls-files`, minus `index.denyPaths`, matched case-insensitively; symlinks never followed). Spec: §6.2.

## First run

```bash
sindri profile init --ring0 && sindri profile approve   # or: profile init, repo add <path>, profile approve
sindri index setup      # checks Ollama, pulls the embedding model, installs the pinned graphify with uv, probes the sandbox
sindri index build      # full build; also creates the bare mirror at $AW_STATE_DIR/sindri/mirrors/<repo>.git
sindri scrub --install-pre-commit   # hook v2: secret scan, then shape recording
```

`index setup` reads the **approved** profile, so approve first. It needs Ollama (running, with the model), `uv`, and macOS `sandbox-exec` (or Linux `bwrap`). Without one of them that layer is `unavailable` and everything else works.

## Layers

| Layer | What it holds | Built with | Off switch |
|---|---|---|---|
| structure | functions, methods, arrows, classes: name, signature, lines, exported, complexity, callees | TypeScript compiler API (TS and JS) | none |
| clones | normalized AST hash per symbol; MinHash/LSH bands over 5-token shingles | core | none |
| deps | `package.json` dependencies with purpose tags (date, http, id, validation, …) | core | none |
| embeddings | one vector per symbol | Ollama on loopback (`index.embeddings`) | `index.embeddings.enabled: false` |
| graph | module and call graph | graphify in a sandbox, on a snapshot of the tracked source and docs files | `index.graph: none` |

**Offline guarantee.** Code never leaves the machine.

- The embedding URL must be the IP literal `127.0.0.1` or `[::1]` with no credentials (not `localhost`), requests refuse redirects, and a model whose name contains `cloud` is refused, because Ollama cloud models forward the text to a remote service. Node's `fetch` ignores `HTTP(S)_PROXY` unless `NODE_USE_ENV_PROXY=1` is set; `doctor` warns if it is. A loopback port that is really an SSH or port-forward tunnel is not this machine; that is documented, not detected.
- graphify runs under `sandbox-exec` (macOS) or `bwrap --unshare-net` (Linux): no network, no LaunchServices opens or Apple events on macOS (so it can't start a browser outside the sandbox with `open URL`), writes only to its snapshot dir, and `~/.ssh`, `~/.aws`, `~/.gnupg`, `~/.agentic-workflow` and `~/Library/Keychains` hidden, with a cleaned environment. Everything else stays readable, so a compromised graphify could still read other files in your home directory, and on macOS it can still reach other mach services (securityd and the like); `graph.json` is size-capped and treated as untrusted. It doesn't run at all without a sandbox. `sindri index setup` proves the sandbox denies the network with a positive control: the same request must succeed outside it.
- graphify is installed at an exact pin (`sindri.graphifyPin` in `sindri/package.json`, at least 14 days old) with `uv tool install --exclude-newer <the pin's upload date>`, which also age-gates its dependencies.
- Symbol bodies are scrubbed before they are stored. The state dir holds source text (the index and the mirror): keep it out of cloud sync and backups you don't control. The mirror holds the repo's full, unfiltered history, including deleted secrets and denied paths, so it is never mounted into a guest without a deny filter.

## Commands

```bash
sindri index setup [--dry-run]          # steps: ollama, ollama-server, embedding-model, graphify, sandbox (all reported, none skipped by an earlier failure)
sindri index build [--quick] [--full]   # incremental; --quick: structure, clones and deps only; --full rebuilds; takes the box-wide heavy-job lock
sindri index status                     # every repo: age, commit, per-layer status with the reason; exit 1 when an index is missing or stale
sindri index query <name>               # a symbol's exact and near clones (for people: output is repo text, never feed it to a session)
sindri repo add <path> [--name NAME]    # profile entry (then sindri profile approve); the next full index build mirrors it
sindri shape report [--recent N]        # signals by type, outcomes and precision; --recent lists signals with their evidence
```

Embeddings: if the URL or model is refused (the profile schema normally catches this first), `index build` does not abort. The embeddings layer reports `unavailable` with the reason and the other layers build. A failed layer is `unavailable` with its reason; the other layers stay usable. Builds write a temp copy and rename it, so an interrupted build leaves the previous index in place.

## Freshness

An hourly `sindri index build --quick` (launchd) refreshes structure, clones and deps; the nightly build at 03:15 refreshes everything. Both read the files **as checked out** (the working tree), so keep `main` checked out in the indexed checkout; uncommitted work there becomes base data. Each shape run records the index age. The bare mirror is for a later plan (guest sandboxes).

## Shape signals (record-only)

The pre-commit hook (installed by `sindri scrub --install-pre-commit`) scans for secrets, then runs `sindri shape --record --staged`. That compares the staged changes of the commit's own worktree (an in-memory overlay; denied paths and files over `index.maxFileKB` are skipped) with the index and writes the signals to `$AW_STATE_DIR/sindri/spool/`. It opens the ledger read-only and always exits 0: nothing blocks a commit until rollout step 3b. The hourly `observe` and `sindri shape report` move the spool into the ledger.

| Signal | Fires when | Default threshold (`shape.thresholds`) |
|---|---|---|
| `reinvented:exact` | a new symbol has the same normalized AST as another file's symbol | none |
| `generalize:near-clone` | MinHash similarity with another symbol, both at least N tokens | Jaccard 0.8, 60 tokens |
| `reinvented:name` | name and signature similar to an exported or utility symbol | 0.85 |
| `reinvented:graph` | call set overlaps an exported or utility symbol's (≥ 3 calls each) | 0.5 |
| `reinvented:embedding` | embedding cosine and AST similarity both high (only within the commit budget) | 0.9 and 0.6 |
| `reinvented:dependency` | a new dependency shares a purpose tag with an existing one | none |
| `simpler:diff-size` | added lines over the size budget (`shape.defaultSize`, default S = 250) | `shape.sizeBudget` |
| `simpler:complexity` | a symbol's complexity grew by more than the limit | 10 |
| `simpler:exports` | more new exports than the size class allows | `shape.exportAllowance` |

Names in signal details are wrapped in `<untrusted>…</untrusted>` with `&`, `<` and `>` escaped: they come from the repo, not from Sindri. To turn recording off, set `shape.record: false` (and approve the profile); to remove the hook step, reinstall without it or delete the `shape --record` line.

## Outcomes and precision (the 3b bar)

Each recorded run keeps `git write-tree` of the staged index. `observe` and `shape report` link a run to the commit with that tree. Once the commit is `shape.outcomeDays` (14) old, each signal gets an outcome:

| Outcome | Meaning |
|---|---|
| `kept` | the flagged symbol (same file, same name, same AST) or dependency is still there |
| `acted-on` | it was changed or removed |
| `dropped` | the commit was never made, was amended, or never reached the default branch |
| `n/a` | diff size and export count have no flagged symbol |

`sindri shape report` prints, per type, `SIGNALS | LABELED | ACTED-ON | KEPT | PRECISION | TOWARD 3b`. Precision is `acted-on / (acted-on + kept)`, and 3b wants at least 30 labeled signals and precision at least 0.70 per layer (`ready`). Two known measurement effects:

- With squash merges, a signal whose flagged code was reworked later in the same PR has no version with the recorded hash on the default branch, so it is labeled `dropped` and excluded from precision. Measured precision is therefore biased **low** (conservative for the 3b bar). Recovering those signals needs PR data, which a later plan adds.
- A file renamed on the default branch after the merge reads as `acted-on` although the code was kept, which slightly **inflates** precision.

This is an **outcome proxy, not a human label**: code is changed for other reasons too, and a correct signal can be ignored. It exists so thresholds can be tuned without anyone hand-labeling (spec invariant 9).

## Troubleshooting

| `index status` or `doctor` says | Meaning | Fix |
|---|---|---|
| `no index` | never built | `sindri index build --repo <repo>` |
| `stale` | older than `index.maxAgeHours` | `sindri index build`; if the launchd job should have run, read `~/.agentic-workflow/sindri/index-launchd.log` |
| `never built` | an empty index file | `sindri index build --repo <repo>` |
| `embeddings unavailable (Ollama not answering …)` | Ollama is down or the model is missing | start Ollama, `sindri index setup` |
| `graph unavailable (…)` | graphify missing, wrong version or failed | `sindri index setup`, then `sindri index build --full` |
| `embeddings pending` / `graph pending` | only quick builds have run | `sindri index build` |
| `uv: command not found` during `index setup` | `uv` is missing; `./setup.sh --with-sindri` installs it with Homebrew (`brew install uv`) when it can, and warns otherwise | `brew install uv` or see the uv install docs, then `sindri index setup` |
| `SND-INDEX-008` (graphify output not usable) | the graphify test fixture (`sindri/tests/fixtures/graphify/graph.json`) is synthetic until the switch-on re-records it from a real sandboxed run, so key names may need a fix then | re-record the fixture from the pinned graphify and fix `parseGraphJson` if a key differs |
| `SND-INDEX-001` / `heavy-lock held` | another heavy job (a test run, another build) holds the lock | wait; a lock whose holder is dead on this host (pid gone, or recorded on another boot) is reclaimed automatically; otherwise run the fix `sindri doctor` prints: `rmdir ${AW_HEAVY_JOB_LOCK:-$AW_STATE_DIR/locks/heavy-job.lock}` and delete the `.holder.json` file beside it |
| `sindri-shape: skipped (no index; …)` in a commit | the hook found no index for this repo | `sindri index build` |
