#!/usr/bin/env bash
# Tests for the code-index fixtures (tests/fixtures/code-index, phase CI-0c). Run: bash scripts/tests/code-index-fixture.test.sh
# Needs node (and git for the sample's text check). No dotnet, no network: it runs the fixture's own checks, proves each can
# fail by breaking a copy, and runs the hostile generator twice into temp dirs.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
FX="$ROOT/tests/fixtures/code-index"
fail=0

check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

WORK="$(mktemp -d)"
trap 'find "$WORK" -delete 2>/dev/null' EXIT

node "$FX/verify.mjs" >/dev/null 2>&1; check "verify.mjs passes on the fixture" "$?" "0"
node "$FX/quality-sample/check.mjs" >/dev/null 2>&1; check "check.mjs passes on the sample" "$?" "0"

# each check can fail: break a copy
cp -R "$FX" "$WORK/fx"
sed 's/"startLine": 12,/"startLine": 13,/' "$WORK/fx/expected.json" > "$WORK/fx/e.json" && mv "$WORK/fx/e.json" "$WORK/fx/expected.json"
node "$WORK/fx/verify.mjs" >/dev/null 2>&1; check "verify.mjs fails when a symbol line moves" "$?" "1"
cp "$FX/expected.json" "$WORK/fx/expected.json"
C="$WORK/fx/project/src/Shapes/Circle.cs"; { echo '// moved'; cat "$C"; } > "$C.new" && mv "$C.new" "$C"
node "$WORK/fx/verify.mjs" >/dev/null 2>&1; check "verify.mjs fails when a source line moves" "$?" "1"
sed 's/"label": true/"label": "yes"/' "$FX/quality-sample/pairs.json" > "$WORK/fx/quality-sample/pairs.json"
node "$WORK/fx/quality-sample/check.mjs" >/dev/null 2>&1; check "check.mjs fails on a non-boolean label" "$?" "1"

# the hostile generator: deterministic, refuses a non-empty dir, usage exit 2
node "$FX/hostile/generate.mjs" --out "$WORK/h1" >/dev/null 2>&1; check "generate.mjs writes a tree" "$?" "0"
node "$FX/hostile/generate.mjs" --out "$WORK/h2" >/dev/null 2>&1
check "generate.mjs is deterministic" "$(cd "$WORK/h1" && find . -type f -print0 | sort -z | xargs -0 cksum | cksum)" "$(cd "$WORK/h2" && find . -type f -print0 | sort -z | xargs -0 cksum | cksum)"
node "$FX/hostile/generate.mjs" --out "$WORK/h1" >/dev/null 2>&1; check "generate.mjs refuses a non-empty directory" "$?" "1"
node "$FX/hostile/generate.mjs" >/dev/null 2>&1; check "generate.mjs without --out exits 2" "$?" "2"
check "the symlink is a symlink" "$([ -L "$WORK/h1/links/link.cs" ] && echo yes || echo no)" "yes"
check "the over-cap file is over the cap" "$([ "$(wc -c < "$WORK/h1/generated/over-cap.cs")" -gt $((512 * 1024)) ] && echo yes || echo no)" "yes"
check "the boundary file is under the cap" "$([ "$(wc -c < "$WORK/h1/generated/at-cap-boundary.cs")" -lt $((512 * 1024)) ] && echo yes || echo no)" "yes"
check "the manifest names every file" "$(node -p 'JSON.parse(require("fs").readFileSync(process.argv[1],"utf8")).files.length' "$WORK/h1/manifest.json")" "$(cd "$WORK/h1" && find . \( -type f -o -type l \) ! -name manifest.json | wc -l | tr -d ' ')"

exit "$fail"
