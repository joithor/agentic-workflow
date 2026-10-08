#!/usr/bin/env bash
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$DIR/../.."
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

test_dry_run_writes_nothing() {
  local out
  out="$(AW_DRY_RUN=1 CLAUDE_LOCAL_BIN="$TMP/bin" bash "$ROOT/scripts/install-sindri.sh")"
  grep -q "\[dry-run\] would write $TMP/bin/sindri" <<<"$out" || { echo "FAIL: dry-run line missing"; exit 1; }
  [ ! -e "$TMP/bin/sindri" ] || { echo "FAIL: dry-run wrote the wrapper"; exit 1; }
  echo "PASS: test_dry_run_writes_nothing"
}

test_wrapper_execs_the_built_cli() {
  AW_SKIP_BUILD=1 AW_SKIP_LAUNCHD=1 CLAUDE_LOCAL_BIN="$TMP/bin" bash "$ROOT/scripts/install-sindri.sh" > /dev/null
  [ -x "$TMP/bin/sindri" ] || { echo "FAIL: wrapper not executable"; exit 1; }
  grep -q 'exec "/.*node" ".*/sindri/dist/cli.js" "\$@"' "$TMP/bin/sindri" || { echo "FAIL: wrapper does not exec dist/cli.js with an absolute node"; exit 1; }
  grep -q "export SINDRI_BIN=\"$TMP/bin/sindri\"" "$TMP/bin/sindri" || { echo "FAIL: wrapper does not export SINDRI_BIN"; exit 1; }
  echo "PASS: test_wrapper_execs_the_built_cli"
}

test_setup_has_opt_in_flag() {
  grep -q -- '--with-sindri) WITH_SINDRI=1' "$ROOT/setup.sh" || { echo "FAIL: setup.sh lacks --with-sindri"; exit 1; }
  grep -q 'would run scripts/install-sindri.sh' "$ROOT/setup.sh" || { echo "FAIL: setup.sh dry-run line missing"; exit 1; }
  echo "PASS: test_setup_has_opt_in_flag"
}

test_dry_run_writes_nothing
test_wrapper_execs_the_built_cli
test_setup_has_opt_in_flag
