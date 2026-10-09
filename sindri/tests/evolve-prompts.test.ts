import Database from "better-sqlite3";
import { createHash } from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { evolveOverlayCheck, effectivePrompts, inspectOverlay, loadPrompt, overlayDir, overlayFile, overlayProblems } from "../src/evolve/overlay.js";
import { defaultPrompt, hasSafetyClause, PROMPT_IDS, PROMPTS, SOURCES_CLAUSE, TRANSCRIPTS_CLAUSE } from "../src/evolve/prompts.js";
import { stateDir } from "../src/deps.js";
import { ledgerPath, openLedger } from "../src/ledger/db.js";
import { makeDeps, tempDir } from "./helpers.js";

const sha = (s: string): string => createHash("sha256").update(s).digest("hex");

function adopt(d: ReturnType<typeof makeDeps>, id: string, text: string): void {
  const db = openLedger(ledgerPath(stateDir(d)));
  db.prepare("INSERT INTO adoptions (prompt_id, proposal_id, sha256, adopted_at, adopted_by, epoch) VALUES (?, 'p', ?, 't', 'me', 1)").run(id, sha(text));
  db.close();
}

function writeOverlay(d: ReturnType<typeof makeDeps>, id: "scope.draft", text: string, mode = 0o600): string {
  fs.mkdirSync(overlayDir(d), { recursive: true });
  const f = overlayFile(d, id);
  fs.writeFileSync(f, text, { mode });
  fs.chmodSync(f, mode);
  return f;
}

describe("the built-in prompts", () => {
  it("lists every prompt, each with its injection-resistance clause", () => {
    expect(PROMPTS.map((p) => p.id)).toEqual([...PROMPT_IDS]);
    expect(PROMPT_IDS).toEqual(["scope.draft", "scope.challenger", "reflect.judgment", "reflect.tooling", "reflect.divergent", "reflect.synthesize", "correct"]);
    for (const p of PROMPTS) {
      expect(hasSafetyClause(p.id, p.text), p.id).toBe(true);
      expect(defaultPrompt(p.id)).toBe(p.text);
    }
    expect(defaultPrompt("scope.draft")).toContain("You scope a software project before work starts.");
    expect(defaultPrompt("scope.draft")).toContain(SOURCES_CLAUSE);
    expect(defaultPrompt("scope.challenger")).toContain("You challenge a scope map.");
    expect(defaultPrompt("reflect.synthesize")).toContain(TRANSCRIPTS_CLAUSE);
    expect(hasSafetyClause("scope.draft", "no clause here")).toBe(false);
    expect(hasSafetyClause("correct", SOURCES_CLAUSE)).toBe(false);
  });
});

describe("the overlay is bound to an adoption (Review Focus 7)", () => {
  it("serves the built-in prompt when there's no overlay, and the overlay only when its hash matches the latest adoption", () => {
    const d = makeDeps();
    const def = defaultPrompt("scope.draft");
    expect(loadPrompt(d, "scope.draft")).toBe(def);
    expect(inspectOverlay(d, "scope.draft")).toEqual({ state: "none", text: null });
    const text = `${SOURCES_CLAUSE}\nAdopted draft prompt.`;
    writeOverlay(d, "scope.draft", text);
    expect(inspectOverlay(d, "scope.draft").state).toBe("unadopted"); // no ledger yet
    expect(loadPrompt(d, "scope.draft")).toBe(def);
    adopt(d, "scope.draft", "some other text");
    expect(loadPrompt(d, "scope.draft")).toBe(def); // hash mismatch
    adopt(d, "scope.draft", text);
    expect(loadPrompt(d, "scope.draft")).toBe(text);
    expect(inspectOverlay(d, "scope.draft")).toEqual({ state: "active", text });
    adopt(d, "scope.draft", "a newer adoption");
    expect(loadPrompt(d, "scope.draft")).toBe(def); // only the latest row counts
    expect(effectivePrompts(d).find((p) => p.id === "scope.draft")?.text).toBe(def);
    expect(effectivePrompts(d)).toHaveLength(7);
  });

  it("treats an overlay as unadopted when the ledger has adoptions, but none for that prompt", () => {
    const d = makeDeps();
    writeOverlay(d, "scope.draft", `${SOURCES_CLAUSE}\nonly another prompt was adopted`);
    adopt(d, "scope.challenger", "whatever");
    expect(inspectOverlay(d, "scope.draft").state).toBe("unadopted");
  });

  it("ignores an overlay that is unsafe, oversized, missing the clause, a symlink or in a symlinked dir", () => {
    const d = makeDeps();
    const good = `${SOURCES_CLAUSE}\nok`;
    adopt(d, "scope.draft", good);
    const f = writeOverlay(d, "scope.draft", good);
    expect(inspectOverlay(d, "scope.draft").state).toBe("active");
    fs.chmodSync(f, 0o666);
    expect(inspectOverlay(d, "scope.draft").state).toBe("unsafe");
    fs.chmodSync(f, 0o600);
    const big = `${SOURCES_CLAUSE}\n${"x".repeat(70_000)}`;
    adopt(d, "scope.draft", big);
    fs.writeFileSync(f, big);
    expect(inspectOverlay(d, "scope.draft").state).toBe("unsafe");
    const noClause = "Write a scope map.";
    adopt(d, "scope.draft", noClause);
    fs.writeFileSync(f, noClause);
    expect(inspectOverlay(d, "scope.draft")).toEqual({ state: "no-clause", text: null });
    fs.rmSync(f);
    fs.symlinkSync(path.join(tempDir(), "elsewhere.txt"), f);
    expect(inspectOverlay(d, "scope.draft").state).toBe("unsafe");
    const d2 = makeDeps();
    const real = tempDir();
    fs.mkdirSync(path.dirname(overlayDir(d2)), { recursive: true });
    fs.symlinkSync(real, overlayDir(d2));
    fs.writeFileSync(path.join(real, "scope.draft.txt"), good);
    expect(inspectOverlay(d2, "scope.draft").state).toBe("unsafe");
  });

  it("reports ignored overlays to doctor", () => {
    const d = makeDeps();
    expect(overlayProblems(d)).toEqual([]);
    expect(evolveOverlayCheck(d)).toEqual({ name: "evolve-overlay", status: "ok", detail: "no ignored prompt overlays" });
    writeOverlay(d, "scope.draft", `${SOURCES_CLAUSE}\nnever adopted`);
    expect(overlayProblems(d)).toEqual(["scope.draft: ignored (its hash doesn't match the latest adoption)"]);
    const c = evolveOverlayCheck(d);
    expect(c.status).toBe("warn");
    expect(c.detail).toContain("scope.draft: ignored");
    expect(c.fix).toBe("sindri evolve adopt <proposal-id> to adopt it properly, or delete the file in the overlay dir");
    const d2 = makeDeps();
    writeOverlay(d2, "scope.draft", "no clause", 0o666);
    expect(overlayProblems(d2)).toEqual(["scope.draft: ignored (the file is unsafe: not a plain private file in a real directory)"]);
    const d3 = makeDeps();
    writeOverlay(d3, "scope.draft", "no clause");
    expect(overlayProblems(d3)).toEqual(["scope.draft: ignored (it is missing the safety clause)"]);
  });
});

describe("the overlay check never touches the ledger (doctor's contract)", () => {
  const ledgerFile = (d: ReturnType<typeof makeDeps>): string => ledgerPath(stateDir(d));
  const strictOverlay = (d: ReturnType<typeof makeDeps>): void => {
    writeOverlay(d, "scope.draft", `${SOURCES_CLAUSE}\nstrict`);
  };

  it("doesn't migrate, back up or chmod an old ledger, and reads the overlay as unadopted", () => {
    const d = makeDeps();
    fs.mkdirSync(path.dirname(ledgerFile(d)), { recursive: true });
    const old = new Database(ledgerFile(d));
    old.pragma("user_version = 1");
    old.close();
    fs.chmodSync(ledgerFile(d), 0o644);
    strictOverlay(d);
    const before = fs.readdirSync(path.dirname(ledgerFile(d))).sort();
    expect(inspectOverlay(d, "scope.draft").state).toBe("unadopted");
    expect(evolveOverlayCheck(d).status).toBe("warn");
    expect(fs.readdirSync(path.dirname(ledgerFile(d))).sort()).toEqual(before);
    expect(fs.statSync(ledgerFile(d)).mode & 0o777).toBe(0o644);
    const check = new Database(ledgerFile(d), { readonly: true });
    expect(check.pragma("user_version", { simple: true })).toBe(1);
    check.close();
  });

  it("doesn't crash on a ledger newer than this build", () => {
    const d = makeDeps();
    fs.mkdirSync(path.dirname(ledgerFile(d)), { recursive: true });
    const newer = new Database(ledgerFile(d));
    newer.pragma("user_version = 999");
    newer.close();
    strictOverlay(d);
    expect(() => evolveOverlayCheck(d)).not.toThrow();
    expect(inspectOverlay(d, "scope.draft").state).toBe("unadopted");
    expect(loadPrompt(d, "scope.draft")).toBe(defaultPrompt("scope.draft"));
  });
});

describe("an unreadable overlay directory and stray files", () => {
  it("reports an overlay directory it can't read, falls back to the built-in prompts, and never throws", () => {
    const d = makeDeps();
    writeOverlay(d, "scope.draft", `${SOURCES_CLAUSE}\nx`);
    const parent = path.dirname(overlayDir(d));
    fs.chmodSync(parent, 0o000);
    try {
      expect(inspectOverlay(d, "scope.draft").state).toBe("unreadable");
      expect(loadPrompt(d, "scope.draft")).toBe(defaultPrompt("scope.draft"));
      expect(effectivePrompts(d)).toHaveLength(7);
      expect(overlayProblems(d)).toEqual(["overlay: ignored (the overlay directory can't be read)"]);
      expect(evolveOverlayCheck(d).status).toBe("warn");
    } finally {
      fs.chmodSync(parent, 0o700);
    }
  });

  it("reports files that aren't a known prompt id, and ignores them", () => {
    const d = makeDeps();
    writeOverlay(d, "scope.draft", `${SOURCES_CLAUSE}\nx`);
    adopt(d, "scope.draft", `${SOURCES_CLAUSE}\nx`);
    fs.writeFileSync(path.join(overlayDir(d), "scope.drafts.txt"), "misnamed");
    fs.writeFileSync(path.join(overlayDir(d), "notes.md"), "stray");
    expect(overlayProblems(d)).toEqual(["notes.md: ignored (not a known prompt file)", "scope.drafts.txt: ignored (not a known prompt file)"]);
    expect(loadPrompt(d, "scope.draft")).toContain("x");
    expect(evolveOverlayCheck(d).status).toBe("warn");
  });
});
