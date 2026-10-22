# The PR babysitter: Steps, effects and the merge record

Phase **PB-B** (with PB-E's effects) of the PR babysitter and docs sync (`docs/superpowers/specs/2026-10-15-sindri-pr-babysitter-docs-sync-design.md`, §4, §10.1, BD7). This page is what the C# blocks on this branch do; the spec says why. The recipe is `recipes/sindri/workflow.pr-babysitter.yaml`; a block's descriptor is `PrSteps` (`Features/Blocks/Step`) or `PrEffects` (`Features/Blocks/Effect`), and a descriptor change moves the pinned recipe hashes.

Every block reads its inputs as stored facts through the block runtime and talks to the forge only through `IPullRequests` (`Core/Forge`); a forge host outside the profile's list is refused before anything is sent.

## `sindri pr babysit <url>` and `sindri docs sync <url>` (API16)

```
sindri pr babysit <url> [--item <id>] [--json]
sindri docs sync <url> [--item <id>] [--json]
```

The hand start (spec §4.2) in one command. Both go through the running control plane like `workflow start` (same caller class, same `POST /sindri/workflow/instances`; fact contents travel in the request, there are no files to write) and end the same way: the instance's status header, or with `--json` the raw answer. Exit 0 started or found (the same item and attempt answer with the instance they already have), 1 the control plane is not running, 2 invalid input, refused or error.

| Verb | Starts | Facts sent |
|------|--------|------------|
| `pr babysit` | `workflow.pr-babysitter` | `fact:pr` = the link as a JSON string; `fact:handoff`, `fact:phasePacket`, `fact:lineContext`, `fact:workspace` = `{}` |
| `docs sync` | `workflow.docs-sync` | `fact:pr` = the link as a JSON string; `fact:phasePacket` = `{}` |

**The link** is checked before anything is called, for its shape only: https, no credentials or port, `owner/repo/pull/number` (further segments such as `/files` are dropped, and the host is lower-cased). A bad link is exit 2 `SND-CLI-003` with a fix line. Whether the host and repo are ones the approved profile names is the intake Step's to refuse, before any network call (spec §10.2).

**The item** is `--item <id>` when given. Otherwise the verb lists the open items and uses the one whose title is exactly the canonical link, and creates an item titled with the link when none is (the control plane may be started for that, as for `item add`). While the item list is rebuilding the verb refuses rather than risk a second item (fix: pass `--item`). To start another attempt on an item that has ended, use `workflow start --attempt next`.

## `sindri docs check --facts <dir> [--json]` (API17)

A local verb: it runs in this process and needs no running control plane. It runs the rules `step.docs.check` runs, through one shared service (`DocsCheckService`), over a docs worktree.

`<dir>` holds the Step's facts, as the docs agent finds them in `$AW_SINDRI_RUN_DIR`:

| File | Holds |
|------|-------|
| `workspace.json` | `fact:workspace`: the docs worktree's path, one JSON string |
| `docsScope.json` | `fact:docsScope`: `{ "base": "<commit>", "paths": ["docs/**", "*.md"], "candidates": [{ "path": "...", "designNamed": true }] }` |

| Rule | Fails when |
|------|------------|
| `DOCS-PATH-001` | a changed file is neither under the profile's doc globs nor a doc the design names |
| `DOCS-LINK-001` | a relative link in a changed `.md` file does not resolve inside the worktree |
| `DOCS-MD-001` | a changed `.md` file opens a code fence it never closes |
| `DOCS-CHANGE-001` | the worktree holds no change since `base` |

Output: one line per finding with its fix, then the verdict; `--json` prints `{ "findings": [{ rule, item, message, fix }], "passed": bool }`.

Exit codes: 0 no findings, 1 findings, 2 usage or a tool error (a missing directory or fact file, a worktree git cannot read).

## Steps (`Features/Pr`)

| Block | Outcomes | Does |
|---|---|---|
| `step.pr.intake` | `seeded` | Reads the pull request once, outputs `fact:workspace` (empty when none given) and seeds the empty facts every later Step reads (`decision`, `ownerNote`, `handoffNote`, `fix`, `reply`, `recheck`). Appends the `start` row. An unreadable PR fails the Step |
| `step.pr.snapshot` | `mergedDocsOpen`, `merged`, `closed`, `restack`, `conflict`, `ciRed`, `comments`, `stale`, `docsRed`, `pending`, `green` | One forge read of the PR, its threads, its comment marks and (when the instance holds `fact:docsPr`) the docs PR; the first outcome that holds in §4.3's order. Writes `fact:prState` on every outcome and `fact:mergedSha` when merged (the merge commit `fact:merge` holds, else the head). Appends the `snapshot` row |

`fact:prState` is the PR view in camelCase: `link`, `state` (`green` when the outcome is, else the PR's own), `pullState`, `headSha`, `stale`, `head`, `mergeable`, `reviewDecision`, `unresolvedThreads`, `checks` (`state`, `sha`, `failing`), `queue` (`unhandled`, `held`, `untrusted`, `bots`), `comments` (each with its latest mark), `docs`, `mergeBlockedBy`, `workspaceAhead`, `outcome` and `at`. A comment with a mark is handled; a thread carrying a Sindri marker is Sindri's own and never queued.

## Effects (`Features/Blocks/Effect`)

| Block | Replies | Does |
|---|---|---|
| `effect.pr.push` | `pushed`, `pushedSource`, `nothing`, `headMoved`, `rejected` | Pushes the workspace head to the PR head with a lease on the head `fact:prState` saw; never forces and never rebases. Carries `fact:push` (`sha`, `commits`, `comments`, `source`, `cause`, `ciFixes`; `ciFixes` counts a `ci` push once, what `gate.pr.autofix-cap` reads). A dispatch repeated after its push landed is `pushed` with `Existed`. Appends the `push` row on `pushed` and `pushedSource` |
| `effect.pr.record` | none | Appends a `recheck` or `docs` row from the fact the child returned (`kind`, `result`), or an `end` row on a transition into a terminal (`kind: end`, `terminal`, and `notes` on `rework`) |
| `effect.pr.rerun-checks` | none | Asks the forge to run the failed checks again on the commit they failed on (`checks.sha`, else `head.sha`). Writes no row |
| `effect.pr.merge` | `merged`, `blocked`, `headMoved` | Merges with the forge's expected-head check; the method is the recipe's `method`, else the profile's `workflow.pr.mergeMethod`, else squash. Human caller only (W23). Appends the `merge` row on `merged` |
| `effect.pr.reply` | none | Forms `thread`, `reaction`, `fixed`, `fixedReplies`, `hold` and `none`. A post is made once per effect identity (Sindri's marker is derived from it); every form writes its comment mark (W26) keyed by the identity, so a redelivery writes none twice; reply text is scrubbed first. Appends a `comment` row |

## The merge record (BD7)

`babysit.jsonl` is in the instance's run area (`IRunArea`). It is appended and never edited. One writer, `MergeRecord` (`Features/Blocks/Effect`, internal), implements the port `IMergeRecord` (`Core/Workflow/Blocks`): `Append(MergeRow)` returns true when it wrote the row, false when a row with that identity was already there, and an error for an unknown kind or an unwritable run area. A Step or an effect takes `IMergeRecord`, never the class, so slices share it without crossing (AW1003).

Every row carries `kind`, `identity`, `instance`, `headSha` and `at`; the kind adds its fields, and a field cannot overwrite one of those columns. The identity is what keeps a retry from writing twice:

| Writer | Identity |
|---|---|
| an effect | `EffectContext.Identity`, `<instance>:<transition sequence>:<index>` |
| `step.pr.intake` | `<address>:start` |
| `step.pr.snapshot` | `<address>:snapshot:<previous prState.at, or first>`, so each successive snapshot has its own and a retry has the same |
| `effect.pr.reply` `fixedReplies` | `<identity>:<comment id>`, one per comment answered |

| Row `kind` | Written by | Fields besides the fixed ones |
|---|---|---|
| `start` | `step.pr.intake` | `pr`, `base` (always null for now: `PullView.Base` exists since #674, but the row does not read it yet), `handoffHash` (`sha256:` of `fact:handoff`, null without one), `phase` (`fact:phasePacket` id), `startedBy` (`line` when `fact:lineContext` is non-empty, else `hand`) |
| `snapshot` | `step.pr.snapshot`, when the outcome or the head differs from the previous `fact:prState` | `outcome`, `checks`, `queue` |
| `push` | `effect.pr.push` | `cause`, `commits`, `comments` (empty for now), `source` |
| `comment` | `effect.pr.reply` | `comment`, `type` (the `decision` argument's `type`), `outcome` (`replied`, `reacted`, `fixed`, `held`, `skipped`), `commit` |
| `recheck`, `docs` | `effect.pr.record` | `result` |
| `end` | `effect.pr.record` | `terminal` (`merged`, `closed`, `stopped`, `rework`), `reworkNotesHash` (`sha256:` of `fact:reworkNotes`' content, on `rework` only) |
| `merge` | `effect.pr.merge` | `method`, `sha`, `actor` (the optional `actor` argument; null without one) |

A step with no instance address still runs and writes nothing.

## Not yet

| Gap | Why |
|---|---|
| Reading `PullView.Base` | `PullView` carries `Base` since #674, but the rows don't read it yet, so `start.base` is null, `step.pr.snapshot` never reports `restack` and `head.ref` and `base` are not in `fact:prState` |
| `restack`, `docsMerge` and `merge-docs` rows | `effect.pr.restack`, `effect.pr.create-stacked` and `effect.pr.merge-docs` are descriptors only until the engine's Retarget lands |
| `effect.pr.open` labels | The open effect does not apply labels yet |
| `pr-watch` poller | Nothing polls a waiting PR; `ready-check` and the `await` wake-ups wait on it |
| Hand-off hash check, workspace cut | Intake records the hash but does not check it, and does not cut a workspace from the PR head: no port reaches either |
| `merge.actor`, `push.comments` | The engine's effect context carries no actor and the push effect has no comment ids; both stay null or empty |
