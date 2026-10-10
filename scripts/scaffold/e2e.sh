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
# One heavy job at a time (config/lib/locks.sh): take the box-wide lock for the whole run unless a caller holds it,
# and tell the generators it is held.
LOCK=""
if [ "${AW_HEAVY_LOCK_HELD:-}" != "1" ]; then
  # shellcheck source=../../config/lib/locks.sh
  source "$ROOT/config/lib/locks.sh"
  LOCK="${AW_STATE_DIR:-$HOME/.agentic-workflow}/locks/heavy-job.lock"
  mkdir -p "$(dirname "$LOCK")"
  acquire_lock "$LOCK" 5400 || { echo "heavy-job lock busy: $LOCK" >&2; exit 2; }
  echo "$$ $(date -u +%FT%TZ) scripts/scaffold/e2e.sh" > "$LOCK/owner"
fi
export AW_HEAVY_LOCK_HELD=1
cleanup() {
  local dir
  for dir in "$TMP/small" "$TMP/http"; do
    if [ -d "$dir" ]; then git -C "$ROOT" worktree remove --force "$dir" >/dev/null 2>&1 || true; fi
  done
  rm -rf "$TMP"
  git -C "$ROOT" worktree prune
  if [ -n "$LOCK" ]; then rm -f "$LOCK/owner"; release_lock "$LOCK"; fi
}
trap cleanup EXIT

step() { printf '\n== %s\n' "$*"; }
fail() { printf 'FAIL: %s\n' "$*" >&2; exit 1; }

# tree <name> — a throwaway worktree of HEAD at $TMP/<name>; cleanup removes it.
tree() { git worktree add --quiet --detach "$TMP/$1" HEAD; }

# The first package deps.yaml declares (tier 1-3) that has no src/ directory yet, so the declared-node path
# (NP-DEPS-MISMATCH checks, a planned edge flipping to current) is exercised while unported packages remain. Once every
# declared package is ported, (a) falls back to an undeclared small probe (the node-adding path (b) also covers).
declared="$(node --input-type=module -e '
  import { parseYaml } from "./scripts/deps/yaml.mjs";
  import fs from "node:fs";
  const doc = parseYaml(fs.readFileSync("deps.yaml", "utf8"));
  const n = doc.packages.find((p) => p.kind === "package" && p.tier >= 1 && !p.test && !fs.existsSync(p.src) && /^src\/[A-Z][A-Za-z0-9]+$/.test(p.src));
  if (!n) process.exit(3);
  console.log(`${n.id} ${n.tier}`);
')" || declared=""
if [ -n "$declared" ]; then
  read -r SMALL SMALL_TIER <<<"$declared"
  SMALL_NOTE=()
  SMALL_KIND="declared in deps.yaml"
else
  SMALL="Scaffoldsmall"
  SMALL_TIER=1
  SMALL_NOTE=(--note "scaffold e2e probe")
  SMALL_KIND="no declared package left unported: an undeclared probe"
fi
HTTP="Scaffoldprobe"

gen() { env -u CI node "$@"; }

# ---- (a) small CLI package, declared ----
tree small
A="$TMP/small"
step "(a) /new-package: $SMALL, small, cli, tier $SMALL_TIER ($SMALL_KIND)"
(cd "$A" && gen scripts/scaffold/new-package.mjs --name "$SMALL" --tier "$SMALL_TIER" --error-prefix E2ESMALL ${SMALL_NOTE[@]+"${SMALL_NOTE[@]}"} \
  --shape small --host cli --first-slice CheckThing --behavior reports_nothing_for_an_empty_tree) || fail "(a) new-package"
(cd "$A" && git status --porcelain --untracked-files=all | grep -q "src/$SMALL/") || fail "(a) wrote no src/$SMALL"

step "(a) make CheckThing green by hand, then /new-slice ListThings with a typed id and a new port"
SVC="$A/src/$SMALL/Aw.$SMALL.App/Features/CheckThing/CheckThingService.cs"
RED="$A/tests/Aw.$SMALL.Tests/CheckThing/CheckThingTests.cs"
perl -0pi -e 's/=> throw new NotSupportedException\("CheckThing: make CheckThingTests pass"\);/=> ValueTask.FromResult<Result<CheckThingResponse>>(new CheckThingResponse());/' "$SVC"
perl -0pi -e 's/\n        Assert\.Fail\("red: assert reports_nothing_for_an_empty_tree"\);//' "$RED"
(cd "$A" && gen scripts/scaffold/new-slice.mjs --package "$SMALL" --slice ListThings --behavior lists_nothing_for_an_empty_tree \
  --surface cli --command list --ids ThingId:string --ports IThingStore) || fail "(a) new-slice"

step "(a) NativeAOT publish of the generated cli App (telemetry included): no trim or AOT warning, native run"
# PublishAot under a RID adds the host's ILCompiler packs to the lock files, which only this throwaway worktree sees.
rid="$(dotnet --info | awk '/^ *RID:/ {print $2; exit}')"
(cd "$A" && env -u CI dotnet publish "src/$SMALL/Aw.$SMALL.App/Aw.$SMALL.App.csproj" -c Release -r "$rid" \
  -p:RestoreLockedMode=false -o "$TMP/small-aot" >"$TMP/small-aot.log" 2>&1) || { tail -20 "$TMP/small-aot.log" >&2; fail "(a) AOT publish"; }
if grep -E 'IL[23][0-9]{3}' "$TMP/small-aot.log" >&2; then fail "(a) AOT publish raised trim or AOT warnings"; fi
bin="$TMP/small-aot/$(printf '%s' "$SMALL" | tr '[:upper:]' '[:lower:]')"
rc=0; AW_LOG_LEVEL=bogus "$bin" >/dev/null 2>"$TMP/small-aot.err" || rc=$?
[ "$rc" = 2 ] || fail "(a) native run with no command: exit $rc, expected 2 (usage)"
grep -q '"eventName":"AW-TELEMETRY-001"' "$TMP/small-aot.err" || fail "(a) native run: no AW-TELEMETRY-001 line for AW_LOG_LEVEL=bogus"

# ---- (b) full http package, not declared ----
tree http
B="$TMP/http"
step "(b) /new-package: $HTTP, full, http, tier 1 (not in deps.yaml)"
(cd "$B" && gen scripts/scaffold/new-package.mjs --name "$HTTP" --tier 1 --error-prefix E2EHTTP --note "scaffold e2e probe" \
  --shape full --host http --first-slice StartItem --behavior starts_an_item --route "POST /items/{itemId}/start" \
  --ids ItemId:string,OwnerId:Guid) || fail "(b) new-package"
step "(b) integration lane: loopback binding and id route binding"
(cd "$B" && env -u CI dotnet test --project "tests/Aw.$HTTP.Tests" --filter-trait "Lane=integration") || fail "(b) integration lane"
step "(b) check-deps with the new node"
(cd "$B" && node scripts/check-deps.mjs) || fail "(b) check-deps"

step "both packages scaffolded, built and tested; the worktrees are removed on exit"
