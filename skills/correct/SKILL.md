---
name: correct
description: Find a mistake class that happened twice and propose one fix at the highest level that works: architecture, types, lint, test, docs last. Port of pstack's correct (MIT).
argument-hint: "[how far back, e.g. 7d]"
allowed-tools: Bash(git *), Bash(sindri evolve *), Read, Glob, Grep
disable-model-invocation: true
---

# Correct

A class is a mistake that happened twice. Fix it where it can never happen again.

<!-- preamble -->
**Before anything else:** read `$HOME/.agentic-workflow/toolkit/skills/_preamble.md` and follow it (skill index, provider capability map, bootstrap check, session context). Run its **Session Close** section when this skill finishes.

## Steps

1. **Find the repeats.** Look through this session and recent ones in this repo for human corrections that point at the same mistake. Count both kinds: the approach or design was wrong ("no, wrong file", "you missed", "don't use that"), and the process was wrong (it belongs in CI, in another doc or tool, or in a different order of steps). Judge by what the human meant, not by trigger words. A class needs at least two corrections from two different sessions on two different days.
2. **Name the class.** One sentence: what the agent did, and what it should have done.
3. **Pick the highest level that works,** in this order:
   1. *Architecture:* make the mistake impossible to express.
   2. *Types:* make the compiler refuse it.
   3. *Lint:* an error message that names the fix.
   4. *Test:* a failing test for the past mistake.
   5. *Docs:* last, only when nothing above can enforce it.
4. **Prove it.** Say which past correction the check would have caught. If it would not have caught one, it is the wrong level.
5. **Report.** Print the class, the level, the proposed change (files and a description, not a patch) and the evidence. Do not edit any file.
6. **Record.** End with: "`sindri evolve correct` runs this weekly over this repo's transcripts."

## Attribution

Port of the `correct` playbook from pstack (MIT, © 2026 Lauren Tan).
