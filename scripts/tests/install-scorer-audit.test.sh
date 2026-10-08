#!/usr/bin/env bash
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$DIR/../.."
PLIST="$ROOT/config/launchd/com.agentic-workflow.scorer-audit.plist"

test_plist_exists_and_is_valid() {
  [ -f "$PLIST" ] || { echo "FAIL: $PLIST missing"; exit 1; }
  if command -v plutil >/dev/null 2>&1; then plutil -lint "$PLIST" >/dev/null || { echo "FAIL: plist invalid"; exit 1; }; fi
  echo "PASS: test_plist_exists_and_is_valid"
}

test_plist_runs_weekly_audit_into_dated_dir() {
  grep -q 'scorer audit --since 7d --label 0 --no-turns-file --out __HOME__/.agentic-workflow/audit/weekly/$(date +%F)' "$PLIST" || { echo "FAIL: audit command missing"; exit 1; }
  grep -q '<key>Weekday</key>' "$PLIST" || { echo "FAIL: not weekly"; exit 1; }
  echo "PASS: test_plist_runs_weekly_audit_into_dated_dir"
}

test_installer_installs_both_plists() {
  grep -q 'com.agentic-workflow.scorer-audit.plist' "$ROOT/scripts/install-scorer.sh" || { echo "FAIL: installer does not install the audit plist"; exit 1; }
  echo "PASS: test_installer_installs_both_plists"
}

test_plist_exists_and_is_valid
test_plist_runs_weekly_audit_into_dated_dir
test_installer_installs_both_plists
