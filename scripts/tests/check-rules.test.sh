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

# addcheck <record> <line>... — insert check lines right after "checks:"
addcheck() {
  local f="$1"; shift
  printf '%s\n' "$@" > "$f.add"
  awk -v addf="$f.add" '{ print } /^checks:$/ { while ((getline l < addf) > 0) print l }' "$f" > "$f.new" && mv "$f.new" "$f"
}

rec() { echo "$1/rules/engineering/csharp/$2.yaml"; }
# sub <file> <from> <to> — replace one literal fragment on every line that has it (portable; no sed -i)
sub() { local tmp="$1.new"; awk -v f="$2" -v t="$3" '{ i = index($0, f); if (i) $0 = substr($0, 1, i - 1) t substr($0, i + length(f)); print }' "$1" > "$tmp" && mv "$tmp" "$1"; }
# drop <file> <pattern> — delete the lines matching an ERE
drop() { local tmp="$1.new"; grep -v -E "$2" "$1" > "$tmp" || true; mv "$tmp" "$1"; }

echo "check-rules"

d="$(fixture ok)"; run "$d"
expect "valid fixture passes with counts" 0 "rules: ok (6 records" "closed 5" "pending 1" "ARCH-SLICE-001 (PR #131)"

d="$(fixture json)"; run "$d" --json
if [ "$CODE" = 0 ] && printf '%s' "$OUT" | node -e 'const j=JSON.parse(require("fs").readFileSync(0,"utf8")); if (j.records!==6||j.failures.length!==0||j.gaps.pending!==1) process.exit(1)'; then ok "--json is machine-readable"; else fail "--json is machine-readable"; printf '%s\n' "$OUT" | sed 's/^/       | /'; fi

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
expect "a record failing its schema reports RULE-SCHEMA-001" 1 "RULE-SCHEMA-001"
refuse "a record failing its schema doesn't cascade into RULE-COVER-002" "RULE-COVER-002"
refuse "a record failing its schema doesn't cascade into RULE-COVER-001" "RULE-COVER-001"

d="$(fixture id)"; sub "$(rec "$d" no-catch-exception)" "id: engineering.csharp.no-catch-exception" "id: engineering.csharp.other"; run "$d"
expect "a record id must match its file name" 1 "RULE-ID-001" "engineering.csharp.other"

d="$(fixture phantom)"; sub "$(rec "$d" no-catch-exception)" "id: CA1031" "id: AW9999"; run "$d"
expect "an active check id that exists nowhere fails" 1 "RULE-CHECK-001" "AW9999" "src/Aw.Analyzers"

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

# ---- existence is a token in live config, not a substring or a comment ----
d="$(fixture substring)"; sub "$(rec "$d" no-catch-exception)" 'id: CA1031' 'id: "CA103"'; run "$d"
expect "an id found only inside a longer id fails" 1 "RULE-CHECK-001" "CA103 "

d="$(fixture comment-only)"; printf '# CA2000 is not configured yet\n' >> "$d/.editorconfig"
sub "$(rec "$d" no-catch-exception)" 'id: CA1031' 'id: "CA2000"'; printf '| x | `CA2000` |\n' >> "$d/.agents/rules/csharp.md"; run "$d"
expect "an id found only in a comment fails" 1 "RULE-CHECK-001" "CA2000"

d="$(fixture off)"; sub "$d/.editorconfig" "dotnet_diagnostic.CA1031.severity = error" "dotnet_diagnostic.CA1031.severity = none"; run "$d"
expect "an analyzer switched off in the root .editorconfig fails" 1 "RULE-CHECK-001" "CA1031" "severity none"

d="$(fixture baseline-off)"; printf 'dotnet_diagnostic.RS0016.severity = none\n' >> "$d/.editorconfig"; run "$d"
expect "a baseline analyzer switched off in the root .editorconfig fails" 1 "RULE-CHECK-001" "RS0016" "severity none"

d="$(fixture baseline-nopkg)"; printf '<Project />\n' > "$d/Directory.Packages.props"; run "$d"
expect "a baseline analyzer whose package isn't referenced fails" 1 "RULE-CHECK-001" "RS0016" "Microsoft.CodeAnalysis.PublicApiAnalyzers"

d="$(fixture baseline-nobulk)"; drop "$d/.editorconfig" '^dotnet_analyzer_diagnostic\.severity'; run "$d"
expect "a baseline analyzer without the bulk error line fails" 1 "RULE-CHECK-001" "RS0016" "bulk"

d="$(fixture aw-nosource)"; printf 'dotnet_diagnostic.AW1005.severity = error\n' >> "$d/.editorconfig"
sub "$(rec "$d" no-catch-exception)" 'id: CA1031' 'id: "AW1005"'; printf '| x | `CA1031` `AW1005` |\n' >> "$d/.agents/rules/csharp.md"; run "$d"
expect "an AW id with a severity but no descriptor in src/Aw.Analyzers fails" 1 "RULE-CHECK-001" "AW1005" "src/Aw.Analyzers"

d="$(fixture ci-substring)"; sub "$(rec "$d" sdk-pinned)" '"The SDK is exactly the global.json pin"' '"The SDK is exactly"'; run "$d"
expect "a CI step name must match a step exactly" 1 "RULE-CHECK-001" "The SDK is exactly"

# ---- other check types ----
d="$(fixture test-ok)"; mkdir -p "$d/tests/Aw.X.Tests"; printf 'public sealed class SampleTests { }\n' > "$d/tests/Aw.X.Tests/SampleTests.cs"
f="$(rec "$d" no-catch-exception)"; addcheck "$f" "  - type: test" '    id: "SampleTests"' "    ref: tests/Aw.X.Tests/SampleTests.cs" "    status: active"; run "$d"
expect "a test check with its file and class passes" 0 "rules: ok"
sub "$f" 'id: "SampleTests"' 'id: "OtherTests"'; run "$d"
expect "a test check whose file lacks the name fails" 1 "RULE-CHECK-001" "OtherTests"
sub "$f" "ref: tests/Aw.X.Tests/SampleTests.cs" "ref: tests/Aw.X.Tests/Gone.cs"; run "$d"
expect "a test check whose file is missing fails" 1 "RULE-CHECK-001" "Gone.cs"

d="$(fixture script-missing)"; f="$(rec "$d" no-catch-exception)"
awk '{print} /^checks:$/ {print "  - type: script\n    id: \"verify\"\n    ref: scripts/verify.sh\n    status: active"}' "$f" > "$f.new" && mv "$f.new" "$f"; run "$d"
expect "a script check whose file is missing fails" 1 "RULE-CHECK-001" "scripts/verify.sh"

d="$(fixture compiler)"; f="$(rec "$d" no-catch-exception)"
awk '{print} /^checks:$/ {print "  - type: compiler\n    id: \"XX1234\"\n    status: active"}' "$f" > "$f.new" && mv "$f.new" "$f"; run "$d"
expect "a compiler check must be a CS or IL id" 1 "RULE-CHECK-001" "XX1234"

d="$(fixture banned-noentries)"; drop "$(rec "$d" core-no-io)" 'entries:'; run "$d"
expect "a banned-symbols check needs its entries" 1 "RULE-SCHEMA-001" "entries"

d="$(fixture banned-unwired)"; printf '<Project />\n' > "$d/Directory.Build.props"; run "$d"
expect "BannedSymbols.txt that no build file wires fails" 1 "RULE-CHECK-001" "RS0030" "Directory.Build.props"

d="$(fixture banned-ref)"; f="$(rec "$d" core-no-io)"
printf 'M:System.Guid.NewGuid;no\n' > "$d/BannedSymbols.All.txt"
sub "$f" 'entries: ["T:System.Console"]' 'entries: ["M:System.Guid.NewGuid"]'
awk '{print} /entries: \["M:System.Guid.NewGuid"\]/ {print "    ref: \"BannedSymbols.All.txt\""}' "$f" > "$f.new" && mv "$f.new" "$f"; run "$d"
expect "a banned-symbols check with a ref the build files don't wire fails" 1 "RULE-CHECK-001" "BannedSymbols.All.txt" "no AdditionalFiles item wires"
printf '<Project>\n  <ItemGroup><AdditionalFiles Include="BannedSymbols.txt" /></ItemGroup>\n  <ItemGroup><AdditionalFiles Include="$(MSBuildThisFileDirectory)BannedSymbols.All.txt" /></ItemGroup>\n</Project>\n' > "$d/Directory.Build.props"; run "$d"
expect "a banned-symbols check with a wired ref file and listed entries passes" 0 "rules: ok"

# ---- gap consistency ----
d="$(fixture partial-nonote)"; sub "$(rec "$d" no-catch-exception)" "gap: closed" "gap: partial"; run "$d"
expect "partial without a gapNote fails" 1 "RULE-GAP-001" "no-catch-exception"

d="$(fixture pending-none)"; sub "$(rec "$d" no-catch-exception)" "gap: closed" "gap: pending"; run "$d"
expect "pending without a pending check fails" 1 "RULE-GAP-001" "no-catch-exception"

d="$(fixture open-pending)"; f="$(rec "$d" slice-isolation)"; sub "$f" "gap: pending" "gap: open"
printf 'gapNote: "followed by hand until the check lands"\n' >> "$f"; run "$d"
expect "open with a pending check fails" 1 "RULE-GAP-001" "slice-isolation"

d="$(fixture closed-pending)"; f="$(rec "$d" no-catch-exception)"
awk '{print} /^checks:$/ {print "  - type: archunit\n    id: \"ARCH-X-001\"\n    status: pending\n    pendingPr: 131"}' "$f" > "$f.new" && mv "$f.new" "$f"; run "$d"
expect "closed with a pending check fails (it's partial until the check lands)" 1 "RULE-GAP-001" "no-catch-exception"

# ---- ids, coverage, inventory ----
d="$(fixture kind)"; sub "$(rec "$d" no-catch-exception)" "kind: engineering" "kind: process"; run "$d"
expect "a record id must start with its kind" 1 "RULE-ID-001" "process.csharp.no-catch-exception"

d="$(fixture related)"; printf '| x | `MA0004` |\n' >> "$d/.agents/rules/csharp.md"
printf 'relatedIds: ["MA0004"]\n' >> "$(rec "$d" no-catch-exception)"; run "$d"
expect "a relatedId satisfies the rules-doc coverage" 0 "rules: ok"

d="$(fixture testing-doc)"; printf '# Testing\n| x | `MA0040` |\n' > "$d/.agents/rules/csharp-testing.md"; run "$d"
expect "ids in csharp-testing.md need a record too" 1 "RULE-COVER-002" "MA0040" "csharp-testing.md"

d="$(fixture cs-id)"; printf '| Unread primary constructor parameter | `CS9113` `xUnit1030` `NU1903` |\n' >> "$d/.agents/rules/csharp.md"; run "$d"
expect "CS, xUnit and NU ids in the rules docs need a record" 1 "RULE-COVER-002" "CS9113" "xUnit1030" "NU1903"

d="$(fixture inv-yaml)"; printf 'version: 1\nrecords: {}\n' > "$d/rules/engineering/csharp.inventory.yaml"; run "$d"
expect "an inventory outside the YAML subset fails" 1 "RULE-YAML-001" "csharp.inventory.yaml"

d="$(fixture inv-nospec)"; drop "$d/rules/engineering/csharp.inventory.yaml" '^spec:'; run "$d"
expect "an inventory without its spec fails with a rule, not a stack" 1 "RULE-INV-001" "spec"

d="$(fixture inv-dup)"; printf '  - id: engineering.csharp.sdk-pinned\n    section: "4b.3"\n' >> "$d/rules/engineering/csharp.inventory.yaml"; run "$d"
expect "a duplicate inventory id fails" 1 "RULE-COVER-005" "engineering.csharp.sdk-pinned"

# ---- modes and arguments ----
d="$(fixture json-fail)"; sub "$(rec "$d" no-catch-exception)" 'id: CA1031' 'id: "AW9999"'; run "$d" --json
if [ "$CODE" = 1 ] && printf '%s' "$OUT" | node -e 'const j=JSON.parse(require("fs").readFileSync(0,"utf8")); if (!j.failures.some(f=>f.rule==="RULE-CHECK-001")) process.exit(1)'; then ok "--json reports failures with exit 1"; else fail "--json reports failures with exit 1"; printf '%s\n' "$OUT" | sed 's/^/       | /'; fi

d="$(fixture strict)"; printf 'public const string Id = "ARCH-SLICE-001";\n' > "$d/tests/Aw.Architecture.Tests/SliceRules.cs"; run "$d" --strict
expect "--strict fails on a pending check that now exists" 1 "RULE-CHECK-002" "ARCH-SLICE-001"

d="$(fixture noroot)"; run "$d" --root
expect "--root without a value is a tool error" 2 "check-rules: error" "--root"

d="$(fixture badarg)"; run "$d" --bogus
expect "an unknown argument is a tool error" 2 "check-rules: error" "--bogus"

# ---- review run 2: editorconfig sections, case, categories; suggestion; packages; CI steps; strings ----
d="$(fixture section-only)"; drop "$d/.editorconfig" 'CA1031'; printf '[tests/**.cs]\ndotnet_diagnostic.CA1031.severity = error\n' >> "$d/.editorconfig"; run "$d"
expect "a severity set only under a narrower section doesn't count" 1 "RULE-CHECK-001" "CA1031"

d="$(fixture narrow-off)"; printf '[src/Legacy/**.cs]\ndotnet_diagnostic.CA1031.severity = none\n' >> "$d/.editorconfig"; run "$d"
expect "a rule switched off under a narrower section doesn't fail the check" 0 "rules: ok"

d="$(fixture lowercase)"; printf 'dotnet_diagnostic.ca1031.severity = none\n' >> "$d/.editorconfig"; run "$d"
expect "editorconfig keys match case-insensitively (a later lowercase none wins)" 1 "RULE-CHECK-001" "CA1031" "severity none"

d="$(fixture category-off)"; printf 'dotnet_analyzer_diagnostic.category-ApiDesign.severity = none\n' >> "$d/.editorconfig"; run "$d"
expect "a baseline check whose category is switched off fails" 1 "RULE-CHECK-001" "RS0016" "category-ApiDesign"

d="$(fixture category-missing)"; drop "$(rec "$d" public-api-tracked)" 'category:'; run "$d"
expect "a baseline check names its category" 1 "RULE-SCHEMA-001" "category"

d="$(fixture suggestion)"; sub "$d/.editorconfig" "dotnet_diagnostic.CA1031.severity = error" "dotnet_diagnostic.CA1031.severity = suggestion"
f="$(rec "$d" no-catch-exception)"; awk '{print} /^    id: CA1031$/ {print "    severity: suggestion"}' "$f" > "$f.new" && mv "$f.new" "$f"; run "$d"
expect "a suggestion-severity check can't close a rule" 1 "RULE-GAP-001" "no-catch-exception"

d="$(fixture version-only)"; printf '<Project>\n  <ItemGroup>\n    <PackageVersion Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" Version="[5.6.0]" />\n  </ItemGroup>\n</Project>\n' > "$d/Directory.Packages.props"; run "$d"
expect "a version-only PackageVersion isn't a referenced analyzer" 1 "RULE-CHECK-001" "RS0016" "GlobalPackageReference"

d="$(fixture job-name)"; printf 'name: CI\njobs:\n  dotnet:\n    name: The SDK is exactly the global.json pin\n    steps:\n      - name: Something else\n' > "$d/.github/workflows/ci.yml"; run "$d"
expect "a job name isn't a step" 1 "RULE-CHECK-001" "The SDK is exactly the global.json pin"

d="$(fixture quoted-hash)"; printf 'jobs:\n  dotnet:\n    steps:\n      - name: "Lane #2 caps"\n' > "$d/.github/workflows/ci.yml"
sub "$(rec "$d" sdk-pinned)" '"The SDK is exactly the global.json pin"' '"Lane #2 caps"'; run "$d"
expect "a # inside a quoted step name isn't a comment" 0 "rules: ok"

d="$(fixture strings)"; printf 'const GLOB = "src/*.cs";\nconst SEP = "a//b"; fail("DEPS-CYCLE-001");\n/** done */\n' > "$d/scripts/check-deps.mjs"; run "$d"
expect "comment markers inside strings don't hide ids" 0 "rules: ok"

d="$(fixture rcs)"; printf '| x | `RCS1075` `SYSLIB1045` |\n' >> "$d/.agents/rules/csharp.md"; run "$d"
expect "RCS and SYSLIB ids in the rules docs need a record" 1 "RULE-COVER-002" "RCS1075" "SYSLIB1045"

d="$(fixture suggestion-flip)"; mkdir -p "$d/src/Aw.Analyzers"; printf 'const string Id = "AW1008";\n' > "$d/src/Aw.Analyzers/Ids.cs"
printf 'dotnet_diagnostic.AW1008.severity = suggestion\n' >> "$d/.editorconfig"
addcheck "$(rec "$d" no-catch-exception)" "  - type: analyzer" '    id: "AW1008"' "    severity: suggestion" "    status: pending" "    pendingPr: 132"; run "$d"
expect "a pending suggestion-level check that now exists still warns to flip it" 0 "WARN RULE-CHECK-002" "AW1008"

# ---- review run 3: lexers, build settings in editorconfig and nuget.config, step lists, section globs ----
d="$(fixture regex-id)"; printf 'if (/^#\\//.test(l)) fail("DEPS-CYCLE-001");\n' > "$d/scripts/check-deps.mjs"; run "$d"
expect "an id after a regex literal on the same line is live" 0 "rules: ok"

d="$(fixture regex-quote)"; printf 'const q = /["]/; // DEPS-CYCLE-001\n' > "$d/scripts/check-deps.mjs"; run "$d"
expect "a quote inside a regex literal doesn't turn a comment into code" 1 "RULE-CHECK-001" "DEPS-CYCLE-001"

d="$(fixture cs-raw)"; printf 'const string G = """\n  glob: src/*\n  """;\nconst string Id = "ARCH-SLICE-001";\n' > "$d/tests/Aw.Architecture.Tests/Raw.cs"; run "$d"
expect "a C# raw string literal doesn't open a comment" 0 "WARN RULE-CHECK-002" "ARCH-SLICE-001"

d="$(fixture cs-verbatim-interp)"; printf 'const string P = $@"C:\\dir\\"; // ARCH-SLICE-001\n' > "$d/tests/Aw.Architecture.Tests/Path.cs"; run "$d"
refuse "a comment after a C# \$@ verbatim string stays a comment" "ARCH-SLICE-001 (PR #131) — fix"

d="$(fixture setting-override)"; addcheck "$(rec "$d" no-catch-exception)" "  - type: build-setting" '    id: "dotnet_analyzer_diagnostic.severity = error"' "    status: active"
printf '[*.cs]\ndotnet_analyzer_diagnostic.severity = none\n' >> "$d/.editorconfig"; run "$d"
expect "an editorconfig build setting overridden later in a general section fails" 1 "RULE-CHECK-001" "dotnet_analyzer_diagnostic.severity = error"

d="$(fixture setting-narrow)"; addcheck "$(rec "$d" no-catch-exception)" "  - type: build-setting" '    id: "dotnet_analyzer_diagnostic.category-roslynator.severity = none"' "    status: active"
printf '[tests/**.cs]\ndotnet_analyzer_diagnostic.category-roslynator.severity = none\n' >> "$d/.editorconfig"; run "$d"
expect "an editorconfig build setting only under a narrower section fails" 1 "RULE-CHECK-001" "category-roslynator"

d="$(fixture nuget-comment)"; printf '<configuration>\n  <!-- <add key="x" value="AW0042" /> -->\n</configuration>\n' > "$d/nuget.config"
addcheck "$(rec "$d" no-catch-exception)" "  - type: build-setting" '    id: "AW0042"' "    status: active"; run "$d"
expect "a build setting found only in a nuget.config comment fails" 1 "RULE-CHECK-001" "AW0042"

d="$(fixture steps-flush)"; printf 'jobs:\n  dotnet:\n    steps:\n    - name: The SDK is exactly the global.json pin\n' > "$d/.github/workflows/ci.yml"; run "$d"
expect "a steps list at the same indent as steps: is read" 0 "rules: ok"

d="$(fixture glob-any)"; printf '[**]\ndotnet_diagnostic.CA1031.severity = none\n' >> "$d/.editorconfig"; run "$d"
expect "[**] is a general section" 1 "RULE-CHECK-001" "CA1031"

d="$(fixture glob-list)"; printf '[{*.cs,*.vb}]\ndotnet_diagnostic.CA1031.severity = none\n' >> "$d/.editorconfig"; run "$d"
expect "[{*.cs,*.vb}] is a general section" 1 "RULE-CHECK-001" "CA1031"

d="$(fixture glob-csold)"; printf '[*.{cs-old}]\ndotnet_diagnostic.CA1031.severity = none\n' >> "$d/.editorconfig"; run "$d"
expect "[*.{cs-old}] is not a C# section" 0 "rules: ok"

# ---- review run 4: verbatim strings that open with a quote, Roslyn's property syntax, nested braces, path globs,
# unreadable code, non-dotnet editorconfig settings, CodeMetricsConfig ----
d="$(fixture verbatim-quote)"; printf 'var s = @"""quoted"" // x"; // ARCH-SLICE-001\nvar t = 1;\n' > "$d/tests/Aw.Architecture.Tests/V.cs"; run "$d"
refuse "a verbatim string opening with an escaped quote isn't a raw string" "ARCH-SLICE-001 (PR #131) — fix"

d="$(fixture inline-semicolon)"; sub "$d/.editorconfig" "dotnet_diagnostic.CA1031.severity = error    # no catch (Exception) outside boundaries" "dotnet_diagnostic.CA1031.severity = error ; why"; run "$d"
expect "an inline ; comment ends an editorconfig value" 0 "rules: ok"

d="$(fixture colon-sep)"; printf 'dotnet_diagnostic.CA1031.severity : none\n' >> "$d/.editorconfig"; run "$d"
expect "a key: value property line counts" 1 "RULE-CHECK-001" "CA1031"

d="$(fixture nested-braces)"; printf '[{*.{cs,vb}}]\ndotnet_diagnostic.CA1031.severity = none\n' >> "$d/.editorconfig"; run "$d"
expect "[{*.{cs,vb}}] is a general section" 1 "RULE-CHECK-001" "CA1031"

d="$(fixture nested-narrow)"; printf '[{src/{a,*.cs,b}}]\ndotnet_diagnostic.CA1031.severity = none\n' >> "$d/.editorconfig"; run "$d"
expect "[{src/{a,*.cs,b}}] is a narrow section" 0 "rules: ok"

d="$(fixture one-level)"; drop "$d/.editorconfig" 'CA1031'; printf '[*/*.cs]\ndotnet_diagnostic.CA1031.severity = error\n' >> "$d/.editorconfig"; run "$d"
expect "[*/*.cs] is a narrow section" 1 "RULE-CHECK-001" "CA1031"

d="$(fixture lexer-fails)"; printf 'const s = `abc ${ x }; // DEPS-CYCLE-001\n' > "$d/scripts/check-deps.mjs"; run "$d"
expect "an id in code the lexer can't read doesn't count" 1 "RULE-CHECK-001" "DEPS-CYCLE-001"

d="$(fixture ma-setting)"; printf 'MA0051.maximum_lines_per_method = 40\n' >> "$d/.editorconfig"
addcheck "$(rec "$d" no-catch-exception)" "  - type: build-setting" '    id: "MA0051.maximum_lines_per_method = 40"' "    status: active"; run "$d"
expect "an editorconfig setting is found in a general section" 0 "rules: ok"
printf '[*.cs]\nMA0051.maximum_lines_per_method = 60\n' >> "$d/.editorconfig"; run "$d"
expect "an editorconfig setting overridden later fails" 1 "RULE-CHECK-001" "maximum_lines_per_method"

d="$(fixture metrics)"; printf '# limits\nCA1502: 10\n' > "$d/CodeMetricsConfig.txt"
addcheck "$(rec "$d" no-catch-exception)" "  - type: build-setting" '    id: "CA1502: 10"' "    status: active"; run "$d"
expect "a CodeMetricsConfig.txt limit is a build setting" 0 "rules: ok"

# ---- review run 5 (post-cap): build-setting literals match whole tokens; deps.yaml limits are build settings ----
d="$(fixture metrics-100)"; printf 'CA1502: 100\n' > "$d/CodeMetricsConfig.txt"
addcheck "$(rec "$d" no-catch-exception)" "  - type: build-setting" '    id: "CA1502: 10"' "    status: active"; run "$d"
expect "a build-setting literal matches whole tokens (CA1502: 100 isn't CA1502: 10)" 1 "RULE-CHECK-001" "CA1502: 10"

d="$(fixture deps-limit)"; printf 'limits:\n  projectFanOut: 4  # spec 4b.1a\n' > "$d/deps.yaml"
addcheck "$(rec "$d" no-catch-exception)" "  - type: build-setting" '    id: "projectFanOut: 4"' "    status: active"; run "$d"
expect "a deps.yaml limit is a build setting" 0 "rules: ok"
printf 'limits:\n  projectFanOut: 40\n' > "$d/deps.yaml"; run "$d"
expect "a raised deps.yaml limit fails" 1 "RULE-CHECK-001" "projectFanOut: 4"

d="$(fixture norecords)"; mv "$d/rules" "$d/rules.gone"; run "$d"
expect "a tree with no rules directory is a tool error" 2 "check-rules: error"

echo "check-rules: $PASS passed, $FAIL failed"
[ "$FAIL" = 0 ]
