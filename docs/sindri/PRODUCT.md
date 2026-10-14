# Product

## Register

product

## Users

One engineer, the owner, at a desk with two or three monitors, Warp and an agent CLI open beside this app. They run
a software factory on their own repositories: agents scope, design, build, test and ship work through recipes, and
the engineer watches, decides and steps in. The app is glanced at between sessions and read closely when something
needs them. Later: the same engineer on a laptop, then a small team sharing one Sindri.

The job on any screen is one of five questions: what needs me, where is each piece of work in its recipe, how do the
recipes perform, what is the product and where does it live in code, what is failing and what does that break.

## Product Purpose

Sindri's desktop app is the window onto the control plane: the queue, the running workflows and their state, the
decisions waiting, the agents in flight, the context graph, the system's health. It is not a chat and it never
replaces the terminal; its headline action is one click into a live agent session. Success is the five-second test:
a fresh pair of eyes on the home screen answers what needs them, what is running, what is queued and whether Sindri
is healthy, and every answer leads to the one action that resolves it.

## Brand Personality

Dense, calm, exact. Everything that matters is on one screen; nothing shouts; every number, id and command is the
real one. Words over icons, mono for anything an engineer would copy, one attention colour for what needs a person
and no other alarm. The voice is a colleague who has read the logs: short sentences, the next step named, no
cheerleading.

## Anti-references

- The SaaS dashboard cliché: hero metrics, identical card grids, gradient accents, a chat panel in the corner.
- The alarmed ops console: red everywhere, blinking, colour as the only signal, sirens for routine events.
- A generic Linear clone: Linear's chrome without the control plane's content; sidebars, labels and priorities that
  mean nothing here. Linear is the visual language, not the information architecture.
- Terminal cosplay: green-on-black mono everything, ASCII boxes shipped as UI, no hierarchy.

## Design Principles

1. **Answer, then act.** Every row states its situation in words and carries the one action that resolves it; the
   action's class (desk, protected, destructive) is visible before the click.
2. **Nothing invented.** Ids, commands, hashes, counts and sentences come from the engine, the ledger and the recipe.
   The app renders; it never decides, abbreviates into ambiguity or adds state of its own.
3. **One attention colour.** Orange means a person is needed, red means an error, green means ok, and each always
   comes with a word. Everything else is ink on the surface ladder.
4. **Dense by default, legible always.** Rows at 36 px, mono where it will be copied, hierarchy by weight and
   surface, never by decoration. Density is a feature for this user; illegibility is not.
5. **One frame for every recipe.** Workflows, items, incidents and triage share the same states, facts and children
   frame; a new recipe needs no new screen.

## Accessibility & Inclusion

WCAG 2.2 AA on every text and control pair against the Linear palette (the UI spec's §11.1 table). Never colour
alone: every state glyph has a word. Reduced motion is honoured (the pulse becomes a static outline; motion durations
go to zero). Full keyboard model with chords and a palette; focus order follows reading order; live regions announce
what cleared. Text scale 0.9 to 1.3 through one resource. Desktop first at 1280 × 800; a 900 × 600 minimum scrolls.
