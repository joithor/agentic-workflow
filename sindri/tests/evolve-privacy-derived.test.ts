import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { indexPath, openIndex, type IndexDb } from "../src/index/db.js";
import { buildDerivedSet, identifierProblem, identifierTokens, loadDerivedSet, privateRepos, unsetPrivateRepos, vocabularyOf, wordsFile } from "../src/evolve/privacy-derived.js";
import { SindriError } from "../src/errors.js";
import { NAME_JOINER, termProblem } from "../src/evolve/privacy.js";
import { seedCorruptIndex, seedIndex } from "./index-fixtures.js";
import { git, gitRepo, makeDeps, tempDir } from "./helpers.js";

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
    expect(identifierTokens("-- !! --")).toEqual([]); // no name spelled with separators either
  });

  it("splits on non-identifier characters, lowercases and drops tokens under 5 characters", () => {
    expect(identifierTokens("call quarbleScheduler() in src/glx-rota/utils.ts, ok")).toEqual(["quarblescheduler", "utils", "glxrota", "utilsts"]); // plus joined forms (security-sentinel-1)
    expect(identifierTokens("ｑuarbleScheduler")).toEqual(["quarblescheduler"]);
  });

  it("is Unicode-aware: a combining mark or a non-ASCII letter inside an identifier never splits it", () => {
    expect(identifierTokens("BillingEngine\u0301Service")).toEqual(["billingengineservice"]); // decomposed accent stripped
    expect(identifierTokens("BillingEngin\u00e9Service")).toEqual(["billingengineservice"]); // precomposed accent folded
    expect(identifierTokens("Bill\u0456ngEngine")).toEqual(["bill\u0456ngengine"]); // a Cyrillic letter stays inside the token
    expect(identifierTokens("\u00c9tatMachine $scope_ref")).toEqual(["etatmachine", "$scope_ref", "scoperef"]);
    expect(identifierTokens("Quarble\u200bScheduler")).toEqual(["quarblescheduler"]); // invisibles still go
  });

  it("matches an accented candidate against a symbol stored with or without the accent (both sides normalized alike)", () => {
    const db = seedIndex(openIndex(path.join(tempDir("ix-"), "p.db")), { files: ["src/caf\u00e9-rota/x.ts"], symbols: ["BillingEngin\u00e9Service"] });
    const set = buildDerivedSet({ privateVocab: [vocabularyOf(db)], toolkit: new Set(), words: new Set() });
    expect(identifierProblem("uses BillingEngine\u0301Service now", set)).toBe("contains a private-repo identifier");
    expect(identifierProblem("uses BillingEngineService now", set)).toBe("contains a private-repo identifier");
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

describe("identifier folding across separators (security-sentinel-1)", () => {
  const words = new Set(["nova", "ledger", "order", "sync", "worker", "packages", "main", "note", "book", "notebook"]);
  const derivedFrom = (o: { files?: string[]; symbols?: string[] }) =>
    buildDerivedSet({ privateVocab: [vocabularyOf(seedIndex(openIndex(path.join(tempDir("ix-"), "p.db")), { files: o.files ?? ["src/a.ts"], symbols: o.symbols ?? [] }))], toolkit: new Set(), words });

  it("adds each symbol's and path segment's joined form, so every spelling of a kebab-case private dir holds", () => {
    const d = derivedFrom({ files: ["packages/nova-ledger/src/main.ts"] });
    for (const spelling of ["nova-ledger", "nova_ledger", "novaLedger", "NovaLedger", "nova.ledger", "touches packages/nova-ledger/src"]) {
      expect(identifierProblem(`the change ${spelling} here`, d)).toBe("contains a private-repo identifier");
    }
    expect(identifierProblem("a nova ledger, two words", d)).toBeNull(); // two separate words stay out of scope
  });

  it("joins parts across every Unicode dash, `.`, `_`, a middle dot and a hyphenation point, on both sides (run 4 security-sentinel-0)", () => {
    const d = derivedFrom({ files: ["packages/nova-ledger/src/main.ts"] });
    const joiners = ["\u2010", "\u2011", "\u2012", "\u2013", "\u2014", "\u2015", "\u2212", "\ufe63", "\uff0d", "\u2e3a", ".", "_", "\u00b7", "\u2027", "--"];
    for (const j of joiners) expect(identifierProblem(`the change nova${j}ledger here`, d), JSON.stringify(j)).toBe("contains a private-repo identifier");
    for (const j of joiners) {
      const set = derivedFrom({ files: [`packages/nova${j}ledger/src/main.ts`] }); // the set side folds the same way
      expect(identifierProblem("the change nova-ledger here", set), JSON.stringify(j)).toBe("contains a private-repo identifier");
    }
    expect(identifierProblem("see nova/ledger", d)).toBeNull(); // `/` stays a path-segment boundary
    expect(identifierProblem("a nova\u2009ledger", d)).toBeNull(); // so does a space
    for (const c of ["/", " ", ":"]) expect(NAME_JOINER.test(c), JSON.stringify(c)).toBe(false);
  });

  it("uses the same joiners as the terms layer, which also folds each of them (run 4 security-sentinel-0)", () => {
    for (const c of ["-", "\u2010", "\u2011", "\u2013", "\u2014", "\u2212", ".", "_", "\u00b7", "\u2027"]) {
      expect(NAME_JOINER.test(c), JSON.stringify(c)).toBe(true);
      expect(termProblem(`the acme${c}care desk`, ["Acme Care"]), JSON.stringify(c)).toBe("contains a private term");
      expect(identifierTokens(`quarble${c}rota`), JSON.stringify(c)).toContain("quarblerota");
    }
  });

  it("holds a private name followed or preceded by more parts: each contiguous run of 2+ parts counts on the candidate side (run 4 kieran-typescript-reviewer-0)", () => {
    const d = derivedFrom({ files: ["packages/nova-ledger/src/main.ts"] });
    for (const text of ["update nova-ledger.ts", "see nova-ledger.md", "the @acme/nova-ledger-client wrapper", "bump @acme/nova-ledger-v2", "old-nova-ledger-v2.ts", "nova‑ledger.test.ts"]) {
      expect(identifierProblem(text, d), JSON.stringify(text)).toBe("contains a private-repo identifier");
    }
    expect(identifierProblem("update supernova-ledger.ts", d)).toBeNull(); // an unrelated joined word doesn't hold
    expect(identifierProblem("update nova-ledgers.ts", d)).toBeNull();
    expect(identifierProblem("a nova-x-ledger note", d)).toBeNull(); // parts are contiguous, never skipped
  });

  it("splits candidate parts at case and letter-digit boundaries too, so a camelCase or PascalCase name around a private one holds (run 5 security-sentinel-2, kieran-typescript-reviewer-1)", () => {
    const d = derivedFrom({ files: ["services/nova-ledger/index.ts"] });
    expect(d.has("novaledger")).toBe(true);
    const held = [
      "NovaLedgerClient", "novaLedgerService", "createNovaLedger()", "useNovaLedger", "NOVALedger", "nova_ledgerClient", "@acme/novaLedger-client",
      "createNovaLedgerV2", "NovaLedger2Client", "the NovaLedgerClient wrapper", "Nova​LedgerClient", "ŃovaLedgerClient", "ＮｏｖａLedgerClient",
      "nova-ledgerClient", "my.NovaLedger.ts",
    ];
    for (const text of held) expect(identifierProblem(text, d), JSON.stringify(text)).toBe("contains a private-repo identifier");
    for (const text of ["SupernovaLedger", "SupernovaLedgerClient", "useLedgerNova", "NovaXLedger", "LedgerClient", "novaLedgers"]) {
      expect(identifierProblem(text, d), JSON.stringify(text)).toBeNull(); // an unrelated camelCase identifier doesn't hold
    }
    expect(identifierProblem("NovaLedgerClient", derivedFrom({ symbols: ["NovaLedger"] }))).toBe("contains a private-repo identifier");
    expect(identifierTokens("NovaLedgerClient")).toEqual(["novaledgerclient"]); // the set side stays whole-token
  });

  it("bounds case-split candidate runs to 8 parts plus the whole run, like separator runs", () => {
    const parts = Array.from({ length: 12 }, (_, i) => `P${String.fromCharCode(97 + i)}x`);
    const camel = parts.join("");
    const lower = (xs: string[]): string => xs.join("").toLowerCase();
    expect(identifierProblem(camel, new Set([lower(parts)]))).toBe("contains a private-repo identifier");
    expect(identifierProblem(camel, new Set([lower(parts.slice(2, 10))]))).toBe("contains a private-repo identifier");
    expect(identifierProblem(camel, new Set([lower(parts.slice(1, 10))]))).toBeNull();
    expect(identifierProblem(`see ${parts.slice(0, 6).join("")}-${parts.slice(6).join("_")}`, new Set([lower(parts.slice(4, 8))]))).toBe("contains a private-repo identifier"); // across both kinds
  });

  it("keeps the set side to whole runs and bounds the candidate side's runs to 8 parts plus the whole run", () => {
    expect(identifierTokens("glx-rota-utils")).toEqual(["utils", "glxrotautils"]); // set side: the whole run only, no glxrota
    const d = new Set(["glxrota"]);
    expect(identifierProblem("glx-rota-utils", d)).toBe("contains a private-repo identifier");
    const parts = Array.from({ length: 12 }, (_, i) => `p${String.fromCharCode(97 + i)}`);
    const long = parts.join("-");
    expect(identifierProblem(long, new Set([parts.join("")]))).toBe("contains a private-repo identifier"); // the whole run, 12 parts
    expect(identifierProblem(long, new Set([parts.slice(2, 10).join("")]))).toBe("contains a private-repo identifier"); // 8 parts
    expect(identifierProblem(long, new Set([parts.slice(1, 10).join("")]))).toBeNull(); // 9 parts, not the whole run: past the bound
  });

  it("matches snake_case against camelCase both ways", () => {
    expect(identifierProblem("rename order_sync_worker", derivedFrom({ symbols: ["OrderSyncWorker"] }))).toBe("contains a private-repo identifier");
    expect(identifierProblem("rename OrderSyncWorker", derivedFrom({ files: ["src/order_sync_worker.py"] }))).toBe("contains a private-repo identifier");
  });

  it("applies the English and toolkit filters to the joined form: a near miss that joins to a word doesn't hold", () => {
    const d = derivedFrom({ files: ["src/note-book/x.ts"] });
    expect(d.has("notebook")).toBe(false);
    expect(identifierProblem("open the notebook and note-book", d)).toBeNull();
    const tk = buildDerivedSet({ privateVocab: [new Set(identifierTokens("nova-ledger"))], toolkit: new Set(identifierTokens("NovaLedger")), words });
    expect(tk.size).toBe(0); // the toolkit's own joined vocabulary is subtracted
  });

  it("never makes a joined form from digits alone or from fewer than 5 characters", () => {
    expect(identifierTokens("1.2.3-4 and ab-cd 12-345")).toEqual([]);
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
    expect(identifierProblem("-- !! --", set)).toBeNull(); // nothing spelled at all
  });
});

describe("loadDerivedSet", () => {
  // Each repo is a real (empty, by default) git checkout: publish lists a private repo's tracked files.
  function loaded(deps: ReturnType<typeof makeDeps>, repos: Record<string, { private?: boolean; path?: string }>) {
    return { profile: { tracker: { repo: "toolkit" } }, repos: Object.fromEntries(Object.entries(repos).map(([k, v]) => [k, { name: k, path: gitRepo({}), ...v }])) } as unknown as Parameters<typeof loadDerivedSet>[1];
  }

  it("returns an empty set when no repo is private, without needing a word list", async () => {
    const deps = makeDeps({ env: { SINDRI_WORDS: "/nonexistent" } });
    expect((await loadDerivedSet(deps, loaded(deps, { toolkit: {}, other: { private: false } }))).size).toBe(0);
  });

  it("refuses with SND-EVOLVE-018 when a repo is private and the word list is missing", async () => {
    const deps = makeDeps({ env: { SINDRI_WORDS: "/nonexistent" } });
    await expect(loadDerivedSet(deps, loaded(deps, { toolkit: {}, globex: { private: true } }))).rejects.toThrow(expect.objectContaining({ code: "SND-EVOLVE-018" }) as SindriError);
  });

  it("refuses with SND-EVOLVE-017 when a private repo has no index or no ok structure layer", async () => {
    const words = path.join(tempDir("w-"), "words");
    fs.writeFileSync(words, "schedule\nnurse\n");
    const deps = makeDeps({ env: { SINDRI_WORDS: words } });
    const l = loaded(deps, { toolkit: {}, globex: { private: true } });
    await expect(loadDerivedSet(deps, l)).rejects.toThrow(expect.objectContaining({ code: "SND-EVOLVE-017" }) as SindriError);
    seedIndex(openIndex(indexPath(deps, "globex")), { files: ["a.ts"], symbols: ["quarbleScheduler"], structure: "pending" }).close();
    await expect(loadDerivedSet(deps, l)).rejects.toThrow(expect.objectContaining({ code: "SND-EVOLVE-017" }) as SindriError);
  });

  it("reads an index whose structure layer is ok while embeddings are still pending (Review Focus 3)", async () => {
    const words = path.join(tempDir("w-"), "words");
    fs.writeFileSync(words, "Schedule\nnurse\n");
    const deps = makeDeps({ env: { SINDRI_WORDS: words } });
    seedIndex(openIndex(indexPath(deps, "toolkit")), { files: ["t.ts"], symbols: ["renderTask"] }).close();
    const db = seedIndex(openIndex(indexPath(deps, "globex")), { files: ["a.ts"], symbols: ["quarbleScheduler", "schedule"] });
    db.prepare("INSERT INTO layers (layer, stamp, status, detail, built_at) VALUES ('embeddings', 's', 'pending', 'embedded 1 of 9', 'x')").run();
    db.close();
    const set = await loadDerivedSet(deps, loaded(deps, { toolkit: {}, globex: { private: true } }));
    expect([...set]).toEqual(["quarblescheduler"]);
  });

  it("never treats the tracker repo as private, subtracting its vocabulary instead", async () => {
    const words = path.join(tempDir("w-"), "words");
    fs.writeFileSync(words, "nurse\n");
    const deps = makeDeps({ env: { SINDRI_WORDS: words } });
    seedIndex(openIndex(indexPath(deps, "toolkit")), { files: ["t.ts"], symbols: ["renderTask"] }).close();
    seedIndex(openIndex(indexPath(deps, "globex")), { files: ["a.ts"], symbols: ["quarbleScheduler", "renderTask"] }).close();
    const set = await loadDerivedSet(deps, loaded(deps, { toolkit: { private: true }, globex: { private: true } }));
    expect([...set]).toEqual(["quarblescheduler"]);
  });

  it("returns an empty set, without a word list, when only the tracker repo is marked private", async () => {
    const deps = makeDeps({ env: { SINDRI_WORDS: "/nonexistent" } });
    expect((await loadDerivedSet(deps, loaded(deps, { toolkit: { private: true } }))).size).toBe(0);
  });

  it("refuses with SND-EVOLVE-017, with the rm fix, when the private index is missing any table or isn't a database", async () => {
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
      await expect(loadDerivedSet(deps, l), table).rejects.toThrow(expect.objectContaining({
        code: "SND-EVOLVE-017", message: `repo globex is private but its code index is unusable (no usable ${table} table)`,
        fix: `rm ${file}, then sindri index build --repo globex, then rerun sindri evolve publish`,
      }) as SindriError);
    }
    fs.writeFileSync(file, "this file is not a SQLite database, only text that fills its first page".repeat(4));
    await expect(loadDerivedSet(deps, l)).rejects.toThrow(expect.objectContaining({ code: "SND-EVOLVE-017", message: "repo globex is private but its code index is unusable (not a database)" }) as SindriError);
  });

  it("subtracts nothing when the toolkit index is unusable: more identifiers are held, never fewer", async () => {
    const words = path.join(tempDir("w-"), "words");
    fs.writeFileSync(words, "nurse\n");
    const deps = makeDeps({ env: { SINDRI_WORDS: words } });
    fs.mkdirSync(path.dirname(indexPath(deps, "toolkit")), { recursive: true });
    fs.writeFileSync(indexPath(deps, "toolkit"), "not a database either, just a page of plain text".repeat(6));
    seedIndex(openIndex(indexPath(deps, "globex")), { files: ["a.ts"], symbols: ["renderTask"] }).close();
    expect([...(await loadDerivedSet(deps, loaded(deps, { toolkit: {}, globex: { private: true } })))]).toEqual(["rendertask"]);
  });

  it("refuses with SND-EVOLVE-017, not an internal error, when the private index is corrupt past its first pages", async () => {
    const words = path.join(tempDir("w-"), "words");
    fs.writeFileSync(words, "nurse\n");
    const deps = makeDeps({ env: { SINDRI_WORDS: words } });
    seedCorruptIndex(openIndex(indexPath(deps, "globex")), indexPath(deps, "globex"));
    await expect(loadDerivedSet(deps, loaded(deps, { toolkit: {}, globex: { private: true } }))).rejects.toThrow(expect.objectContaining({
      code: "SND-EVOLVE-017", message: "repo globex is private but its code index is unusable (unreadable pages)",
    }) as SindriError);
  });

  it("adds the path segments of every tracked file of a private repo, also those its index skips (denyPaths, secrets, non-source)", async () => {
    const words = path.join(tempDir("w-"), "words");
    fs.writeFileSync(words, "nurse\nexports\nsecrets\n");
    const deps = makeDeps({ env: { SINDRI_WORDS: words } });
    seedIndex(openIndex(indexPath(deps, "globex")), { files: ["a.ts"], symbols: ["quarbleScheduler"] }).close();
    const repo = gitRepo({ "customer-exports/zorbBillingMigration.sql": "x\n", "config/secrets/vexillaKeys.yaml": "x\n", "nurse.md": "x\n" });
    fs.writeFileSync(path.join(repo, "untrackedPlumbus.txt"), "x\n");
    const set = await loadDerivedSet(deps, loaded(deps, { toolkit: {}, globex: { private: true, path: repo } }));
    expect([...set].sort()).toEqual(["config", "customer", "customerexports", "quarblescheduler", "vexillakeys", "zorbbillingmigration"]);
  });

  it("adds the tracked file names inside a private repo's submodules (git ls-files --recurse-submodules)", async () => {
    const words = path.join(tempDir("w-"), "words");
    fs.writeFileSync(words, "nurse\nvendor\ninner\n");
    const deps = makeDeps({ env: { SINDRI_WORDS: words } });
    seedIndex(openIndex(indexPath(deps, "globex")), { files: ["a.ts"], symbols: ["quarbleScheduler"] }).close();
    const sub = gitRepo({ "plumbob-kit/frobnitzWidget.ts": "x\n" });
    const repo = gitRepo({ "nurse.md": "x\n" });
    git(repo, "-c", "protocol.file.allow=always", "submodule", "add", "-q", sub, "vendor/inner");
    git(repo, "commit", "-qm", "sub");
    const set = await loadDerivedSet(deps, loaded(deps, { toolkit: {}, globex: { private: true, path: repo } }));
    expect(set.has("frobnitzwidget")).toBe(true);
    expect(set.has("plumbob")).toBe(true);
  });

  it("refuses with SND-EVOLVE-019 when a private repo's tracked files can't be listed (fail closed)", async () => {
    const words = path.join(tempDir("w-"), "words");
    fs.writeFileSync(words, "nurse\n");
    const deps = makeDeps({ env: { SINDRI_WORDS: words } });
    seedIndex(openIndex(indexPath(deps, "globex")), { files: ["a.ts"], symbols: ["quarbleScheduler"] }).close();
    const notGit = tempDir("not-a-repo-");
    await expect(loadDerivedSet(deps, loaded(deps, { toolkit: {}, globex: { private: true, path: notGit } }))).rejects.toThrow(expect.objectContaining({
      code: "SND-EVOLVE-019", message: "couldn't list the tracked files of private repo globex, so publish can't check proposals for their names",
      fix: "check that repos/globex.yaml's path is that repo's git checkout on this machine, then rerun sindri evolve publish",
    }) as SindriError);
  });

  it("names private and unset repos the same way for doctor and publish, never the tracker repo", async () => {
    const deps = makeDeps();
    const l = loaded(deps, { toolkit: { private: true }, b: { private: true }, a: { private: true }, c: {}, d: { private: false } });
    expect(privateRepos(l)).toEqual(["a", "b"]);
    expect(unsetPrivateRepos(l)).toEqual(["c"]);
  });

  it("refuses with SND-EVOLVE-017 when the private index has no usable layers table", async () => {
    const words = path.join(tempDir("w-"), "words");
    fs.writeFileSync(words, "nurse\n");
    const deps = makeDeps({ env: { SINDRI_WORDS: words } });
    const db = seedIndex(openIndex(indexPath(deps, "globex")), { files: ["a.ts"], symbols: ["quarbleScheduler"] });
    db.exec("DROP TABLE layers");
    db.close();
    await expect(loadDerivedSet(deps, loaded(deps, { toolkit: {}, globex: { private: true } }))).rejects.toThrow(expect.objectContaining({ code: "SND-EVOLVE-017" }) as SindriError);
  });
});
