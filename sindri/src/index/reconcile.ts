import type { Deps } from "../deps.js";
import { withEpoch, type Ledger } from "../ledger/db.js";
import type { LoadedProfile } from "../profile/load.js";
import { readManifestDeps } from "./deps-layer.js";
import { typescriptParser } from "./parse-ts.js";

const DAY = 86_400_000;
// Every call is about a profile repo, never the caller's own, so git's hook-exported repository
// variables are cleared for it.
const FOREIGN = { foreign: true };

export type Outcome = "kept" | "acted-on" | "dropped" | "n/a";

interface Pending {
  run_id: string;
  repo: string;
  ts: string;
  tree: string;
}

interface Due {
  seq: number;
  type: string;
  at: string;
  name: string | null;
  ast_hash: string | null;
  repo: string;
  commit_sha: string;
  ts: string;
}

// Step 1: link each run to the commit that was actually made, by tree hash.
async function linkRuns(db: Ledger, deps: Deps, loaded: LoadedProfile, epoch: number, now: Date): Promise<{ linked: number; dropped: number }> {
  const pending = db.prepare("SELECT run_id, repo, ts, tree FROM shape_runs WHERE commit_sha IS NULL AND tree IS NOT NULL ORDER BY ts").all() as Pending[];
  const byRepo = new Map<string, Pending[]>();
  for (const r of pending) byRepo.set(r.repo, [...(byRepo.get(r.repo) ?? []), r]);
  const links: { runId: string; sha: string }[] = [];
  const drops: string[] = [];
  for (const [repo, runs] of byRepo) {
    const cfg = loaded.repos[repo];
    if (cfg === undefined) continue;
    const since = Math.floor((Date.parse(runs[0].ts) - DAY) / 1000);
    const log = await deps.git.run(["log", "--all", "--format=%H %T", `--since=${since}`], cfg.path, FOREIGN);
    if (!log.ok) continue;
    const trees = new Map<string, string>();
    for (const line of log.stdout.split("\n")) {
      const [sha, tree] = line.split(" ");
      // git prints newest first, so the oldest commit with a given tree wins.
      if (tree !== undefined) trees.set(tree, sha);
    }
    for (const r of runs) {
      const sha = trees.get(r.tree);
      if (sha !== undefined) links.push({ runId: r.run_id, sha });
      else if (now.getTime() - Date.parse(r.ts) > 7 * DAY) drops.push(r.run_id);
    }
  }
  let dropped = 0;
  withEpoch(db, epoch, () => {
    for (const l of links) db.prepare("UPDATE shape_runs SET commit_sha = ? WHERE run_id = ?").run(l.sha, l.runId);
    for (const id of drops) {
      dropped += db.prepare("UPDATE shape_signals SET outcome = 'dropped', labeled_at = ? WHERE run_id = ? AND outcome IS NULL").run(now.toISOString(), id).changes;
    }
  });
  return { linked: links.length, dropped };
}

// Step 2: label each signal whose run is old enough. The outcome is read from the default
// branch's own content (`git show <tip>:<file>`), never from the checked-out working tree or the
// index, so it is right whatever branch the checkout is on (arch r2 N1). Merging is recognised by
// content (`git log -S<name>` on the branch), not by ancestry, so squash and rebase merges count
// (arch r2 N2). The branch tip prefers origin/<branch> after a best-effort fetch, so a stale
// local branch can't mislabel.
async function labelSignals(db: Ledger, deps: Deps, loaded: LoadedProfile, epoch: number, now: Date): Promise<number> {
  const cutoff = now.getTime() - loaded.profile.shape.outcomeDays * DAY;
  const due = (
    db
      .prepare("SELECT s.seq, s.type, s.at, s.name, s.ast_hash, r.repo, r.commit_sha, r.ts FROM shape_signals s JOIN shape_runs r ON r.run_id = s.run_id WHERE s.outcome IS NULL AND r.commit_sha IS NOT NULL ORDER BY s.seq")
      .all() as Due[]
  ).filter((s) => Date.parse(s.ts) <= cutoff);
  const byRepo = new Map<string, Due[]>();
  for (const s of due) byRepo.set(s.repo, [...(byRepo.get(s.repo) ?? []), s]);
  const labels: { seq: number; outcome: Outcome }[] = [];
  for (const [repo, signals] of byRepo) {
    const cfg = loaded.repos[repo];
    if (cfg === undefined) continue;
    // dropped is permanent, so never decide it from a stale view: when the repo has an origin
    // and the fetch fails (offline), leave this repo's signals unlabeled until next time.
    const hasOrigin = (await deps.git.run(["remote", "get-url", "origin"], cfg.path, FOREIGN)).ok;
    // The fetch moves only refs/remotes/origin/<branch> (no tags, no local branch, HEAD, index or
    // working tree). Refs are spelled out in full, so a branch name can't be read as an option.
    if (hasOrigin && !(await deps.git.run(["fetch", "--quiet", "--no-tags", "origin", `refs/heads/${cfg.defaultBranch}`], cfg.path, FOREIGN)).ok) continue;
    const remote = await deps.git.run(["rev-parse", "--verify", "--quiet", `refs/remotes/origin/${cfg.defaultBranch}^{commit}`], cfg.path, FOREIGN);
    const local = remote.ok ? remote : await deps.git.run(["rev-parse", "--verify", "--quiet", `refs/heads/${cfg.defaultBranch}^{commit}`], cfg.path, FOREIGN);
    // Without a default branch to read, nothing can be decided yet: leave the signals unlabeled.
    if (!local.ok) continue;
    const tip = local.stdout.trim();
    const shown = new Map<string, string | null>();
    const fileAt = async (rel: string): Promise<string | null> => {
      if (!shown.has(rel)) {
        const r = await deps.git.run(["show", `${tip}:${rel}`], cfg.path, FOREIGN);
        shown.set(rel, r.ok ? r.stdout : null);
      }
      return shown.get(rel) ?? null;
    };
    for (const s of signals) {
      if (s.type === "simpler:diff-size" || s.type === "simpler:exports" || s.name === null) {
        labels.push({ seq: s.seq, outcome: "n/a" });
        continue;
      }
      const file = s.type === "reinvented:dependency" ? s.at : s.at.slice(0, s.at.lastIndexOf(":"));
      const since = Math.floor((Date.parse(s.ts) - DAY) / 1000);
      const reached = await deps.git.run(["log", tip, `--since=${since}`, `-S${s.name}`, "--format=%H", "--", file], cfg.path, FOREIGN);
      if (!reached.ok) continue;
      if (reached.stdout.trim() === "") {
        // The flagged name never reached the default branch within outcomeDays: the change was dropped.
        labels.push({ seq: s.seq, outcome: "dropped" });
        continue;
      }
      const text = await fileAt(file);
      if (s.type === "reinvented:dependency") {
        labels.push({ seq: s.seq, outcome: text !== null && readManifestDeps(file, text).some((d) => d.name === s.name) ? "kept" : "acted-on" });
        continue;
      }
      const syms = text === null ? [] : typescriptParser.parse(file, text);
      labels.push({ seq: s.seq, outcome: syms.some((x) => x.name === s.name && x.astHash === s.ast_hash) ? "kept" : "acted-on" });
    }
  }
  withEpoch(db, epoch, () => {
    for (const l of labels) db.prepare("UPDATE shape_signals SET outcome = ?, labeled_at = ? WHERE seq = ?").run(l.outcome, now.toISOString(), l.seq);
  });
  return labels.length;
}

// Call inside the tick lock, with the epoch it was acquired under. Idempotent.
export async function reconcileShape(db: Ledger, deps: Deps, loaded: LoadedProfile, epoch: number): Promise<{ linked: number; labeled: number }> {
  const now = deps.now();
  const { linked, dropped } = await linkRuns(db, deps, loaded, epoch, now);
  return { linked, labeled: dropped + (await labelSignals(db, deps, loaded, epoch, now)) };
}
