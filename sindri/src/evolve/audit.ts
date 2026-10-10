import type { Deps } from "../deps.js";
import type { Ledger } from "../ledger/db.js";
import { makeScrubber, type Scrubber } from "../scrub/scrub.js";

// Every privileged verb (reject, adopt, revert, publish, promote, rollback) leaves a row (spec §8.7).
export function audit(db: Ledger, deps: Deps, verb: string, detail: string, epoch: number, scrubber: Scrubber = makeScrubber()): void {
  db.prepare("INSERT INTO evolve_audit (ts, verb, actor, detail, epoch) VALUES (?, ?, ?, ?, ?)").run(
    deps.now().toISOString(), verb, deps.system.username(), scrubber.scrub(detail).text.slice(0, 500), epoch,
  );
}

// The "already ran" markers of reflect and correct (`pr-N`, `YYYY-Www 7d`) are looked up by exact match, so they are
// stored as they are. They hold no free text (a number, an ISO week and a validated window), so there is nothing to scrub,
// and a profile scrub pattern that happened to match one would make the lookup miss and the run repeat.
// `generalize-hold` (`<id>: <fixed reason>`, a hold on the content) is counted per proposal by stage's retry cap, and
// `generalize-retry` (`<id>`, written by `sindri evolve retry`) restarts that count, so both are stored as they are too.
export function auditMarker(db: Ledger, deps: Deps, verb: "reflect" | "correct" | "generalize-hold" | "generalize-retry", key: string, epoch: number): void {
  db.prepare("INSERT INTO evolve_audit (ts, verb, actor, detail, epoch) VALUES (?, ?, ?, ?, ?)").run(deps.now().toISOString(), verb, deps.system.username(), key, epoch);
}
