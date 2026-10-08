import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { explainKey } from "../src/profile/explain.js";
import { loadProfile, profileHash, resolveProfileRoot } from "../src/profile/load.js";
import { SIZES, sizeRank } from "../src/profile/schema.js";
import { makeDeps, tempDir } from "./helpers.js";

const EXAMPLE = path.resolve(import.meta.dirname, "../profile/examples/generic");

function copyExample(): string {
  const dir = tempDir("sindri-profile-");
  fs.cpSync(EXAMPLE, dir, { recursive: true });
  return dir;
}

function edit(dir: string, rel: string, fn: (text: string) => string): void {
  const file = path.join(dir, rel);
  fs.writeFileSync(file, fn(fs.readFileSync(file, "utf8")));
}

function issuesOf(dir: string) {
  const r = loadProfile(dir);
  if (r.ok) throw new Error("expected issues");
  return r.issues;
}

describe("loadProfile", () => {
  it("loads the example profile with defaults applied", () => {
    const r = loadProfile(EXAMPLE);
    expect(r.ok).toBe(true);
    if (!r.ok) return;
    expect(r.value.profile.mode).toBe("shadow");
    expect(r.value.profile.providers.allowed).toEqual(["anthropic", "jev"]);
    expect(r.value.profile.scrub.extraPatterns).toEqual([]);
    expect(r.value.profile.budget).toEqual({});
    expect(r.value.repos.example.defaultBranch).toBe("main");
    expect(r.value.files).toEqual(["profile.yaml", "repos/example.yaml"]);
    expect(r.value.hash).toMatch(/^[0-9a-f]{64}$/);
    expect(r.value.hash).toBe(profileHash(EXAMPLE, r.value.files));
    expect(Object.keys(r.value.bytes)).toEqual(["profile.yaml", "repos/example.yaml"]);
  });

  it("names the file and key path of a typo, with a hint", () => {
    const dir = copyExample();
    edit(dir, "profile.yaml", (t) => `${t}\nautoStartMaxSzie: S\n`);
    const [issue] = issuesOf(dir);
    expect(issue).toMatchObject({ file: "profile.yaml", keyPath: "" });
    expect(issue.message).toContain("autoStartMaxSzie");
    expect(issue.hint).toBe("remove the key or fix its spelling");
  });

  it("reports wrong types at their key path without a hint", () => {
    const dir = copyExample();
    edit(dir, "profile.yaml", (t) => t.replace("mode: shadow", "mode: turbo"));
    const [issue] = issuesOf(dir);
    expect(issue).toMatchObject({ file: "profile.yaml", keyPath: "mode" });
    expect(issue.hint).toBeUndefined();
  });

  it("refuses a pasted secret and never prints it (Review Focus 4)", () => {
    const dir = copyExample();
    const secret = "ghp" + "_" + "z".repeat(36);
    edit(dir, "repos/example.yaml", (t) => t.replace(".github/**", secret));
    const issues = issuesOf(dir);
    const hit = issues.find((i) => i.keyPath === "protectedPaths.0");
    expect(hit?.file).toBe("repos/example.yaml");
    expect(hit?.message).toContain("looks like a secret (github-token)");
    expect(JSON.stringify(issues)).not.toContain(secret);
  });

  it("never echoes a secret pasted into an enum or as a key name (Review Focus 4)", () => {
    const dir = copyExample();
    const secret = "ghp" + "_" + "y".repeat(36);
    edit(dir, "profile.yaml", (t) => t.replace("mode: shadow", `mode: ${secret}`) + `\n${secret}: 1\n`);
    const issues = issuesOf(dir);
    expect(issues.map((i) => i.keyPath)).toContain("mode");
    expect(JSON.stringify(issues)).not.toContain(secret);
  });

  it("redacts a secret-shaped key name in the reported key path (m16)", () => {
    const dir = copyExample();
    const secret = "ghp" + "_" + "k".repeat(36);
    const value = "ghp" + "_" + "v".repeat(36);
    edit(dir, "repos/example.yaml", (t) => t.replace("protectedPaths:", `overrides:\n  ${secret}: ${value}\nprotectedPaths:`));
    const issues = issuesOf(dir);
    const hit = issues.find((i) => i.message.startsWith("value looks like a secret"));
    expect(hit?.keyPath).toBe("overrides.[REDACTED:github-token]");
    expect(JSON.stringify(issues)).not.toContain(secret);
    expect(JSON.stringify(issues)).not.toContain(value);
  });

  it("refuses a tracker glob that leaves the repo, and defaults include to every plan", () => {
    const dir = copyExample();
    edit(dir, "profile.yaml", (t) => t.replace("glob: docs/superpowers/plans/*.md", "glob: ../other/*.md"));
    expect(issuesOf(dir)[0]).toMatchObject({ keyPath: "tracker.glob", message: "glob must stay inside the repo (no ..)" });
    const ok = loadProfile(EXAMPLE);
    expect(ok.ok && ok.value.profile.tracker.include).toEqual(["*"]);
  });

  it("cross-checks repos, the tracker repo and file names", () => {
    const dir = copyExample();
    edit(dir, "profile.yaml", (t) => t.replace("repos:\n  - example", "repos:\n  - example\n  - other").replace("repo: example", "repo: missing"));
    fs.writeFileSync(path.join(dir, "repos", "stray.yaml"), "schemaVersion: 1\nname: wrong\npath: /x\n");
    const messages = issuesOf(dir).map((i) => `${i.file}|${i.keyPath}|${i.message}`);
    expect(messages).toContain('repos/stray.yaml|name|name "wrong" must match the file name "stray"');
    expect(messages).toContain("profile.yaml|repos|repo \"other\" has no file repos/other.yaml");
    expect(messages).toContain("repos/stray.yaml||repo file is not listed in profile.yaml repos");
    expect(messages).toContain('profile.yaml|tracker.repo|tracker.repo "missing" is not in repos');
  });

  it("reports repo schema errors, YAML errors and a missing profile.yaml", () => {
    const dir = copyExample();
    edit(dir, "repos/example.yaml", (t) => t.replace("/path/to/repo", "relative/path"));
    expect(issuesOf(dir)[0]).toMatchObject({ file: "repos/example.yaml", keyPath: "path", message: "must be an absolute path" });
    edit(dir, "profile.yaml", () => "mode: [unclosed\n");
    expect(issuesOf(dir)[0].message).toMatch(/^YAML: /);
    fs.rmSync(path.join(dir, "profile.yaml"));
    expect(issuesOf(dir)[0]).toMatchObject({ file: "profile.yaml", message: "file not found", hint: "sindri profile init" });
  });

  it("reports a scrub pattern that doesn't compile", () => {
    const dir = copyExample();
    edit(dir, "profile.yaml", (t) => `${t}\nscrub:\n  extraPatterns:\n    - kind: bad\n      regex: "("\n`);
    expect(issuesOf(dir)[0]).toMatchObject({ file: "profile.yaml", keyPath: "scrub.extraPatterns" });
  });

  it("loads a profile with no repos dir as an issue, not a crash", () => {
    const dir = copyExample();
    fs.rmSync(path.join(dir, "repos"), { recursive: true });
    expect(issuesOf(dir).map((i) => i.message)).toContain('repo "example" has no file repos/example.yaml');
  });

  it("hashes content and order, not timestamps", () => {
    const dir = copyExample();
    const files = ["profile.yaml", "repos/example.yaml"];
    const h1 = profileHash(dir, files);
    fs.utimesSync(path.join(dir, "profile.yaml"), new Date(0), new Date(0));
    expect(profileHash(dir, files)).toBe(h1);
    edit(dir, "profile.yaml", (t) => t.replace("selfMerge: human", "selfMerge: auto"));
    expect(profileHash(dir, files)).not.toBe(h1);
  });
});

describe("resolveProfileRoot", () => {
  it("prefers --profile, then AW_PROFILE_DIR, then $AW_STATE_DIR/profile when present", () => {
    const deps = makeDeps();
    expect(resolveProfileRoot(deps)).toBeNull();
    const link = path.join(deps.env.AW_STATE_DIR as string, "profile");
    fs.mkdirSync(link, { recursive: true });
    expect(resolveProfileRoot(deps)).toBe(link);
    expect(resolveProfileRoot({ ...deps, env: { ...deps.env, AW_PROFILE_DIR: "/env/dir" } })).toBe("/env/dir");
    expect(resolveProfileRoot({ ...deps, cwd: "/work" }, "rel/dir")).toBe("/work/rel/dir");
  });
});

describe("explainKey", () => {
  it("names the source: repo override, profile file or default", () => {
    const dir = copyExample();
    edit(dir, "repos/example.yaml", (t) => `${t}overrides:\n  autoStartMaxSize: S\n`);
    const r = loadProfile(dir);
    if (!r.ok) throw new Error(JSON.stringify(r.issues));
    expect(explainKey(r.value, "autoStartMaxSize", "example")).toEqual({ key: "autoStartMaxSize", value: "S", source: "repos/example.yaml (overrides)" });
    expect(explainKey(r.value, "autoStartMaxSize")).toEqual({ key: "autoStartMaxSize", value: "XS", source: "profile.yaml" });
    expect(explainKey(r.value, "hosts.active")).toEqual({ key: "hosts.active", value: "my-host", source: "profile.yaml" });
    expect(explainKey(r.value, "providers.allowed")).toEqual({ key: "providers.allowed", value: ["anthropic", "jev"], source: "default" });
    expect(explainKey(r.value, "selfMerge", "example")?.source).toBe("profile.yaml");
    expect(explainKey(r.value, "mode", "no-such-repo")?.source).toBe("profile.yaml");
    expect(explainKey(r.value, "no.such.key")).toBeNull();
    expect(explainKey(r.value, "mode.deeper")).toBeNull();
  });
});

describe("sizeRank", () => {
  it("orders XS < S < M < L < XL", () => {
    expect(SIZES.map(sizeRank)).toEqual([0, 1, 2, 3, 4]);
  });
});
