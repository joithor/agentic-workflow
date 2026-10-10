import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { indexPath, openIndex, type IndexDb } from "../src/index/db.js";
import { buildDerivedSet, identifierProblem, identifierTokens, loadDerivedSet, privateRepos, unsetPrivateRepos, vocabularyOf, wordsFile } from "../src/evolve/privacy-derived.js";
import { SindriError } from "../src/errors.js";
import { seedIndex } from "./index-fixtures.js";
import { makeDeps, tempDir } from "./helpers.js";

const WORDS = new Set(["schedule", "nurse", "shift", "patient", "query", "utils"]);

describe("wordsFile", () => {
  it("defaults to the system dictionary and honours SINDRI_WORDS", () => {
    expect(wordsFile(makeDeps())).toBe("/usr/share/dict/words");
    expect(wordsFile(makeDeps({ env: { SINDRI_WORDS: "/w" } }))).toBe("/w");
  });
});

describe("identifierTokens", () => {
  it("returns nothing for text without identifiers", () => {
    expect(identifierTokens("12 -- 34")).toEqual([]);
  });

  it("splits on non-identifier characters, lowercases and drops tokens under 5 characters", () => {
    expect(identifierTokens("call quarbleScheduler() in src/glx-rota/utils.ts, ok")).toEqual(["quarblescheduler", "utils"]);
    expect(identifierTokens("ｑuarbleScheduler")).toEqual(["quarblescheduler"]);
  });
});

describe("buildDerivedSet", () => {
  it("keeps private identifiers that are neither English words nor toolkit vocabulary", () => {
    const priv = seedIndex(openIndex(path.join(tempDir("ix-"), "p.db")), { files: ["src/quarble-rota/schedule.ts"], symbols: ["quarbleScheduler", "useQuery", "renderTask"] });
    const toolkit = seedIndex(openIndex(path.join(tempDir("ix-"), "t.db")), { files: ["sindri/src/evolve/render.ts"], symbols: ["renderTask"] });
    const set = buildDerivedSet({ privateVocab: [vocabularyOf(priv)], toolkit: vocabularyOf(toolkit), words: WORDS });
    expect(set.has("quarblescheduler")).toBe(true);
    expect(set.has("quarble")).toBe(true); // from the path segment quarble-rota
    expect(set.has("usequery")).toBe(true); // a public library name: a false positive that holds, never leaks
    expect(set.has("rendertask")).toBe(false); // toolkit vocabulary
    expect(set.has("schedule")).toBe(false); // English
    expect(vocabularyOf(toolkit).has("render")).toBe(true);
  });
});

describe("buildDerivedSet without a toolkit index", () => {
  it("keeps every non-English private identifier", () => {
    const priv = seedIndex(openIndex(path.join(tempDir("ix-"), "p.db")), { files: ["a.ts"], symbols: ["renderTask"] });
    expect(buildDerivedSet({ privateVocab: [vocabularyOf(priv)], toolkit: new Set(), words: WORDS }).has("rendertask")).toBe(true);
  });
});

describe("identifierProblem", () => {
  it("passes everything when the derived set is empty", () => {
    expect(identifierProblem("anything goes here", new Set())).toBeNull();
  });

  it("holds text naming a derived identifier, without echoing it", () => {
    const set = new Set(["quarblescheduler"]);
    expect(identifierProblem("Tighten QuarbleScheduler retries", set)).toBe("contains a private-repo identifier");
    expect(identifierProblem("Tighten the scheduler retries", set)).toBeNull();
  });
});

describe("loadDerivedSet", () => {
  function loaded(deps: ReturnType<typeof makeDeps>, repos: Record<string, { private?: boolean }>) {
    return { profile: { tracker: { repo: "toolkit" } }, repos: Object.fromEntries(Object.entries(repos).map(([k, v]) => [k, { name: k, ...v }])) } as unknown as Parameters<typeof loadDerivedSet>[1];
  }

  it("returns an empty set when no repo is private, without needing a word list", () => {
    const deps = makeDeps({ env: { SINDRI_WORDS: "/nonexistent" } });
    expect(loadDerivedSet(deps, loaded(deps, { toolkit: {}, other: { private: false } })).size).toBe(0);
  });

  it("refuses with SND-EVOLVE-018 when a repo is private and the word list is missing", () => {
    const deps = makeDeps({ env: { SINDRI_WORDS: "/nonexistent" } });
    expect(() => loadDerivedSet(deps, loaded(deps, { toolkit: {}, globex: { private: true } }))).toThrow(expect.objectContaining({ code: "SND-EVOLVE-018" }) as SindriError);
  });

  it("refuses with SND-EVOLVE-017 when a private repo has no index or no ok structure layer", () => {
    const words = path.join(tempDir("w-"), "words");
    fs.writeFileSync(words, "schedule\nnurse\n");
    const deps = makeDeps({ env: { SINDRI_WORDS: words } });
    const l = loaded(deps, { toolkit: {}, globex: { private: true } });
    expect(() => loadDerivedSet(deps, l)).toThrow(expect.objectContaining({ code: "SND-EVOLVE-017" }) as SindriError);
    seedIndex(openIndex(indexPath(deps, "globex")), { files: ["a.ts"], symbols: ["quarbleScheduler"], structure: "pending" }).close();
    expect(() => loadDerivedSet(deps, l)).toThrow(expect.objectContaining({ code: "SND-EVOLVE-017" }) as SindriError);
  });

  it("reads an index whose structure layer is ok while embeddings are still pending (Review Focus 3)", () => {
    const words = path.join(tempDir("w-"), "words");
    fs.writeFileSync(words, "Schedule\nnurse\n");
    const deps = makeDeps({ env: { SINDRI_WORDS: words } });
    seedIndex(openIndex(indexPath(deps, "toolkit")), { files: ["t.ts"], symbols: ["renderTask"] }).close();
    const db = seedIndex(openIndex(indexPath(deps, "globex")), { files: ["a.ts"], symbols: ["quarbleScheduler", "schedule"] });
    db.prepare("INSERT INTO layers (layer, stamp, status, detail, built_at) VALUES ('embeddings', 's', 'pending', 'embedded 1 of 9', 'x')").run();
    db.close();
    const set = loadDerivedSet(deps, loaded(deps, { toolkit: {}, globex: { private: true } }));
    expect([...set]).toEqual(["quarblescheduler"]);
  });

  it("never treats the tracker repo as private, subtracting its vocabulary instead", () => {
    const words = path.join(tempDir("w-"), "words");
    fs.writeFileSync(words, "nurse\n");
    const deps = makeDeps({ env: { SINDRI_WORDS: words } });
    seedIndex(openIndex(indexPath(deps, "toolkit")), { files: ["t.ts"], symbols: ["renderTask"] }).close();
    seedIndex(openIndex(indexPath(deps, "globex")), { files: ["a.ts"], symbols: ["quarbleScheduler", "renderTask"] }).close();
    const set = loadDerivedSet(deps, loaded(deps, { toolkit: { private: true }, globex: { private: true } }));
    expect([...set]).toEqual(["quarblescheduler"]);
  });

  it("returns an empty set, without a word list, when only the tracker repo is marked private", () => {
    const deps = makeDeps({ env: { SINDRI_WORDS: "/nonexistent" } });
    expect(loadDerivedSet(deps, loaded(deps, { toolkit: { private: true } })).size).toBe(0);
  });

  it("refuses with SND-EVOLVE-017, with the rm fix, when the private index is missing any table or isn't a database", () => {
    const words = path.join(tempDir("w-"), "words");
    fs.writeFileSync(words, "nurse\n");
    const deps = makeDeps({ env: { SINDRI_WORDS: words } });
    const file = indexPath(deps, "globex");
    const l = loaded(deps, { toolkit: {}, globex: { private: true } });
    for (const table of ["symbols", "files", "meta"]) {
      fs.rmSync(file, { force: true });
      const db = seedIndex(openIndex(file), { files: ["a.ts"], symbols: ["quarbleScheduler"] });
      db.exec(`DROP TABLE ${table}`);
      db.close();
      expect(() => loadDerivedSet(deps, l), table).toThrow(expect.objectContaining({
        code: "SND-EVOLVE-017", message: `repo globex is private but its code index is unusable (no usable ${table} table)`,
        fix: `rm ${file}, then sindri index build --repo globex, then rerun sindri evolve publish`,
      }) as SindriError);
    }
    fs.writeFileSync(file, "this file is not a SQLite database, only text that fills its first page".repeat(4));
    expect(() => loadDerivedSet(deps, l)).toThrow(expect.objectContaining({ code: "SND-EVOLVE-017", message: "repo globex is private but its code index is unusable (not a database)" }) as SindriError);
  });

  it("subtracts nothing when the toolkit index is unusable: more identifiers are held, never fewer", () => {
    const words = path.join(tempDir("w-"), "words");
    fs.writeFileSync(words, "nurse\n");
    const deps = makeDeps({ env: { SINDRI_WORDS: words } });
    fs.mkdirSync(path.dirname(indexPath(deps, "toolkit")), { recursive: true });
    fs.writeFileSync(indexPath(deps, "toolkit"), "not a database either, just a page of plain text".repeat(6));
    seedIndex(openIndex(indexPath(deps, "globex")), { files: ["a.ts"], symbols: ["renderTask"] }).close();
    expect([...loadDerivedSet(deps, loaded(deps, { toolkit: {}, globex: { private: true } }))]).toEqual(["rendertask"]);
  });

  it("names private and unset repos the same way for doctor and publish, never the tracker repo", () => {
    const deps = makeDeps();
    const l = loaded(deps, { toolkit: { private: true }, b: { private: true }, a: { private: true }, c: {}, d: { private: false } });
    expect(privateRepos(l)).toEqual(["a", "b"]);
    expect(unsetPrivateRepos(l)).toEqual(["c"]);
  });

  it("refuses with SND-EVOLVE-017 when the private index has no usable layers table", () => {
    const words = path.join(tempDir("w-"), "words");
    fs.writeFileSync(words, "nurse\n");
    const deps = makeDeps({ env: { SINDRI_WORDS: words } });
    const db = seedIndex(openIndex(indexPath(deps, "globex")), { files: ["a.ts"], symbols: ["quarbleScheduler"] });
    db.exec("DROP TABLE layers");
    db.close();
    expect(() => loadDerivedSet(deps, loaded(deps, { toolkit: {}, globex: { private: true } }))).toThrow(expect.objectContaining({ code: "SND-EVOLVE-017" }) as SindriError);
  });
});
