# Sindri evolve: all-sessions input behind a hardened publish gate — design

Status: approved 2026-10-09; plans: `docs/superpowers/plans/2026-10-09-sindri-evolve-privacy-b1-publish-gate.md`, `...-b2-all-sessions.md`
Builds on: Plan 5 (self-evolution, #81, #82). Blocks: Plan 5 Task 14 (switch-on).

## 1. Why

Plan 5 reads only sessions whose cwd is the toolkit repo (Plan 5 amendment 6) and gates published
proposals with a literal `privacy.denyTerms` list (amendment 2). Both are wrong for the goal:

- **Too narrow an input.** Most agent work happens in other repos and with other providers. Evolve
  should learn from every session Sindri can read (Claude Code, Codex, Cursor; any repo) and turn that
  into changes to the toolkit itself.
- **Too weak a gate.** Workplace data can't be enumerated as a word list. Anything a workplace MCP
  server can fetch (tickets, docs, code, messages) is private, and protection has to follow the
  source, not a list of terms.

Widening the input makes the gate the only boundary that matters, so the gate is rebuilt first.

## 2. Threat model

- **Asset:** workplace information: names, code identifiers, ticket keys, product and domain detail.
- **Boundary:** the plan file `sindri evolve publish` writes into the toolkit repo. The toolkit repo is
  public; anything committed there is published. Everything before it (transcripts, ledger, index
  DBs, staged previews) stays local in 0600/0700 state.
- **Not a boundary:** model calls. Evolve already sends transcript text to the same model provider
  every session uses. Model **input** is unrestricted; model **output** headed for the plan file is
  restricted.
- **Leak paths:** model-written proposal text (title, rationale, change description, file list,
  replacement text) that copies or paraphrases workplace content from its inputs.

## 3. Decisions

| # | Decision |
|---|---|
| D1 | Evolve reads all sessions from every provider and repo Sindri can access (B2). |
| D2 | Public tasks never quote transcripts. Evidence is opaque refs only; `sindri evolve show` resolves them locally. Enforced by a verbatim-overlap gate (§5.4). |
| D3 | Every proposal passes a model generalization pass (rewrite, then a separate judge) before it can be published. |
| D4 | The derived private deny list is computed at publish time from the private repos' index DBs. Nothing new is stored. |
| D5 | Repos are private by opt-in (`private: true` in `repos/<name>.yaml`); doctor warns on any non-toolkit repo with no explicit value. |
| D6 | No source exclusion and no turn taint. `privacy.denySources` is not added. |
| D7 | `privacy.denyPatterns` is publish-only, with no limit on count or length. |
| D8 | One spec, two PRs: B1 (publish privacy) merges before B2 (widened inputs). Task 14 waits for both. |

## 4. Data flow

```
sources: all repos × claude / codex / cursor                         [B2]
  └─ readSessions → SessionLine {provider, repo, session, n, ref, ...}
       ├─ telemetry --since         (hook fires, all sessions)
       ├─ correct --since           (human turns, all sessions)
       └─ reflect --repo R --pr N   (that repo's PR + its branch's lines)
            └─ proposals (evidence = refs) → ledger
stage                                                                 [B1]
  └─ generalize: rewrite → judge → proposal_public row, or held
publish                                                               [B1]
  └─ gate, first hit holds; reasons name the layer, never the match:
       1 denyTerms  2 denyPatterns  3 derived identifiers  4 verbatim overlap
       5 email / home path / scrubber (existing)
  └─ append to the plan file on a branch; the owner opens and merges the PR
```

## 5. B1: publish privacy

### 5.1 Generalization pass (at `stage`)

Runs on every proposal being staged, regardless of source: toolkit sessions also carry echoes of
private context (for example, session-start memory output).

- **Rewrite.** Input: the proposal and the scrubbed excerpts behind its evidence refs, fenced as
  untrusted. Instruction: restate the proposal as a change to agent behavior or to the toolkit, with no
  product, company, customer, codebase, ticket or people detail; keep the artifact, kind and evidence
  refs. Output: `ProposalSchema`. The artifact id and evidence refs must equal the original's; a
  rewrite that changes them is a failure.
- **Judge.** A separate call. Input: the rewritten proposal and the same evidence; it does not see the
  rewrite prompt or reasoning. Output: `{leaks: boolean, category: string}`. Instruction: could a
  reader of the rewritten text learn anything specific to the workplace behind the evidence?
- **Outcome.** `leaks: false` writes a `proposal_public` row and stages the proposal. `leaks: true`
  holds it with reason `generalize: judge found <category>`. A failed or invalid call holds it with
  `generalize: rewrite failed` / `generalize: judge failed`. The next `stage` retries every held
  proposal that has no `proposal_public` row (publish-gate holds always have one). Nothing is staged from the original text.
- **Budget.** Two calls per staged proposal, at most `evolve.maxOpenProposals` per run, under the
  existing evolve budget. Calls use the evolve model config (`role: "draft"`).
- **Storage.** Ledger migration v4 → v5 (backup `ledger.db.bak-v4`, the existing `migrateWith`
  pattern) adds `proposal_public (proposal_id TEXT PRIMARY KEY REFERENCES proposals(id), body TEXT NOT
  NULL, judge_category TEXT NOT NULL, model TEXT NOT NULL, created_at TEXT NOT NULL, epoch INTEGER NOT NULL)`. The original
  proposal stays in `proposals`, local only.
- **Publish reads only `proposal_public`.** A staged or held proposal with no row cannot be published.

### 5.2 `privacy.denyTerms` (obvious names)

Unchanged in code. `SND-EVOLVE-015` (publish refuses while the list is empty) stays. The owner's
names go into the private profile and are approved once, together with `denyPatterns`.

### 5.3 `privacy.denyPatterns`

- Profile: `privacy.denyPatterns: string[]`, default `[]`, no limit on count or pattern length.
- Each must compile as a JavaScript regex; `sindri profile validate` fails on one that doesn't.
- Matched with the `iu` flags against the visible normalized text (NFKC, invisibles stripped, the
  `visibleForm` already in `privacy.ts`).
- Used by publish only. Not added to the scrubber: `scrub.extraPatterns` redacts everywhere and would
  strip ticket refs from scope maps.
- Docs use neutral examples (`ABC-\d+`).

### 5.4 Derived private identifiers

At publish, for each repo with `private: true`:

1. Open `index/<repo>.db` read-only. Missing DB or no built symbols layer → `SND-EVOLVE-017`, publish
   refuses.
2. Collect symbol names (`symbols.name`) and file path segments (each directory and the basename
   without extension, from `files.path`).
3. Keep tokens that are ≥ 5 characters, not in `/usr/share/dict/words` (case-insensitive), and not in
   the toolkit repo's own index vocabulary (its symbol names and path segments). A missing word list →
   `SND-EVOLVE-018`.
4. Lowercase into an in-memory set.

The candidate text (raw proposal fields plus the rendered task) is tokenized into identifiers
(`[A-Za-z_$][A-Za-z0-9_$]*`) and path segments, lowercased and checked against the set. A hit holds
the proposal with `contains a private-repo identifier`. The set is never written, logged or printed.
False positives (public library names used in a private repo) hold a proposal; they never leak one.
`publish --dry-run` reports held counts per layer so the noise is visible before a real run.

### 5.5 Verbatim overlap

Shingle the rendered task into runs of 8 normalized words (`normalizeForPrivacy`, split on spaces).
Shingle every transcript line behind the proposal's evidence refs (resolved locally; the rewrite keeps
them unchanged). A shared shingle holds the proposal with `copies transcript text`, unless the
shingle also appears in a file `git ls-files` lists in the toolkit repo (working-tree text) (so quoting the toolkit's own
public skill text passes). Toolkit shingles are computed once per publish run from `git ls-files` text
files.

### 5.6 Gate order and reasons

Terms, patterns, derived identifiers, verbatim overlap, then the existing email, home-path and
scrubber checks. The first hit holds. Held reasons name the layer only and never contain matched text.
Held proposals keep their local preview and leave the cap (unchanged).

## 6. B2: widened inputs

### 6.1 Session readers

- `readRepoSessions(dir, repo, ...)` is replaced by `readSessions(sources, since, ...)`, in
  `sindri/src/evolve/sources/`.
- Discovery and text extraction for Claude, Codex and Cursor are ported from `scorer/src/transcript/`
  (copied, not imported, the precedent `correct.ts` set). Codex threads imported from Claude are read
  only from the point they ran in Codex, as the scorer does. Claude subagent files and linked
  worktrees are read.
- Dedupe of resumed and forked copies (same timestamp and text) is kept and applied across providers.
- Profile: `sources.transcripts.dir` stays the Claude dir; new `sources.transcripts.codex.dir` and
  `sources.transcripts.cursor.dir` default to `~/.codex/sessions` and `~/.cursor/projects`. Additive, so no
  profile migration. `enabled` keeps its scoping meaning and evolve ignores it, as today. A missing
  directory skips that provider.
- Each `SessionLine` gains `provider` and `repo`: the profile repo whose path, or any path from its
  `git worktree list --porcelain`, contains the line's cwd; otherwise `unknown`. Worktree lists are
  read once per run. Cursor lines, which carry no cwd, get the repo from their project directory where
  it maps to one, otherwise `unknown`.
- Refs become `transcript:<provider>:<session8>#<n>`. Old `transcript:<session8>#<n>` refs keep
  resolving as Claude refs. `excerptFor` resolves across all providers.

### 6.2 Consumers

- **telemetry:** hook fires from all sessions.
- **correct:** human turns from all sessions; the labeler, cap and scrubbing are unchanged.
- **reflect:** `sindri evolve reflect [--repo <name>] --pr <n>`. `--repo` defaults to the toolkit repo.
  It uses that repo's `gh` remote and `evolve.prAuthors`, and builds the branch transcript from lines
  with that `repo` and branch. Toolkit PRs keep the key `reflect:pr-<n>`; other repos use
  `reflect:<repo>:pr-<n>`, so no ledger migration is needed.
- **Public provenance:** a task sourced from another repo's PR renders its source as `reflect on another
  repo's PR`, and its `pr:` evidence refs are withheld, so the plan file never names a private repo or its
  PR numbers.
- **weekly:** lists merged PRs for every profile repo and reflects on the unreflected ones.

## 7. Profile and repo config

| Key | Type | Default | Notes |
|---|---|---|---|
| `privacy.denyTerms` | string[] | `[]` | Unchanged. |
| `privacy.denyPatterns` | string[] | `[]` | Must compile; publish-only. |
| `repos/<name>.yaml` `private` | boolean | `false` | Feeds §5.4. |
| `sources.transcripts.{codex,cursor}.dir` | string | provider default | B2; `sources.transcripts.dir` stays Claude. |

Any change needs `sindri profile approve`, as today. `docs/sindri/profile.md` is regenerated with
`npm run gen`; `docs/sindri/evolve.md` gains a threat-model section (§2, §4, §5).

## 8. Doctor

- warn: a profile repo other than the toolkit repo with no explicit `private:` value.
- warn: a `private: true` repo with no symbols index (publish will refuse).
- warn: `/usr/share/dict/words` missing.

## 9. Errors

| Code | When | Fix hint |
|---|---|---|
| `SND-EVOLVE-015` | `denyTerms` empty (unchanged) | add names, approve |
| `SND-EVOLVE-017` | a `private: true` repo has no built symbols index | `sindri index build --repo <name>` |
| `SND-EVOLVE-018` | the English word list is missing | install a word list at `/usr/share/dict/words` |

Generalization failures are held reasons, not errors (§5.1).

## 10. Testing

Vitest with in-memory SQLite and a fake `ModelRunner`, TDD with RED recorded per task, per
`.agents/rules/testing.md`.

- **Gates:** per layer, a hit, a near-miss and normalization evasions (NBSP, zero-width, fullwidth,
  decomposed accents, line breaks). Held reasons never contain the matched text.
- **Derived list:** English and toolkit subtraction, the 5-character floor, path segments, refusal on a
  missing index or word list.
- **Verbatim:** an 8-word copy holds, a 7-word overlap passes, a run present at toolkit HEAD passes.
- **Generalization:** rewrite ok/fail/changed-refs, judge leaks/clean/fail, retry on the next stage,
  publish refuses a proposal with no `proposal_public` row.
- **Ledger:** v4 → v5 with backup; reflect source-key rewrite.
- **B2:** fixture transcripts for Claude (main, subagent, linked worktree), Codex (including an
  imported thread) and Cursor; repo attribution through worktrees; cross-provider dedupe; legacy refs;
  `reflect --repo`; weekly across repos.
- **End-to-end leak test:** a synthetic workplace (made-up company name, identifiers, ticket keys and
  domain story) in a private fixture repo and its transcripts, run through reflect, stage and publish;
  assert nothing from it reaches the plan file.

## 11. Delivery

1. **PR B1**: §5, the `denyPatterns` and `private` schema keys, doctor checks for §5, docs. Works on
   today's toolkit-only input.
2. **PR B2**: §6, the additive `sources.transcripts` provider dirs, docs.
3. **Task 14** (Plan 5) switch-on, using the private profile: names, patterns and `private: true`.

Each PR: a fresh branch off `origin/main`, one implementer at a time, then review and fix rounds until
clean, then `/review`. Pushed to the fork; the owner merges.

## 12. Out of scope

- Publishing to a private location instead of the toolkit repo.
- Source exclusion or turn taint (D6).
- An allow-list for derived-identifier false positives (revisit after the first `--dry-run` numbers).
- The Plan 5 accepted limitations listed in #81, except "linked-worktree sessions aren't read", which
  B2 fixes.
