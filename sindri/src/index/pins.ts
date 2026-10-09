import { createRequire } from "node:module";

// graphifyy release installed by `sindri index setup` and expected by `doctor`, and its PyPI
// upload time (the `--exclude-newer` cutoff, which also age-gates its transitive dependencies).
// Written by Task 7 Step 1 into the `sindri` key of package.json. Upgrades go through a
// pack-upgrade proposal (spec §7.4, §16 pin policy).
const pkg = createRequire(import.meta.url)("../../package.json") as { sindri: { graphifyPin: string; graphifyPinDate: string } };

export const GRAPHIFY_PIN: string = pkg.sindri.graphifyPin;
export const GRAPHIFY_PIN_DATE: string = pkg.sindri.graphifyPinDate;
