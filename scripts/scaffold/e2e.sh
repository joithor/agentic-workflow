#!/usr/bin/env bash
# scripts/scaffold/e2e.sh — the generators' end-to-end test (docs/csharp/scaffolds/new-package.md, "Done criteria"):
# scaffolds (a) a small CLI package declared in deps.yaml and (b) a full http package that isn't, each in its own
# temporary git worktree of HEAD, and lets the generator run its own checks against the real root files
# (restores, RS0016, build -warnaserror, the unit lane with exactly one red test, format, check-deps). Then, by hand:
# (a) makes its first slice green and adds a second slice with /new-slice (exactly one new red test); (b) runs its
# integration lane (loopback binding, id route binding) green. The worktrees are removed afterwards.
#
# Usage: scripts/scaffold/e2e.sh            (from the repo root; needs the .NET SDK from global.json and node)
# Exit:  0 both packages behaved; 1 a check failed (its output is printed); 2 usage or setup error.
#
# The generators restore new projects, which have no lock file yet: under CI=true (RestoreLockedMode, the AW0001
# lock-file guard) that is refused by design, so the generator steps run with CI unset. CI's own locked restore
# and package checks cover the committed tree, not these throwaway packages.
set -euo pipefail

ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT"
TMP="$(mktemp -d "${TMPDIR:-/tmp}/aw-scaffold-e2e.XXXXXX")"
TREES=()
cleanup() {
  for tree in "${TREES[@]}"; do git -C "$ROOT" worktree remove --force "$tree" >/dev/null 2>&1 || true; done
  rm -rf "$TMP"
}
trap cleanup EXIT

step() { printf '\n== %s\n' "$*"; }
fail() { printf 'FAIL: %s\n' "$*" >&2; exit 1; }

tree() {
  local dir="$TMP/$1"
  git worktree add --quiet --detach "$dir" HEAD
  TREES+=("$dir")
  echo "$dir"
}

# The first package deps.yaml declares (tier 1-3) that has no src/ directory yet, so the declared-node path
# (NP-DEPS-MISMATCH checks, a planned edge flipping to current) is exercised whichever packages have been ported.
declared="$(node --input-type=module -e '
  import { parseYaml } from "./scripts/deps/yaml.mjs";
  import fs from "node:fs";
  const doc = parseYaml(fs.readFileSync("deps.yaml", "utf8"));
  const n = doc.packages.find((p) => p.kind === "package" && p.tier >= 1 && !p.test && !fs.existsSync(p.src) && /^src\/[A-Z][A-Za-z0-9]+$/.test(p.src));
  if (!n) process.exit(3);
  console.log(`${n.id} ${n.tier}`);
')" || { echo "no declared package left to scaffold" >&2; exit 2; }
read -r SMALL SMALL_TIER <<<"$declared"
HTTP="Scaffoldprobe"

gen() { env -u CI AW_HEAVY_LOCK_HELD="${AW_HEAVY_LOCK_HELD:-}" node "$@"; }

# ---- (a) small CLI package, declared ----
A="$(tree small)"
step "(a) /new-package: $SMALL, small, cli, tier $SMALL_TIER (declared in deps.yaml)"
(cd "$A" && gen scripts/scaffold/new-package.mjs --name "$SMALL" --tier "$SMALL_TIER" --error-prefix E2ESMALL \
  --shape small --host cli --first-slice CheckThing --behavior reports_nothing_for_an_empty_tree) || fail "(a) new-package"
(cd "$A" && git status --porcelain --untracked-files=all | grep -q "src/$SMALL/") || fail "(a) wrote no src/$SMALL"

step "(a) make CheckThing green by hand, then /new-slice ListThings with a typed id"
SVC="$A/src/$SMALL/Aw.$SMALL.App/Features/CheckThing/CheckThingService.cs"
RED="$A/tests/Aw.$SMALL.Tests/CheckThing/CheckThingTests.cs"
perl -0pi -e 's/=> throw new NotSupportedException\("CheckThing: make CheckThingTests pass"\);/=> ValueTask.FromResult<Result<CheckThingResponse>>(new CheckThingResponse());/' "$SVC"
perl -0pi -e 's/\n        Assert\.Fail\("red: assert reports_nothing_for_an_empty_tree"\);//' "$RED"
(cd "$A" && gen scripts/scaffold/new-slice.mjs --package "$SMALL" --slice ListThings --behavior lists_nothing_for_an_empty_tree \
  --surface cli --command list --ids ThingId:string) || fail "(a) new-slice"

# ---- (b) full http package, not declared ----
B="$(tree http)"
step "(b) /new-package: $HTTP, full, http, tier 1 (not in deps.yaml)"
(cd "$B" && gen scripts/scaffold/new-package.mjs --name "$HTTP" --tier 1 --error-prefix E2EHTTP --note "scaffold e2e probe" \
  --shape full --host http --first-slice StartItem --behavior starts_an_item --route "POST /items/{itemId}/start" \
  --ids ItemId:string,OwnerId:Guid) || fail "(b) new-package"
step "(b) integration lane: loopback binding and id route binding"
(cd "$B" && env -u CI dotnet test --project "tests/Aw.$HTTP.Tests" --filter-trait "Lane=integration") || fail "(b) integration lane"
step "(b) check-deps with the new node"
(cd "$B" && node scripts/check-deps.mjs) || fail "(b) check-deps"

step "both packages scaffolded, built, tested and removed"
