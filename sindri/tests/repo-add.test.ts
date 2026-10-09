import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { runCli } from "../src/main.js";
import { sanitizeName } from "../src/profile/commands.js";
import { git, gitRepo, makeDeps, tempDir } from "./helpers.js";

describe("sindri repo add", () => {
  it("adds a repo to the live profile, keeps comments, asks for approval, creates no mirror, and is idempotent", async () => {
    const d = makeDeps();
    await runCli(["profile", "init"], d);
    const target = gitRepo({ "a.ts": "export const a = 1;\n" });
    const r = await runCli(["repo", "add", target, "--name", "webapp"], d);
    expect(r.exitCode).toBe(0);
    expect(r.stdout).toContain("Added webapp");
    expect(r.stdout).toContain("sindri profile approve");
    const state = d.env.AW_STATE_DIR as string;
    const profile = fs.readFileSync(path.join(state, "profile", "profile.yaml"), "utf8");
    expect(profile).toMatch(/repos:\n {2}- example\n {2}- webapp/);
    expect(profile).toContain("# Example Sindri profile.");
    expect(fs.readFileSync(path.join(state, "profile", "repos", "webapp.yaml"), "utf8")).toContain(`path: ${fs.realpathSync(target)}`);
    expect(fs.existsSync(path.join(state, "sindri", "mirrors"))).toBe(false);
    expect(fs.readdirSync(path.join(state, "profile")).filter((n) => n.includes(".tmp"))).toEqual([]);
    expect((await runCli(["profile", "validate"], d)).exitCode).toBe(0);
    expect((await runCli(["repo", "add", target, "--name", "webapp"], d)).stdout).toContain("webapp is already in the profile.");
  });

  it("from a linked worktree, adds the main checkout and names it after that (final review I2)", async () => {
    const d = makeDeps();
    await runCli(["profile", "init"], d);
    const target = gitRepo({ "a.ts": "1" });
    const wt = path.join(tempDir(), "wt");
    git(target, "worktree", "add", "-q", wt, "-b", "side");
    fs.mkdirSync(path.join(wt, "sub"));
    const r = JSON.parse((await runCli(["repo", "add", path.join(wt, "sub"), "--json"], d)).stdout) as { name: string; path: string };
    expect(r).toMatchObject({ name: sanitizeName(path.basename(fs.realpathSync(target))), path: fs.realpathSync(target) });
    expect((await runCli(["repo", "add", target], d)).stdout).toContain("is already in the profile.");
  });

  it("names a repo after its directory when no name is given", async () => {
    const d = makeDeps();
    await runCli(["profile", "init"], d);
    const target = gitRepo({ "a.ts": "1" });
    const want = sanitizeName(path.basename(fs.realpathSync(target)));
    expect((await runCli(["repo", "add", target], d)).stdout).toContain(`Added ${want} (`);
  });

  it("refuses a --name that sanitizing would change, so it can't escape the profile dir", async () => {
    const d = makeDeps();
    await runCli(["profile", "init"], d);
    const target = gitRepo({ "a.ts": "1" });
    const reposDir = path.join(d.env.AW_STATE_DIR as string, "profile", "repos");
    const before = fs.readdirSync(reposDir);
    for (const name of ["../../x", "Web App"]) {
      const r = await runCli(["repo", "add", target, "--name", name], d);
      expect(r.stderr).toContain("SND-PROFILE-014");
    }
    expect(fs.readdirSync(reposDir)).toEqual(before);
  });

  it("refuses a non-repo path, a name used for another path, an orphan repos file, a missing profile, and bad usage", async () => {
    expect((await runCli(["repo", "add", "."], makeDeps())).stderr).toContain("SND-PROFILE-002");
    const d = makeDeps();
    await runCli(["profile", "init"], d);
    expect((await runCli(["repo", "add", "/"], d)).stderr).toContain("SND-PROFILE-009");
    const a = gitRepo({ "a.ts": "1" });
    const b = gitRepo({ "b.ts": "2" });
    await runCli(["repo", "add", a, "--name", "same"], d);
    expect((await runCli(["repo", "add", b, "--name", "same"], d)).stderr).toContain("SND-PROFILE-013");
    // The profile loader refuses an orphan repos file, so repo add never overwrites one.
    const orphanFile = path.join(d.env.AW_STATE_DIR as string, "profile", "repos", "orphan.yaml");
    fs.writeFileSync(orphanFile, "schemaVersion: 1\nname: orphan\npath: /tmp/x\n");
    const orphan = await runCli(["repo", "add", b, "--name", "orphan"], d);
    expect(orphan.stderr).toContain("SND-PROFILE-001");
    expect(fs.readFileSync(orphanFile, "utf8")).toBe("schemaVersion: 1\nname: orphan\npath: /tmp/x\n");
    fs.rmSync(orphanFile);
    expect((await runCli(["repo"], d)).stderr).toContain("SND-CLI-002");
    expect((await runCli(["repo", "add"], d)).stderr).toContain("SND-CLI-002");
  });

  it("removes the repos file it wrote when the profile.yaml swap fails, so the profile still loads", async () => {
    const d = makeDeps();
    await runCli(["profile", "init"], d);
    const dir = path.join(d.env.AW_STATE_DIR as string, "profile");
    const before = fs.readFileSync(path.join(dir, "profile.yaml"), "utf8");
    // The profile dir refuses new files (the tmp swap file); repos/ stays writable.
    fs.chmodSync(dir, 0o500);
    let r;
    try {
      r = await runCli(["repo", "add", gitRepo({ "a.ts": "1" }), "--name", "webapp"], d);
    } finally {
      fs.chmodSync(dir, 0o700);
    }
    expect(r.exitCode).not.toBe(0);
    expect(fs.existsSync(path.join(dir, "repos", "webapp.yaml"))).toBe(false);
    expect(fs.readdirSync(dir).filter((n) => n.includes(".tmp"))).toEqual([]);
    expect(fs.readFileSync(path.join(dir, "profile.yaml"), "utf8")).toBe(before);
    expect((await runCli(["profile", "validate"], d)).exitCode).toBe(0);
    // A repos/ dir that refuses the write is reported as it is, and the profile is untouched.
    fs.chmodSync(path.join(dir, "repos"), 0o500);
    try {
      r = await runCli(["repo", "add", gitRepo({ "a.ts": "1" }), "--name", "webapp"], d);
    } finally {
      fs.chmodSync(path.join(dir, "repos"), 0o700);
    }
    expect(r.stderr).toContain("EACCES");
    expect(r.stderr).not.toContain("SND-PROFILE-013");
    expect(fs.readFileSync(path.join(dir, "profile.yaml"), "utf8")).toBe(before);
  });

  it("reports a same-name add that raced it as SND-PROFILE-013 and leaves the winner's file", async () => {
    const d0 = makeDeps();
    await runCli(["profile", "init"], d0);
    const winner = path.join(d0.env.AW_STATE_DIR as string, "profile", "repos", "webapp.yaml");
    // The other add writes its repos file after this one loaded the profile (at the git lookup).
    const d = { ...d0, git: { run: async (args: string[], cwd: string) => {
      fs.writeFileSync(winner, "winner\n");
      return d0.git.run(args, cwd);
    } } };
    const r = await runCli(["repo", "add", gitRepo({ "a.ts": "1" }), "--name", "webapp"], d);
    expect(r.stderr).toContain("SND-PROFILE-013");
    expect(r.stderr).toContain("appeared while adding webapp");
    expect(fs.readFileSync(winner, "utf8")).toBe("winner\n");
  });
});
