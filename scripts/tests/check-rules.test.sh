#!/usr/bin/env bash
# Tests for scripts/check-rules.mjs (the rule-record validator) using temp copies of a fixture repo.
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
CHECKER="$REPO/scripts/check-rules.mjs"
BASE="$HERE/fixtures/check-rules/base"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/check-rules-test.XXXXXX")"
trap 'rm -rf "$TMPROOT"' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

# fixture <name> — a fresh copy of the base fixture plus the real schema (so the two can't drift)
fixture() {
  local d="$TMPROOT/$1"
  cp -R "$BASE" "$d"
  mkdir -p "$d/schemas"
  cp "$REPO/schemas/rule-record.schema.json" "$d/schemas/"
  echo "$d"
}

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

rec() { echo "$1/rules/engineering/csharp/$2.yaml"; }
# sub <file> <from> <to> — replace one literal fragment on every line that has it (portable; no sed -i)
sub() { local tmp="$1.new"; awk -v f="$2" -v t="$3" '{ i = index($0, f); if (i) $0 = substr($0, 1, i - 1) t substr($0, i + length(f)); print }' "$1" > "$tmp" && mv "$tmp" "$1"; }
# drop <file> <pattern> — delete the lines matching an ERE
drop() { local tmp="$1.new"; grep -v -E "$2" "$1" > "$tmp" || true; mv "$tmp" "$1"; }

echo "check-rules"

d="$(fixture ok)"; run "$d"
expect "valid fixture passes with counts" 0 "rules: ok (5 records" "closed 4" "pending 1" "ARCH-SLICE-001 (PR #131)"

d="$(fixture json)"; run "$d" --json
if [ "$CODE" = 0 ] && printf '%s' "$OUT" | node -e 'const j=JSON.parse(require("fs").readFileSync(0,"utf8")); if (j.records!==5||j.failures.length!==0||j.gaps.pending!==1) process.exit(1)'; then ok "--json is machine-readable"; else fail "--json is machine-readable"; printf '%s\n' "$OUT" | sed 's/^/       | /'; fi

d="$(fixture no-why)"; drop "$(rec "$d" no-catch-exception)" '^why:'; run "$d"
expect "a record missing why fails the schema" 1 "RULE-SCHEMA-001" "no-catch-exception.yaml" "missing required property why" "fix:"

d="$(fixture bad-gap)"; sub "$(rec "$d" no-catch-exception)" "gap: closed" "gap: maybe"; run "$d"
expect "an unknown gap value fails the schema" 1 "RULE-SCHEMA-001" "/gap"

d="$(fixture bad-type)"; sub "$(rec "$d" no-catch-exception)" "type: analyzer" "type: vibes"; run "$d"
expect "an unknown check type fails the schema" 1 "RULE-SCHEMA-001" "/checks/0/type"

d="$(fixture no-pr)"; drop "$(rec "$d" slice-isolation)" 'pendingPr:'; run "$d"
expect "a pending check without its PR fails the schema" 1 "RULE-SCHEMA-001" "pendingPr"

d="$(fixture yaml)"; printf 'id: [unclosed\n' > "$(rec "$d" no-catch-exception)"; run "$d"
expect "a record outside the YAML subset fails" 1 "RULE-YAML-001" "no-catch-exception.yaml"

d="$(fixture yaml-line)"; printf 'schemaVersion: 1\nselect: {languages: [csharp]}\n' > "$(rec "$d" no-catch-exception)"; run "$d"
expect "a YAML error names the record's line, not deps.yaml" 1 "no-catch-exception.yaml: line 2:"
refuse "a YAML error names the record's line, not deps.yaml (no deps.yaml)" "deps.yaml:2"

d="$(fixture no-cascade)"; drop "$(rec "$d" no-catch-exception)" '^why:'; run "$d"
refuse "a record failing its schema doesn't cascade into RULE-COVER-002" "RULE-COVER-002"

d="$(fixture id)"; sub "$(rec "$d" no-catch-exception)" "id: engineering.csharp.no-catch-exception" "id: engineering.csharp.other"; run "$d"
expect "a record id must match its file name" 1 "RULE-ID-001" "engineering.csharp.other"

d="$(fixture phantom)"; sub "$(rec "$d" no-catch-exception)" "id: CA1031" "id: AW9999"; run "$d"
expect "an active check id that exists nowhere fails" 1 "RULE-CHECK-001" "AW9999" ".editorconfig"

d="$(fixture phantom-arch)"; f="$(rec "$d" slice-isolation)"
sub "$f" "status: pending" "status: active"; sub "$f" "gap: pending" "gap: closed"; drop "$f" 'pendingPr:'; run "$d"
expect "an ArchUnitNET id claimed active but absent fails" 1 "RULE-CHECK-001" "ARCH-SLICE-001" "tests/Aw.Architecture.Tests"

d="$(fixture banned)"; sub "$(rec "$d" core-no-io)" '"T:System.Console"' '"T:System.Console", "M:System.Guid.NewGuid"'; run "$d"
expect "a banned-symbols entry missing from BannedSymbols.txt fails" 1 "RULE-CHECK-001" "M:System.Guid.NewGuid" "BannedSymbols.txt"

d="$(fixture ci)"; sub "$(rec "$d" sdk-pinned)" '"The SDK is exactly the global.json pin"' '"A step nobody wrote"'; run "$d"
expect "a CI step that isn't in the workflow fails" 1 "RULE-CHECK-001" "A step nobody wrote" ".github/workflows/ci.yml"

d="$(fixture flipped)"; printf 'public const string Id = "ARCH-SLICE-001";\n' > "$d/tests/Aw.Architecture.Tests/SliceRules.cs"; run "$d"
expect "a pending check that now exists warns to flip it" 0 "WARN RULE-CHECK-002" "ARCH-SLICE-001" "status: active"

d="$(fixture gap-closed)"; sub "$(rec "$d" slice-isolation)" "gap: pending" "gap: closed"; run "$d"
expect "closed with no active deterministic check fails" 1 "RULE-GAP-001" "slice-isolation"

d="$(fixture gap-open)"; sub "$(rec "$d" no-catch-exception)" "gap: closed" "gap: open"; run "$d"
expect "open with an active check and no note fails" 1 "RULE-GAP-001" "no-catch-exception"

d="$(fixture missing)"; rm "$(rec "$d" no-package-cycles)"; run "$d"
expect "an inventory rule with no record fails" 1 "RULE-COVER-001" "engineering.csharp.no-package-cycles" "4b.1a"

d="$(fixture rules-doc)"; printf '| Method length | `MA0051` |\n' >> "$d/.agents/rules/csharp.md"; run "$d"
expect "a check id in the rules docs that no record names fails" 1 "RULE-COVER-002" "MA0051" ".agents/rules/csharp.md"

d="$(fixture orphan)"; cp "$(rec "$d" no-catch-exception)" "$(rec "$d" orphan)"
sub "$(rec "$d" orphan)" "id: engineering.csharp.no-catch-exception" "id: engineering.csharp.orphan"; run "$d"
expect "a record the inventory doesn't list fails" 1 "RULE-COVER-003" "engineering.csharp.orphan"

d="$(fixture section)"; sub "$d/rules/engineering/csharp.inventory.yaml" 'section: "4b.6"' 'section: "4b.42"'
sub "$(rec "$d" sdk-pinned)" 'section: "4b.6"' 'section: "4b.42"'; run "$d"
expect "an inventory section the spec doesn't have fails" 1 "RULE-COVER-004" "4b.42"

d="$(fixture section-mismatch)"; sub "$(rec "$d" sdk-pinned)" 'section: "4b.6"' 'section: "4b.3"'; run "$d"
expect "a record's section must match the inventory" 1 "RULE-SPEC-001" "sdk-pinned" "4b.6" "4b.3"

d="$(fixture norecords)"; mv "$d/rules" "$d/rules.gone"; run "$d"
expect "a tree with no rules directory is a tool error" 2 "check-rules: error"

echo "check-rules: $PASS passed, $FAIL failed"
[ "$FAIL" = 0 ]
