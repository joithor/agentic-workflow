import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { copiedFrom, patternProblem, personalProblem, privacyProblem, runWords, shingleSession, termProblem, toolkitCorpus, RUN_WORDS } from "../src/evolve/privacy.js";
import { realGitRunner } from "../src/git-real.js";
import { gitRepo } from "./helpers.js";

describe("toolkitCorpus (spec §5.5, read from origin/<default branch>)", () => {
  const g = realGitRunner();
  const run = (repo: string, ...args: string[]): void => {
    execFileSync("git", ["-c", "user.name=T", "-c", "user.email=t@example.com", ...args], { cwd: repo });
  };
  const pushed = (repo: string): void => run(repo, "update-ref", "refs/remotes/origin/main", "HEAD");

  it("joins the normalized words of text files at origin/main, skipping binaries, files over 1 MB and symlinks", async () => {
    const repo = gitRepo({
      "a.md": "Hello,  World\nSecond LINE",
      "bin.dat": "abc\u0000secretbinaryword",
      "big.txt": `bigfileword ${"x".repeat(1_000_001)}`,
      "sub dir/c.txt": "spaced path words",
    });
    fs.symlinkSync("a.md", path.join(repo, "link.md"));
    run(repo, "add", "link.md");
    run(repo, "commit", "-qm", "link");
    pushed(repo);
    const c = await toolkitCorpus(g, repo, "main");
    expect(c.startsWith(" ") && c.endsWith(" ")).toBe(true);
    expect(c).toContain(" hello world second line ");
    expect(c).toContain(" spaced path words ");
    expect(c).not.toMatch(/secretbinaryword|bigfileword/);
    expect(c.match(/hello world/g)).toHaveLength(1); // the symlink isn't read a second time
  });

  it("never counts text that hasn't reached origin/main: uncommitted, staged-only or unpushed (security-sentinel-0)", async () => {
    const repo = gitRepo({ "a.md": "published words only" });
    pushed(repo);
    fs.writeFileSync(path.join(repo, "a.md"), "published words only\nuncommitted edit words");
    fs.writeFileSync(path.join(repo, "staged.md"), "staged only words");
    run(repo, "add", "staged.md");
    run(repo, "checkout", "-q", "-b", "work");
    fs.writeFileSync(path.join(repo, "branch.md"), "unpushed branch words");
    run(repo, "add", "branch.md");
    run(repo, "commit", "-qm", "work");
    const c = await toolkitCorpus(g, repo, "main");
    expect(c).toContain(" published words only ");
    expect(c).not.toMatch(/uncommitted|staged only|unpushed/);
  });

  it("keeps files apart, so a run never spans the end of one file and the start of the next", async () => {
    const repo = gitRepo({ "a.md": "one two three four", "b.md": "five six seven eight" });
    pushed(repo);
    const c = await toolkitCorpus(g, repo, "main");
    expect(c).toContain(" one two three four ");
    expect(c).not.toContain(" four five ");
  });

  it("is empty without the origin ref (fail closed), with no text file at it, and when git fails", async () => {
    expect(await toolkitCorpus(g, gitRepo({ "a.md": "local words" }), "main")).toBe("");
    const binOnly = gitRepo({ "bin.dat": "a\u0000b" });
    pushed(binOnly);
    expect(await toolkitCorpus(g, binOnly, "main")).toBe("");
    expect(await toolkitCorpus({ run: async () => ({ ok: false, stderr: "boom", code: 128 }) }, "/nowhere", "main")).toBe("");
  });
});

describe("termProblem: joined, hyphenated and underscored variants (security-sentinel-4)", () => {
  const HELD = "contains a private term";
  it("holds a multi-part term written with its separators removed or swapped, without the owner listing each variant", () => {
    for (const text of ["the AcmeCare portal", "the acme-care portal", "the ACME_CARE flag", "acme.care.example", "AcMe CaRe"]) {
      expect(termProblem(text, ["Acme Care"]), text).toBe(HELD);
    }
    for (const text of ["the Acme Care team", "AcmeCare", "acme_care"]) expect(termProblem(text, ["acme-care"]), text).toBe(HELD);
  });

  it("holds a multi-part term behind any Unicode dash, slash, dot or other punctuation, symbol or space separator", () => {
    const spellings = ["Acme\u2011Care", "Acme\u2013Care", "Acme\u2014Care", "Acme/Care", "Acme\u00b7Care", "Acme\u2010Care", "acme:care", "Acme+Care", "Acme'Care", "Acme\u2009Care", "Acme\u3000Care"];
    for (const text of spellings) expect(termProblem(`the ${text} team`, ["Acme Care"]), JSON.stringify(text)).toBe(HELD);
    // The term's own separators are stripped the same way.
    for (const term of ["Acme\u2013Care", "Acme/Care", "Acme\u00b7Care"]) expect(termProblem("the AcmeCare team", [term]), JSON.stringify(term)).toBe(HELD);
  });

  it("accepts the false positives of a joined match (acceptable for holds), but leaves single-word terms whole-word", () => {
    expect(termProblem("acme carefully", ["Acme Care"])).toBe(HELD); // joined match: a false positive that holds
    expect(termProblem("the Acmeville depot", ["Acme"])).toBeNull(); // no separator in the term: whole words as before
    expect(termProblem("acme and care", ["Acme Care"])).toBeNull();
    expect(termProblem("plain text", ["- _"])).toBeNull(); // a term of separators only never matches everything
  });
});

describe("privacyProblem (Review Focus 6)", () => {
  it("finds profile deny terms as whole words, case-insensitively, without echoing them", () => {
    expect(privacyProblem("Fix the ACME care scheduler", ["acme care"])).toBe("contains a private term");
    expect(privacyProblem("Fix the Acme_Care scheduler", ["Acme"])).toBe("contains a private term"); // an underscore separates words
    expect(privacyProblem("the Acmeville depot", ["Acme"])).toBeNull(); // not a whole word
    expect(privacyProblem("the (Acme) team", ["Acme"])).toBe("contains a private term");
    expect(privacyProblem("a+b is special", ["a+b"])).toBe("contains a private term");
    expect(privacyProblem("nothing here", ["Acme", "Globex"])).toBeNull();
  });

  it("finds emails and home directory paths even with no deny terms", () => {
    expect(privacyProblem("mail joi@example.com please", [])).toBe("contains an email address or a home directory path");
    expect(privacyProblem("see /Users/joi/work/x.ts", [])).toBe("contains an email address or a home directory path");
    expect(privacyProblem("see /home/builder/x.ts", [])).toBe("contains an email address or a home directory path");
    expect(privacyProblem("a plain sentence about done-gate.sh and src/a.ts", [])).toBeNull();
  });
});

describe("privacyProblem: hidden and look-alike spellings (Task 10 fix round 1, I1 + m3)", () => {
  const HELD = "contains a private term";
  it("holds a multi-word term split by a line break, extra spaces, an NBSP, U+2028 or a quote prefix", () => {
    for (const hidden of ["Acme\nCare", "Acme  Care", "Acme\u00a0Care", "Acme\u2028Care", "Acme\r\nCare", "Acme\u0085Care", "Acme\t \u3000Care", "Acme\n> Care"]) {
      expect(privacyProblem(`We saw this at ${hidden} twice`, ["Acme Care"]), JSON.stringify(hidden)).toBe(HELD);
    }
  });

  it("holds fullwidth, decomposed and invisibly-padded spellings, and the Kelvin sign", () => {
    expect(privacyProblem("at \uff21\uff43\uff4d\uff45 today", ["Acme"])).toBe(HELD);
    expect(privacyProblem("a Cafe\u0301 on the corner", ["Caf\u00e9"])).toBe(HELD);
    expect(privacyProblem("a Caf\u00e9 on the corner", ["Cafe\u0301"])).toBe(HELD);
    expect(privacyProblem("at Ac\u200bme Care", ["Acme Care"])).toBe(HELD);
    expect(privacyProblem("at Ac\u00adme\u2060 Care", ["Acme Care"])).toBe(HELD);
    expect(privacyProblem("at A\u202ecme", ["Acme"])).toBe(HELD);
    expect(privacyProblem("a \u212a-team", ["K-team"])).toBe(HELD);
    expect(privacyProblem("at Acme\u200b", ["Acme\u200b Care\u200b"])).toBeNull();
  });

  it("never echoes the term, ignores a term that is only invisible characters, and keeps whole-word matching", () => {
    const reason = privacyProblem("at Acme  Care", ["Acme Care"]);
    expect(reason).toBe(HELD);
    expect(reason).not.toMatch(/Acme/);
    expect(privacyProblem("nothing here", ["\u200b", " "])).toBeNull();
    expect(privacyProblem("the Acmeville\ndepot", ["Acme"])).toBeNull();
  });

  it("holds only an absolute home directory with a user segment (I5)", () => {
    const PATHS = "contains an email address or a home directory path";
    const table: [string, string | null][] = [
      ["/Users/alice/x", PATHS], ["see /home/bob/.ssh", PATHS], ["(/Users/alice/x)", PATHS], ["\"/home/x/y\"", PATHS],
      ["see C:\\Users\\joi\\x.ts", PATHS], ["see c:\\\\users\\\\joi", PATHS],
      ["~/.claude/settings.json", null], ["~/.agentic-workflow/scorer", null], ["src/api/users/x.ts", null], ["app/home/page.tsx", null],
      ["/users/me", null], ["github.com/users/foo", null], ["/users/:id", null], ["GET /users/{id}", null], ["a ~ b and src/x~/y", null],
    ];
    for (const [text, want] of table) expect(privacyProblem(text, []), text).toBe(want);
  });

  it("strips tag characters, variation selectors and U+061C, and holds a term with one inside (I4)", () => {
    expect(privacyProblem("at Ac\u{E0041}me today", ["Acme"])).toBe(HELD);
    expect(privacyProblem("at Acme today", ["Ac\u{E0041}me"])).toBe(HELD);
    expect(privacyProblem("at Ac\u{E0100}me\u061c today", ["Acme"])).toBe(HELD);
    expect(privacyProblem("at Ac\ufe0fme", ["Acme"])).toBe(HELD);
  });

  it("matches across combining marks and Turkish dotted and dotless i (m3)", () => {
    expect(privacyProblem("a Cafe\u0301 here", ["Caf\u00e9"])).toBe(HELD);
    expect(privacyProblem("a Caf\u00e9 here", ["Cafe"])).toBe(HELD);
    expect(privacyProblem("at A\u0332cme", ["Acme"])).toBe(HELD);
    expect(privacyProblem("at Acme", ["A\u0332cme"])).toBe(HELD);
    expect(privacyProblem("at A\u0130cmE", ["Aicme"])).toBe(HELD);
    expect(privacyProblem("at A\u0131cme", ["Aicme"])).toBe(HELD);
    expect(privacyProblem("at Acme", ["A\u0130cme"])).toBeNull();
  });
});

describe("privacyProblem: file URLs, lowercase homes and Claude's encoded project directories (sec-3)", () => {
  const PATHS = "contains an email address or a home directory path";

  it("holds a home path behind a file:// prefix", () => {
    expect(privacyProblem("see file:///Users/joi/work/acme-api/x.ts", [])).toBe(PATHS);
    expect(privacyProblem("see file:///home/builder/x.ts", [])).toBe(PATHS);
    expect(privacyProblem("see https://example.com/Users/page", [])).toBeNull();
  });

  it("holds a lowercase or upper-case /users/<name>/ when <name> is the current OS user, and only then", () => {
    expect(privacyProblem("at /users/joi/work/x.ts", [], "joi")).toBe(PATHS);
    expect(privacyProblem("at /USERS/Joi/x", [], "joi")).toBe(PATHS);
    expect(privacyProblem("at /home/joi", [], "joi")).toBe(PATHS);
    expect(privacyProblem("at /users/joi/work/x.ts", [], "alice")).toBeNull();
    expect(privacyProblem("at /users/joi/work/x.ts", [])).toBeNull();
    expect(privacyProblem("GET /users/joiner/profile", [], "joi")).toBeNull();
    expect(privacyProblem("at github.com/users/joi", [], "joi")).toBeNull();
  });

  it("holds Claude's -Users-<name>- project directory encoding, generically, and lowercase for the current OS user", () => {
    expect(privacyProblem("~/.claude/projects/-Users-joi-personal-agentic-workflow/5e55a1d0.jsonl", [])).toBe(PATHS);
    expect(privacyProblem("see -Users-someone.else-app/x", [])).toBe(PATHS);
    expect(privacyProblem("see -users-joi-app/x", [], "joi")).toBe(PATHS);
    expect(privacyProblem("see -users-joi-app/x", [])).toBeNull();
    expect(privacyProblem("a plain -Users- list and the Users-guide", [])).toBeNull();
  });
});

describe("patternProblem (privacy B1)", () => {
  const P = "matches a privacy.denyPatterns entry";
  it("matches case-insensitively, through fullwidth letters, invisibles and line breaks, without echoing the match", () => {
    expect(patternProblem("see glx-1234 for the repro", ["GLX-\\d+"])).toBe(P);
    expect(patternProblem("see ＧＬＸ-1234", ["GLX-\\d+"])).toBe(P);
    expect(patternProblem("see GL\u200bX-1234", ["GLX-\\d+"])).toBe(P);
    expect(patternProblem("see GLX-\n1234", ["GLX-\\s?\\d+"])).toBe(P);
    expect(patternProblem("see GLX-12 34", ["GLX-\\d{4}"])).toBeNull();
    expect(patternProblem("anything", [])).toBeNull();
  });
});

describe("termProblem and personalProblem split privacyProblem (privacy B1)", () => {
  it("keeps the two reasons separate", () => {
    expect(termProblem("at Globex today", ["Globex"])).toBe("contains a private term");
    expect(termProblem("mail a@b.co", ["Globex"])).toBeNull();
    expect(personalProblem("mail a@b.co")).toBe("contains an email address or a home directory path");
    expect(personalProblem("at Globex today")).toBeNull();
  });
});

describe("copiedFrom (privacy B1, verbatim overlap)", () => {
  const copiedRun = (text: string, sources: readonly string[], isPublic: (run: string) => boolean): boolean => copiedFrom(text, sources.map((x) => shingleSession([x])), isPublic);
  const src = "the overnight rota swap left two shifts without a charge nurse on the ward";
  const never = (): boolean => false;
  it("finds a run of RUN_WORDS words copied from a source, ignoring case and punctuation", () => {
    expect(RUN_WORDS).toBe(8);
    expect(copiedRun("Seen: The overnight rota-swap left two shifts WITHOUT a charge.", [src], never)).toBe(true);
  });
  it("passes a 7-word overlap", () => {
    expect(copiedRun("the overnight rota swap left two shifts, then", [src], never)).toBe(false);
  });
  it("passes a copied run that is public toolkit text", () => {
    const pub = runWords(src);
    expect(copiedRun("the overnight rota swap left two shifts without", [src], (run) => pub.includes(run))).toBe(false);
  });
  it("passes when there are no sources", () => {
    expect(copiedRun(src, [], never)).toBe(false);
  });
  it("passes text shorter than a run", () => {
    expect(copiedRun("the overnight rota", [src], never)).toBe(false);
  });

  it("finds a run that spans two turns of one session (joined across turns)", () => {
    const session = shingleSession(["please check why the overnight rota swap", "left two shifts without cover again"]);
    expect(copiedFrom("Fix: the overnight rota swap left two shifts without a lead", [session], never)).toBe(true);
    // Sessions are never joined to each other.
    const apart = [shingleSession(["please check why the overnight rota swap"]), shingleSession(["left two shifts without cover again"])];
    expect(copiedFrom("Fix: the overnight rota swap left two shifts without a lead", apart, never)).toBe(false);
  });

  it("finds a short turn (4 to 7 words) copied whole, ignoring case and punctuation", () => {
    for (const turn of ["fix the zorb reconcile cron", "Fix the zorb reconcile cron for payroll!", "migrate the ledger sync first"]) {
      expect(copiedFrom(`Title: ${turn.toUpperCase()} soon`, [shingleSession([turn])], never), turn).toBe(true);
    }
  });

  it("passes a 3-word turn copied whole, and a short turn copied only in part (near misses)", () => {
    expect(copiedFrom("Please retry flaky checks today", [shingleSession(["retry flaky checks"])], never)).toBe(false);
    expect(copiedFrom("fix the zorb reconcile job", [shingleSession(["fix the zorb reconcile cron"])], never)).toBe(false);
    expect(copiedFrom("prefix the zorb reconcile cronjob", [shingleSession(["fix the zorb reconcile cron"])], never)).toBe(false); // whole words only
  });

  it("passes a short turn the toolkit's own text already holds", () => {
    const pub = ` ${runWords("Run the done gate first")} `;
    expect(copiedFrom("Always run the done gate first.", [shingleSession(["run the done gate first"])], (t) => pub.includes(` ${t} `))).toBe(false);
  });
});
