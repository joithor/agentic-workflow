import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { channelsRoot, readChannels, wrapperTarget, writeChannels, writeWrapper } from "../src/evolve/channel.js";
import { makeChannelCommand } from "../src/evolve/cmd/channel.js";
import type { GitRunner } from "../src/git.js";
import { evolveFixture, fakeProc } from "./evolve-fixtures.js";
import { makeDeps, tempDir } from "./helpers.js";

const A = "a".repeat(40);
const B = "b".repeat(40);
const entry = (sha: string, dir: string, at = "2026-10-01T00:00:00Z") => ({ sha, dir, installedAt: at });

async function ready(o: { tty?: string | null; git?: GitRunner; bin?: string } = {}) {
  const fx = await evolveFixture();
  const asked: string[] = [];
  const deps = {
    ...fx.deps,
    env: { ...fx.deps.env, CLAUDE_LOCAL_BIN: o.bin ?? tempDir() },
    git: o.git ?? fx.deps.git,
    isTTY: o.tty !== undefined && o.tty !== null,
    prompt: async (q: string) => {
      asked.push(q);
      return o.tty ?? "";
    },
  };
  const mk = (channel: "stable" | "next", sha: string): string => {
    const dir = path.join(channelsRoot(deps), channel, sha);
    fs.mkdirSync(path.join(dir, "dist"), { recursive: true });
    fs.writeFileSync(path.join(dir, "dist", "cli.js"), "x");
    return dir;
  };
  const proc = fakeProc(() => ({}));
  const run = (args: string[]) => makeChannelCommand({ process: proc })(args, deps);
  const record = (sha: string) =>
    fx.ctx.db.prepare("INSERT INTO suite_runs (artifact_id, hash, head, dirty, ok, exit_code, ms, ts, epoch) VALUES ('package:sindri', ?, ?, 0, 1, 0, 1, 't', 1)").run(`at:${sha}`, sha);
  return { fx, deps, asked, mk, run, record, proc };
}

describe("sindri channel status", () => {
  it("says so when nothing is recorded, then shows both channels, the reason next can't be promoted, and the wrapper", async () => {
    const t = await ready();
    expect((await t.run(["status"])).stdout).toBe("No channels recorded.\nNext: scripts/install-sindri.sh --channel next --ref <sha>\n");
    const dirA = t.mk("stable", A);
    const dirB = t.mk("next", B);
    writeChannels(t.deps, { stable: { ...entry(A, dirA), previous: null }, next: entry(B, dirB) });
    const r = await t.run(["status"]);
    expect(r.exitCode).toBe(0);
    expect(r.stdout).toBe(
      [
        "stable aaaaaaaa (since 2026-10-01); previous none",
        `next   bbbbbbbb (since 2026-10-01); no passing package:sindri suite run for ${B}; run: sindri evolve check package:sindri --at ${B}`,
        "wrapper: none found",
        `Next: sindri evolve check package:sindri --at ${B}`,
        "",
      ].join("\n"),
    );
    t.record(B);
    expect((await t.run(["status"])).stdout).toContain(`next   bbbbbbbb (since 2026-10-01); soaked 7 days on next; suite passed at that sha\n`);
    expect(JSON.parse((await t.run(["status", "--json"])).stdout)).toMatchObject({ next: { sha: B }, canPromote: { ok: true } });
    writeWrapper(t.deps, path.join(dirA, "dist", "cli.js"));
    expect((await t.run(["status"])).stdout).toContain(`wrapper: runs ${path.join(dirA, "dist", "cli.js")}\n`);
    writeWrapper(t.deps, "/somewhere/else/dist/cli.js");
    const odd = await t.run(["status"]);
    expect(odd.exitCode).toBe(1);
    expect(odd.stdout).toContain("(does not match stable; an in-place install may have overwritten it)");
    writeChannels(t.deps, { stable: null, next: entry(B, dirB) });
    expect((await t.run(["status"])).stdout).toContain("stable none\n");
    t.fx.close();
  });
});

describe("sindri channel status with no next build", () => {
  it("shows the previous stable build and says nothing is on next", async () => {
    const t = await ready();
    writeChannels(t.deps, { stable: { ...entry(B, t.mk("stable", B)), previous: entry(A, t.mk("stable", A)) }, next: null });
    const r = await t.run(["status"]);
    expect(r.stdout).toBe(
      [
        "stable bbbbbbbb (since 2026-10-01); previous aaaaaaaa",
        "next   none",
        "wrapper: none found",
        "Next: scripts/install-sindri.sh --channel next --ref <sha>",
        "",
      ].join("\n"),
    );
    t.fx.close();
  });
});

describe("sindri channel promote (Review Focus: human-only, protected paths shown)", () => {
  const protectedDiff: GitRunner = { run: async (args) => (args[0] === "diff" ? { ok: true, stdout: "sindri/src/scrub/patterns.ts\nskills/review/SKILL.md\n" } : { ok: false, stderr: "" }) };

  it("refuses until the soak, the suite run, a terminal and the typed confirmation are all there", async () => {
    const t = await ready({ tty: null });
    const dirA = t.mk("stable", A);
    const dirB = t.mk("next", B);
    writeChannels(t.deps, { stable: { ...entry(A, dirA), previous: null }, next: entry(B, dirB, "2026-10-07T00:00:00Z") });
    expect((await t.run(["promote", B])).stderr).toContain("SND-EVOLVE-005 next has soaked 1 of 3 days");
    writeChannels(t.deps, { stable: { ...entry(A, dirA), previous: null }, next: entry(B, dirB) });
    const noSuite = await t.run(["promote", B]);
    expect(noSuite.stderr).toContain(`SND-EVOLVE-005 no passing package:sindri suite run for ${B}`);
    expect(noSuite.stderr).toContain(`fix: sindri evolve check package:sindri --at ${B}`);
    t.record(B);
    expect((await t.run(["promote", B])).stderr).toContain("SND-EVOLVE-006");
    expect((await t.run(["promote", "bbbb"])).stderr).toContain("SND-CLI-002 usage: sindri channel promote <40-character sha>");
    expect((await t.run(["promote"])).stderr).toContain("SND-CLI-002");
    t.fx.close();
    const wrong = await ready({ tty: "nope" });
    const wa = wrong.mk("stable", A);
    const wb = wrong.mk("next", B);
    writeChannels(wrong.deps, { stable: { ...entry(A, wa), previous: null }, next: entry(B, wb) });
    wrong.record(B);
    expect((await wrong.run(["promote", B])).stderr).toContain("SND-EVOLVE-007");
    expect(readChannels(wrong.deps).stable?.sha).toBe(A);
    wrong.fx.close();
  });

  it("shows the protected paths that changed since stable, then promotes and records it", async () => {
    const t = await ready({ tty: "bbbbbbbb", git: protectedDiff });
    const dirA = t.mk("stable", A);
    const dirB = t.mk("next", B);
    writeChannels(t.deps, { stable: { ...entry(A, dirA), previous: null }, next: entry(B, dirB) });
    t.record(B);
    const r = await t.run(["promote", B]);
    expect(r.stdout).toBe("Promoted bbbbbbbb to stable. Roll back with: sindri channel rollback\nNext: sindri channel status\n");
    expect(t.asked[0]).toBe("Promote bbbbbbbb to stable?\nProtected paths changed since stable: sindri/src/scrub/patterns.ts\nType the first 8 characters of the sha to confirm: ");
    expect(wrapperTarget(t.deps)).toBe(path.join(dirB, "dist", "cli.js"));
    expect(readChannels(t.deps).stable).toMatchObject({ sha: B, previous: { sha: A } });
    expect(t.fx.ctx.db.prepare("SELECT verb, detail FROM evolve_audit").all()).toEqual([{ verb: "promote", detail: `${B} (previous ${A})` }]);
    t.fx.close();
  });

  it("words the prompt for a clean diff, an unreadable diff and a first stable build", async () => {
    const clean = await ready({ tty: "bbbbbbbb", git: { run: async () => ({ ok: true, stdout: "skills/review/SKILL.md\n" }) } });
    writeChannels(clean.deps, { stable: { ...entry(A, clean.mk("stable", A)), previous: null }, next: entry(B, clean.mk("next", B)) });
    clean.record(B);
    await clean.run(["promote", B]);
    expect(clean.asked[0]).toContain("No protected paths changed since stable.");
    clean.fx.close();
    const broken = await ready({ tty: "bbbbbbbb", git: { run: async () => ({ ok: false, stderr: "bad object" }) } });
    writeChannels(broken.deps, { stable: { ...entry(A, broken.mk("stable", A)), previous: null }, next: entry(B, broken.mk("next", B)) });
    broken.record(B);
    await broken.run(["promote", B]);
    expect(broken.asked[0]).toContain("Couldn't list the changes since stable.");
    broken.fx.close();
    const first = await ready({ tty: "bbbbbbbb" });
    writeChannels(first.deps, { stable: null, next: entry(B, first.mk("next", B)) });
    first.record(B);
    await first.run(["promote", B]);
    expect(first.asked[0]).toContain("This is the first stable build.");
    first.fx.close();
  });
});

describe("sindri channel rollback", () => {
  it("needs a terminal and a typed confirmation, then points stable back at the previous build", async () => {
    const t = await ready({ tty: null });
    const dirA = t.mk("stable", A);
    const dirB = t.mk("stable", B);
    writeChannels(t.deps, { stable: { ...entry(B, dirB), previous: entry(A, dirA) }, next: null });
    expect((await t.run(["rollback"])).stderr).toContain("SND-EVOLVE-006");
    t.fx.close();
    const wrong = await ready({ tty: "nope" });
    writeChannels(wrong.deps, { stable: { ...entry(B, wrong.mk("stable", B)), previous: entry(A, wrong.mk("stable", A)) }, next: null });
    expect((await wrong.run(["rollback"])).stderr).toContain("SND-EVOLVE-007");
    wrong.fx.close();
    const ok = await ready({ tty: "aaaaaaaa" });
    const a = ok.mk("stable", A);
    writeChannels(ok.deps, { stable: { ...entry(B, ok.mk("stable", B)), previous: entry(A, a) }, next: null });
    const r = await ok.run(["rollback"]);
    expect(r.stdout).toBe("Rolled back to aaaaaaaa. Roll forward again with: sindri channel rollback\nNext: sindri channel status\n");
    expect(ok.asked[0]).toBe("Roll stable back to aaaaaaaa?\nType the first 8 characters of the sha to confirm: ");
    expect(readChannels(ok.deps).stable?.sha).toBe(A);
    expect(ok.fx.ctx.db.prepare("SELECT verb FROM evolve_audit").all()).toEqual([{ verb: "rollback" }]);
    const none = await ready({ tty: "x" });
    expect((await none.run(["rollback"])).stderr).toContain("SND-EVOLVE-005 there is no previous stable build to roll back to");
    ok.fx.close();
    none.fx.close();
  });
});

describe("the channel dispatcher", () => {
  it("rejects an unknown subcommand and needs an approved profile", async () => {
    const t = await ready();
    expect((await t.run(["nope"])).stderr).toContain("SND-CLI-002 unknown channel subcommand: nope; use status, promote, rollback");
    expect((await t.run([])).stderr).toContain("(none)");
    const bare = await makeChannelCommand({ process: t.proc })(["status"], makeDeps());
    expect(bare.stderr).toContain("SND-PROFILE-012");
    t.fx.close();
  });
});
