import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import { makeTracker } from "../adapters/registry.js";
import { unwrap, type WorkItem } from "../adapters/types.js";
import { parseFlags } from "../args.js";
import { stateDir, type Deps } from "../deps.js";
import { SindriError } from "../errors.js";
import { ulid } from "../ids.js";
import { ledgerPath, openLedger, withEpoch, type Ledger } from "../ledger/db.js";
import { listEvents, markMissing, setCursor, upsertItem } from "../ledger/items.js";
import { acquireTickLock } from "../lock/lock.js";
import type { Command } from "../main.js";
import { fromError, success, type CommandResult } from "../output.js";
import { approvedProfile } from "../profile/approve.js";
import { requireProfile, ring0Files } from "../profile/commands.js";
import { loadProfile, resolveProfileRoot, type LoadedProfile } from "../profile/load.js";
import { compileExtraPatterns, makeScrubber } from "../scrub/scrub.js";
import { assess, nextPerPlan, startBlocker, type Assessment } from "./size.js";

export interface ObserveRow {
  id: string;
  title: string;
  state: "open" | "done";
  size: string;
  ambiguity: string;
  trusted: boolean;
  next: boolean;
  steps: string;
  wouldStart: boolean;
  blocker: string | null;
}

interface Snapshot {
  items: WorkItem[];
  cursor: string;
}

// No profile: observe the current repo through a throwaway ring-0 profile, never recording.
async function ephemeralProfile(deps: Deps): Promise<LoadedProfile> {
  let files: Record<string, string>;
  try {
    files = await ring0Files(deps, "me", deps.system.hostname());
  } catch {
    throw new SindriError("SND-PROFILE-002", "no profile found, and the current directory is not a repo with plan files", {
      fix: "sindri profile init --ring0 (from a repo with docs/superpowers/plans) or sindri profile init",
    });
  }
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "sindri-observe-"));
  try {
    for (const [rel, text] of Object.entries(files)) {
      fs.mkdirSync(path.dirname(path.join(dir, rel)), { recursive: true });
      fs.writeFileSync(path.join(dir, rel), text);
    }
    return unwrapProfile(loadProfile(dir));
  } finally {
    fs.rmSync(dir, { recursive: true, force: true });
  }
}

function unwrapProfile(r: ReturnType<typeof loadProfile>): LoadedProfile {
  if (r.ok) return r.value;
  throw new SindriError("SND-PROFILE-001", "the generated ring-0 profile is invalid", { details: r.issues.map((i) => `${i.file}: ${i.message}`) });
}

async function readAll(loaded: LoadedProfile, deps: Deps): Promise<Snapshot> {
  const tracker = makeTracker(loaded, deps);
  const scan = unwrap(await tracker.scan({ includeDone: true }));
  const items: WorkItem[] = [];
  for (const ref of scan.items) items.push(unwrap(await tracker.read(ref.id)));
  items.sort((a, b) => Number(a.meta.order) - Number(b.meta.order));
  return { items, cursor: scan.cursor };
}

const sourceOf = (loaded: LoadedProfile): string => `${loaded.profile.tracker.type}:${loaded.profile.tracker.repo}`;

// Call while holding the tick lock, with the epoch it was acquired under.
function record(db: Ledger, deps: Deps, loaded: LoadedProfile, snap: Snapshot, epoch: number): { new: number; changed: number; removed: number } {
  const scrubber = makeScrubber(compileExtraPatterns(loaded.profile.scrub.extraPatterns));
  const ctx = { epoch, tickId: ulid(deps.now()), now: deps.now(), scrubber };
  const source = sourceOf(loaded);
  return withEpoch(db, epoch, () => {
    const counts = { new: 0, changed: 0, removed: 0 };
    for (const item of snap.items) {
      const a = assess(item, loaded.profile);
      const outcome = upsertItem(db, ctx, {
        id: item.id, source, title: item.title, state: item.state, size: a.size, sizedBy: a.size === null ? null : a.sizedBy,
        ambiguity: a.ambiguity, stepsDone: Number(item.meta.stepsDone), stepsTotal: Number(item.meta.stepsTotal),
        contentHash: `${String(item.meta.contentHash)}:${item.state}`,
      });
      if (outcome !== "same") counts[outcome]++;
    }
    counts.removed = markMissing(db, ctx, source, new Set(snap.items.map((i) => i.id)));
    setCursor(db, source, snap.cursor, deps.now());
    return counts;
  });
}

function table(rows: ObserveRow[]): string[] {
  const head = ["ITEM", "SIZE", "AMBIGUITY", "TRUSTED", "NEXT", "STEPS", "WOULD-START", "TITLE"];
  const cells = rows.map((r) => [r.id, r.size, r.ambiguity, r.trusted ? "yes" : "no", r.next ? "yes" : "no", r.steps, r.blocker ?? "yes", r.title.slice(0, 60)]);
  const widths = head.map((h, i) => Math.max(h.length, ...cells.map((c) => c[i].length)));
  const fmt = (c: string[]): string => c.map((v, i) => (i === c.length - 1 ? v : v.padEnd(widths[i]))).join("  ");
  return [fmt(head), ...cells.map(fmt)];
}

function report(
  loaded: LoadedProfile, snap: Snapshot, recorded: { new: number; changed: number; removed: number } | null, note: string, attention: boolean, json: boolean,
): CommandResult {
  const scrubber = makeScrubber(compileExtraPatterns(loaded.profile.scrub.extraPatterns));
  const next = nextPerPlan(snap.items);
  const rows: ObserveRow[] = snap.items.map((i) => {
    const a: Assessment = assess(i, loaded.profile);
    const blocker = startBlocker(i, a, next.has(i.id), loaded.profile.autoStartMaxSize);
    return {
      id: i.id, title: scrubber.scrub(i.title).text, state: i.state, size: a.size ?? "?", ambiguity: a.ambiguity, trusted: a.trusted,
      next: next.has(i.id), steps: `${String(i.meta.stepsDone)}/${String(i.meta.stepsTotal)}`, wouldStart: blocker === null, blocker,
    };
  });
  const open = rows.filter((r) => r.state === "open");
  const starts = rows.filter((r) => r.wouldStart).length;
  const up = rows.find((r) => r.wouldStart) ?? rows.find((r) => r.next);
  const lines = [
    ...(open.length === 0 ? ["No open items."] : table(open)),
    "",
    `Observed ${rows.length} items (${open.length} open); would start ${starts}.`,
    ...(up === undefined ? [] : [`Next up: ${up.id}`]),
    note.trim(),
    "Nothing outside the ledger changed.",
  ];
  const data = { observed: rows.length, open: open.length, wouldStart: starts, nextUp: up?.id ?? null, recorded, note, items: rows };
  return success(lines.join("\n"), data, json, attention ? 1 : 0);
}

export const observeCommand: Command = async (args, deps) => {
  const json = args.includes("--json");
  try {
    const { values } = parseFlags(args, { profile: { type: "string" }, json: { type: "boolean" }, "no-record": { type: "boolean" } });
    const root = resolveProfileRoot(deps, values.profile);
    if (root === null) {
      const loaded = await ephemeralProfile(deps);
      return report(loaded, await readAll(loaded, deps), null, "Not recorded: no profile (run `sindri profile init --ring0` to keep a ledger).", false, json);
    }
    const live = requireProfile(deps, values.profile);
    const db = openLedger(ledgerPath(stateDir(deps)));
    try {
      // Spec §8.7: runtime uses the last approved snapshot, never unapproved edits.
      const approved = approvedProfile(deps, db);
      if (approved === null) {
        const note = `Not recorded: profile ${live.hash.slice(0, 12)} is not approved (run \`sindri profile approve\`).`;
        return report(live, await readAll(live, deps), null, note, true, json);
      }
      const drift = approved.hash === live.hash ? "" : ` Using approved profile ${approved.hash.slice(0, 12)}; the live profile has unapproved changes (sindri profile approve).`;
      const host = deps.system.hostname();
      const active = approved.profile.hosts.active;
      if (values["no-record"] === true) return report(approved, await readAll(approved, deps), null, `Not recorded: --no-record.${drift}`, drift !== "", json);
      if (active !== host) {
        return report(approved, await readAll(approved, deps), null, `Not recorded: this host (${host}) is not hosts.active (${active}).${drift}`, true, json);
      }
      const lock = acquireTickLock({ dir: stateDir(deps), db, sys: deps.system, now: deps.now });
      if (!lock.ok) {
        const r = report(approved, await readAll(approved, deps), null, `Not recorded: another run holds the lock.${drift}`, drift !== "", json);
        return { ...r, stderr: `no-op: ${lock.detail}\n` };
      }
      try {
        const snap = await readAll(approved, deps); // inside the lock: no older snapshot can overwrite a newer one
        const counts = record(db, deps, approved, snap, lock.owner.epoch);
        const note = `Recorded ${counts.new} new, ${counts.changed} changed, ${counts.removed} removed in the ledger.${drift}`;
        return report(approved, snap, counts, note, drift !== "", json);
      } finally {
        lock.release();
      }
    } finally {
      db.close();
    }
  } catch (e) {
    return fromError(e, json);
  }
};

export function parseSince(s: string, now: Date): Date {
  const m = /^(\d+)([dh])$/.exec(s);
  if (m === null) throw new SindriError("SND-CLI-002", `--since must look like 7d or 12h, got ${s}`, { fix: "sindri ledger --since 7d" });
  return new Date(now.getTime() - Number(m[1]) * (m[2] === "d" ? 86_400_000 : 3_600_000));
}

export const ledgerCommand: Command = async (args, deps) => {
  const json = args.includes("--json");
  try {
    const { values } = parseFlags(args, { item: { type: "string" }, since: { type: "string" }, json: { type: "boolean" } });
    const since = values.since === undefined ? undefined : parseSince(values.since, deps.now());
    // A read never creates the ledger.
    if (!fs.existsSync(ledgerPath(stateDir(deps)))) return success("No ledger yet (sindri observe records one once the profile is approved).", [], json);
    const db = openLedger(ledgerPath(stateDir(deps)));
    try {
      if (values.item !== undefined && db.prepare("SELECT 1 FROM items WHERE id = ?").get(values.item) === undefined) {
        throw new SindriError("SND-ITEM-404", `no such item: ${values.item}`);
      }
      const rows = listEvents(db, { itemId: values.item, since });
      const text = rows.length === 0 ? "No ledger rows match." : rows.map((r) => `${r.ts}  ${r.item_id}  ${r.kind}  ${r.detail}`).join("\n");
      return success(text, rows, json);
    } finally {
      db.close();
    }
  } catch (e) {
    return fromError(e, json);
  }
};
