---
name: system-design-video
description: Turn a system design (data model, how a value flows through functions, rollout states, PR order) into a short whiteboard explainer video drawn live, stroke by stroke, on a dark board. One cut per audience (engineering, product), captions, a narration script, and 1080p + 720p MP4s. Use when asked for a system design video, a design walkthrough film, or a whiteboard explainer of a design doc or TDD.
argument-hint: "[design doc, packet dir, or topic] [--persona engineering|product]"
allowed-tools: Bash(ls *), Bash(mkdir *), Bash(bun *), Bash(ffmpeg *), Bash(ffprobe *), Bash(git *), Bash(SHARED_DIR=*), Bash(source *), Read, Write, Edit, Glob, Grep, Skill, AskUserQuestion
---

<!-- preamble -->
**Before anything else:** read `$HOME/.agentic-workflow/toolkit/skills/_preamble.md` and follow it (skill index, provider capability map, bootstrap check, session context). Run its **Session Close** section when this skill finishes.

<!-- design-preamble -->
**Design context:** read `$HOME/.agentic-workflow/toolkit/skills/_design-preamble.md` and follow it before continuing (the ink colors come from the project's `design-tokens.json`).

---

# System design video

Explain a design the way an engineer whiteboards it in an interview: a dark board, the explanation drawn live, stroke by stroke. Boxes, arrows, tables and numbers appear as they are drawn; earlier drawings stay on the board; the camera pans to new space. Nothing fades in as a finished diagram. Each beat is one spoken sentence and the pen draws what that sentence says.

Same four gates as a product demo film (scope, brief, storyboard, produce), but the footage is a drawing, not a screen recording. Works for any project; everything project-specific comes from the project (see Inputs).

Read before starting: `references/style.md` (look, motion, legibility), `references/storyboard-format.md` (the storyboard file), `references/personas.md`. Read on trouble: `references/gotchas.md`. A runnable worked example, with generic content, is in `examples/base-limit/`.

`$SKILL` = this skill's directory (`$HOME/.agentic-workflow/toolkit/skills/system-design-video`). Output: `$HOME/.agentic-workflow/$REPO_SLUG/videos/<design>/<persona>/` (`$D` below): `brief.md`, `storyboard.json`, `narration.md`, `take/`, `deliverables/`.

## Inputs (all from the project, nothing baked in)

| Need | Source |
| --- | --- |
| The design: model, flows, decisions, a worked example with real numbers | the doc or packet the user names; the sibling system-design packet if one exists; otherwise the plan, spec and TDD files in the repo |
| Code and surfaces named on the board | Sindri context: `mcp: sindri/context_lenses`, then `mcp: sindri/context_query` for the symbols and files a beat names; the repo's `AGENTS.md` for the vocabulary. If Sindri is not available, read the code directly and say so in the brief |
| Look: board color, chalk, the meaning-colors | `design-tokens.json` (W3C DTCG) and `.impeccable.md`, from **Invoke skill `design-language`** if the project has neither. Ask the user which tokens are the board, the neutral ink and each concept's color |
| Ticket context | whatever tracker the project uses, through its own tool; read-only |

Every number on the board comes from a source line you can cite. Trace the worked example's arithmetic against the source before drawing it.

## Step 0: Check the machine

```bash
$SKILL/scripts/doctor.sh
```
It checks ffmpeg, bun, and playwright-core with its Chromium. It prints the install command for anything missing; it never installs. If the browser stack is missing, **Ask the user** whether to run the printed command.

## Step 1: Scope (gate)

One video per audience, each answering one person's question (`references/personas.md`). Defaults: an engineering cut (model, write and read paths, rollout, PR order) and a product cut (one thing to set, a rule per workflow, what changes for the people using it). Merge cuts that would draw the same board; drop an audience with nothing to see. Write the film table (audience, question, story from start to payoff, the shared worked example). **Ask the user** to approve the list before continuing.

## Step 2: Brief (gate)

`$D/brief.md`: the promise in one sentence, facts with source refs, the worked example traced step by step, 3 to 8 beats, and `$D/narration.md` (one spoken sentence per beat, in the audience's words). A ratio reads "X requests to Y workers", never with an arrow. **Ask the user** to approve.

## Step 3: Storyboard (gate)

`$D/storyboard.json` per `references/storyboard-format.md`: one scene per diagram or concept, one beat per sentence, items in the order the pen draws them, one new idea per beat, 30 to 60 seconds in total. `inks` bind each ink name to a token path. **Ask the user** to review.

## Step 4: Produce

```bash
TOKENS=design-tokens.json        # the project's tokens
bun $SKILL/whiteboard/render.ts $D/storyboard.json $D/take --tokens $TOKENS --still 5,12,20   # stills in seconds
bun $SKILL/whiteboard/render.ts $D/storyboard.json $D/take --tokens $TOKENS                   # footage.mp4 + layout.json
$SKILL/scripts/review.sh $D/take                                                              # one still per beat, in sheets
```

Read every `take/check/sheet-*.jpg` against the storyboard and run the checklist in `references/style.md`. Fix overlaps and clipping in the storyboard, re-render, read the sheets again. Then encode:

```bash
$SKILL/scripts/deliver.sh $D/take/footage.mp4 $D/deliverables/v1 <name> <board-hex>   # board hex is in take/resolved-inks.json
```

This writes the 1080p and 720p MP4s (BT.709 tagged, no audio) and prints ffprobe and a corner-pixel check against the board color. Extract 3 to 6 frames from the 1080p during motion and one from the 720p cut (`ffmpeg -ss <t> -i <mp4> -frames:v 1`), and read them.

`deliver.sh` ends with `playback-check.sh` (full decode, level at most 4.1, no B-frames). Then tell the user to play each MP4 once, start to end, in the system player before delivery; stills are not playback.

Sound and voiceover are not built in. The captions carry the narration. Offer the user the options in `references/style.md` (audio) and add none unasked.

## Done when

- Every beat's still was read against the storyboard, and the style checklist holds.
- The deliverables exist at 1920x1080 and 1280x720, h264, BT.709 tags, with the corner pixel check passing.
- The report names which frames were read; say "reviewed stills", never "watched", unless the video was played in a real player. `playback-check.sh` passes on both files.
- Deliverable paths and sizes are listed.

Posting to chat or a tracker is a separate step: **Ask the user** first, and use the project's own tool for it.

## Next steps

- `/design-language`: when the project has no tokens, define them first.
- `/officeHours` or `/archReview`: when drawing the design exposed a gap in the design itself.
