import type { Deps } from "../deps.js";
import type { Ledger } from "../ledger/db.js";
import { makeScrubber, type Scrubber } from "../scrub/scrub.js";

// Every privileged verb (reject, adopt, revert, publish, promote, rollback) leaves a row (spec §8.7).
export function audit(db: Ledger, deps: Deps, verb: string, detail: string, epoch: number, scrubber: Scrubber = makeScrubber()): void {
  db.prepare("INSERT INTO evolve_audit (ts, verb, actor, detail, epoch) VALUES (?, ?, ?, ?, ?)").run(
    deps.now().toISOString(), verb, deps.system.username(), scrubber.scrub(detail).text.slice(0, 500), epoch,
  );
}
