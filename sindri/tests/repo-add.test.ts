import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { runCli } from "../src/main.js";
import { sanitizeName } from "../src/profile/commands.js";
import { gitRepo, makeDeps } from "./helpers.js";

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
});
