import { makeScrubber } from "../scrub/scrub.js";
import { stripInvisible } from "./invisible.js";
import { reduceEvidence, type Proposal, type Tier } from "./proposals.js";

const scrubber = makeScrubber();
const MAX_LINE = 400;
const MAX_LINES = 30;
const LINE_BREAKS = /\r\n|[\r\n\u0085\u2028\u2029]/;

// Model-written text becomes inert Markdown: no HTML, images, links, @-mentions, issue references,
// code spans or control characters, so it can't forge a heading, a ticked step or a ping in a plan file.
// Controls and invisible characters go first: scrubbing "AKIA<control>…" before the strip would let it rebuild a live key.
const KEEP = new Set(["\t", "\n", "\r"]);

export function inert(s: string): string {
  return scrubber.scrub(stripInvisible(s, KEEP)).text
    .replace(/\\/g, "\\\\").replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")
    .replace(/[[\]!`|*]/g, (c) => `\\${c}`)
    .replace(/@/g, "(at)").replace(/#(?=\d)/g, "(num)")
    .replace(/https?:\/\//gi, "hxxp://")
    .replace(/(?<![a-z0-9])www\./gi, "www(dot)");
}

export const oneLine = (s: string, max = 200): string => inert(s.replace(/[\s\u0085]+/g, " ").trim().slice(0, max));

// A quoted line that starts with a heading, setext, fence or list marker would render as one inside the quote.
const BLOCK_START = /^(\s*)([#=\-~+])/;

export const quote = (s: string): string[] => s.split(LINE_BREAKS).slice(0, MAX_LINES).map((l) => `> ${inert(l.slice(0, MAX_LINE)).replace(BLOCK_START, "$1\\$2")}`.trimEnd());

const CHECK_KINDS: readonly Proposal["kind"][] = ["prompt-edit", "docs", "rule"];

export function renderTask(n: number, id: string, p: Proposal, tier: Tier, why: string, source: string): string {
  const files = p.change.type === "describe" ? p.change.files : [];
  const ev = reduceEvidence(p.evidence);
  const stepOne = CHECK_KINDS.includes(p.kind)
    ? "Write the check that would have caught the evidence case (a test, lint rule or assertion that guards this artifact)"
    : "Write a failing test that shows the problem, using a past case from the evidence";
  return [
    `### Task ${n}: ${oneLine(p.title, 120)}`,
    "",
    `Proposal \`${id}\` (${tier}: ${oneLine(why, 120)}), from ${oneLine(source, 60)}.`,
    ...(tier === "approval" ? ["", "**Protected: the owner approves the change before it merges (spec §7.7).**"] : []),
    "",
    "**Files:**",
    ...files.map((f) => `- Modify: \`${f}\``),
    "",
    "> **Why**",
    ...quote(p.rationale),
    ">",
    "> **Change**",
    ...quote(p.change.type === "describe" ? p.change.description : "Replace the prompt text; see the proposal in sindri evolve show."),
    "",
    `**Evidence:** ${ev.refs.length > 0 ? ev.refs.join(", ") : "none recorded"}${ev.withheld > 0 ? ` (${ev.withheld} reference(s) withheld)` : ""}`,
    "",
    `- [ ] **Step 1: ${stepOne}**`,
    "- [ ] **Step 2: Make the change described above**",
    `- [ ] **Step 3: Run \`sindri evolve check ${p.artifact}\`, the AGENTS.md merge gate for the touched package, and \`sindri evolve tier ${id}\`**`,
    `- [ ] **Step 4: Commit with a message that mentions Proposal \`${id}\`, then tick these steps**`,
    "",
  ].join("\n");
}
