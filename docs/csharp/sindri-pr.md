# Sindri PR babysitter and docs sync

Spec: `docs/superpowers/specs/2026-10-15-sindri-pr-babysitter-docs-sync-design.md`. This page holds what the C# build has so far.

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
