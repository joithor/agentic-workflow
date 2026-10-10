#!/usr/bin/env bash
# Tests for scripts/check-deps.mjs using temp copies of a fixture repo.
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
CHECKER="$HERE/../check-deps.mjs"
BASE="$HERE/fixtures/check-deps/base"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/check-deps-test.XXXXXX")"
trap 'rm -rf "$TMPROOT"' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

# fixture <name> — a fresh copy of the base fixture repo; prints its path
fixture() { local d="$TMPROOT/$1"; cp -R "$BASE" "$d"; echo "$d"; }

# run <dir> [args...] — sets OUT (stdout+stderr) and CODE
run() { local d="$1"; shift; set +e; OUT="$(node "$CHECKER" --root "$d" "$@" 2>&1)"; CODE=$?; set -e; }

expect() { # expect <name> <code> <substring>...
  local name="$1" code="$2"; shift 2
  local good=1 s
  [ "$CODE" = "$code" ] || good=0
  for s in "$@"; do case "$OUT" in *"$s"*) ;; *) good=0 ;; esac; done
  if [ "$good" = 1 ]; then ok "$name"; else fail "$name (exit $CODE, want $code)"; printf '%s\n' "$OUT" | sed 's/^/       | /'; fi
}

refuse() { # refuse <name> <substring> — OUT must not contain it
  case "$OUT" in *"$2"*) fail "$1"; printf '%s\n' "$OUT" | sed 's/^/       | /' ;; *) ok "$1" ;; esac
}

edge() { # edge <dir> <from> <to> <kind> <status> — append an edge
  local tmp="$1/deps.yaml.new"
  awk -v f="$2" -v t="$3" -v k="$4" -v s="$5" '
    /^exceptions:/ && !done { printf "  - from: %s\n    to: %s\n    kind: %s\n    status: %s\n    note: added by test\n", f, t, k, s; done=1 }
    { print }' "$1/deps.yaml" > "$tmp" && mv "$tmp" "$1/deps.yaml"
}

csproj() { # csproj <dir> <relpath> [ref-relpath...]
  local d="$1" p="$2"; shift 2
  mkdir -p "$d/$(dirname "$p")"
  { echo '<Project Sdk="Microsoft.NET.Sdk">'; echo '  <ItemGroup>'
    for r in "$@"; do echo "    <ProjectReference Include=\"$r\" />"; done
    echo '  </ItemGroup>'; echo '</Project>'; } > "$d/$p"
}

echo "check-deps.mjs"

d="$(fixture clean)"; run "$d"
expect "clean fixture passes" 0 "deps: ok" "no C# projects yet — skipped, ok"
refuse "test files and comments are not scanned" "Delta"

d="$(fixture undeclared-cli)"
printf 'import { execFile } from "node:child_process";\nexecFile("beta", ["x"]);\n' > "$d/alpha/src/call.ts"
run "$d"
expect "undeclared cli edge fails with rule, measured value, limit and fix" 1 \
  "DEPS-EDGE-001" "Alpha -> Beta (cli)" "alpha/src/call.ts:2" "limit:" "fix:"

d="$(fixture undeclared-shell)"
printf '#!/usr/bin/env bash\nbeta report --json\n' > "$d/hooks/other.sh"
run "$d"
expect "undeclared cli edge from a shell script fails" 1 "DEPS-EDGE-001" "Hooks -> Beta (cli)" "hooks/other.sh:2"

d="$(fixture bin-helper)"
printf '#!/usr/bin/env bash\nbin="$(find_bin beta)" || exit 0\n' > "$d/hooks/status.sh"
run "$d"
expect "a *_bin helper naming another package's binary is a cli edge" 1 "DEPS-EDGE-001" "Hooks -> Beta (cli)" "hooks/status.sh:2"

d="$(fixture scan-file)"
perl -pi -e 's/^    scan: \[hooks\]\n/    scan: [hooks, statusline.sh]\n/' "$d/deps.yaml"
printf '#!/usr/bin/env bash\nbeta live --json\n' > "$d/statusline.sh"
run "$d"
expect "a single file in scan is scanned" 1 "DEPS-EDGE-001" "Hooks -> Beta (cli)" "statusline.sh:2"

d="$(fixture undeclared-file)"
printf 'const o = path.join(stateDir, "alpha", "outbox");\n' > "$d/delta/src/read.ts"
run "$d"
expect "undeclared file edge fails" 1 "DEPS-EDGE-001" "Delta -> Alpha (file)" "delta/src/read.ts:1"

d="$(fixture default-bin)"
printf 'export function realDeps(alphaBin = "alpha") { return alphaBin; }\n' > "$d/delta/src/deps.ts"
run "$d"
expect "a *Bin default naming another package's binary is a cli edge" 1 "DEPS-EDGE-001" "Delta -> Alpha (cli)"

d="$(fixture stale)"
printf 'export const dbPath = (s: string) => path.join(s, "alpha", "a.sqlite");\n' > "$d/beta/src/run.ts"
run "$d"
expect "declared current edge that the scan no longer finds is stale" 1 "DEPS-STALE-001" "Beta -> Alpha (cli)" "status: planned"

d="$(fixture cycle)"; edge "$d" Alpha Beta cli planned; run "$d"
expect "cycle fails with its path" 1 "DEPS-CYCLE-001" "Alpha -> Beta -> Alpha"

d="$(fixture same-tier)"; edge "$d" Beta Delta project planned; run "$d"
expect "same-tier project edge fails" 1 "DEPS-TIER-001" "Beta (tier 2) -> Delta (tier 2)"

d="$(fixture upward-db)"; edge "$d" Kernel Alpha db planned; run "$d"
expect "db edge to a higher tier fails" 1 "DEPS-TIER-002" "Kernel (tier 0) -> Alpha (tier 1)"

d="$(fixture same-tier-cli)"; edge "$d" Delta Beta cli planned; run "$d"
expect "same-tier cli edge is allowed (a contract, §4b.1a)" 0 "deps: ok"

d="$(fixture project-into-shell)"; edge "$d" Beta Hooks project planned; run "$d"
expect "project edge into a shell node fails" 1 "DEPS-KIND-001" "Beta -> Hooks (project)"

d="$(fixture file-into-shell)"; edge "$d" Beta Hooks file planned; run "$d"
expect "any edge into a shell node fails" 1 "DEPS-KIND-002" "Beta -> Hooks (file)"

d="$(fixture unknown)"; edge "$d" Beta Zeta cli planned; run "$d"
expect "unknown package fails" 1 "DEPS-REF-001" "Zeta"

d="$(fixture schema)"; perl -pi -e 's/^    tier: 2\n/    tier: 7\n/' "$d/deps.yaml"; run "$d"
expect "schema violation fails with its path" 1 "DEPS-SCHEMA-001" "/packages/2/tier"

d="$(fixture schema-required)"; perl -ni -e 'print unless /projectPrefix: Aw.Delta/' "$d/deps.yaml"; run "$d"
expect "package kind requires projectPrefix (if/then)" 1 "DEPS-SCHEMA-001" "/packages/3" "projectPrefix"

d="$(fixture yaml)"; perl -pi -e 's/^  - id: Alpha\n/  - &a id: Alpha\n/' "$d/deps.yaml"; run "$d"
expect "YAML outside the subset fails" 1 "DEPS-YAML-001" "anchor"

d="$(fixture yaml-dup)"; perl -pi -e 's/^    note: delta\n/    note: delta\n    note: again\n/' "$d/deps.yaml"; run "$d"
expect "duplicate key fails" 1 "DEPS-YAML-001" "duplicate key"

d="$(fixture fanout)"; perl -pi -e 's/projectFanOut: 4/projectFanOut: 1/' "$d/deps.yaml"; edge "$d" Beta Alpha project planned; run "$d"
expect "fan-out over the limit fails" 1 "DEPS-FANOUT-001" "Beta has 2 project edges" "limit: 1"

d="$(fixture fanout-exc)"; perl -pi -e 's/projectFanOut: 4/projectFanOut: 1/' "$d/deps.yaml"; edge "$d" Beta Alpha project planned
perl -pi -e 's/^exceptions: \[\]\n/exceptions:\n  - rule: DEPS-FANOUT-001\n    subject: Beta\n    reason: test\n    recorded: 2026-10-10\n    expires: 2099-01-01\n/' "$d/deps.yaml"; run "$d"
expect "an unexpired exception silences fan-out" 0 "deps: ok" "exception DEPS-FANOUT-001 Beta (expires 2099-01-01)"

d="$(fixture fanout-expired)"; perl -pi -e 's/projectFanOut: 4/projectFanOut: 1/' "$d/deps.yaml"; edge "$d" Beta Alpha project planned
perl -pi -e 's/^exceptions: \[\]\n/exceptions:\n  - rule: DEPS-FANOUT-001\n    subject: Beta\n    reason: test\n    recorded: 2020-01-01\n    expires: 2020-06-01\n/' "$d/deps.yaml"; run "$d"
expect "an expired exception fails and no longer silences" 1 "DEPS-EXC-001" "DEPS-FANOUT-001"

d="$(fixture proj-undeclared)"
csproj "$d" src/Beta/Aw.Beta.Core/Aw.Beta.Core.csproj ../../Delta/Aw.Delta.Core/Aw.Delta.Core.csproj
csproj "$d" src/Delta/Aw.Delta.Core/Aw.Delta.Core.csproj
run "$d"
expect "undeclared ProjectReference fails once src/ has projects" 1 "DEPS-PROJ-001" "Aw.Beta.Core -> Aw.Delta.Core"

d="$(fixture proj-declared)"
csproj "$d" src/Beta/Aw.Beta.Core/Aw.Beta.Core.csproj ../../Aw.Kernel/Aw.Kernel.csproj
csproj "$d" src/Aw.Kernel/Aw.Kernel.csproj
csproj "$d" src/Beta/Aw.Beta.Host/Aw.Beta.Host.csproj ../Aw.Beta.Core/Aw.Beta.Core.csproj
run "$d"
expect "declared and same-package ProjectReferences pass" 0 "deps: ok" "checked 3 C# projects"

d="$(fixture proj-unowned)"; csproj "$d" src/Other/Aw.Other.csproj; run "$d"
expect "a C# project no package owns fails" 1 "DEPS-PROJ-002" "Aw.Other"

d="$(fixture mermaid)"; run "$d" --mermaid
expect "--mermaid prints a graph and the edge list" 0 '```mermaid' "flowchart TB" "Beta -->|project| Kernel" "Beta -.->|cli| Alpha" "edges (5):"

d="$(fixture mermaid-diff)"; cp "$d/deps.yaml" "$TMPROOT/base-deps.yaml"; edge "$d" Delta Beta cli planned; run "$d" --mermaid --base-file "$TMPROOT/base-deps.yaml"
expect "--base-file lists added and removed edges" 0 "Added edges (1):" "+ Delta -> Beta (cli, planned)" "Removed edges (0):"

d="$TMPROOT/empty"; mkdir -p "$d"; run "$d"
expect "missing deps.yaml is a tool error (exit 2)" 2 "deps.yaml not found"

run "$BASE" --bogus
expect "unknown flag is a tool error (exit 2)" 2 "unknown argument"

echo
echo "check-deps: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
