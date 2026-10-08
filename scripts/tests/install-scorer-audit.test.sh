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

test_cron_hint_escapes_percent() {
  # An unescaped % in a crontab line ends the command; the hint must print \% so a pasted entry works.
  # The echo in install-scorer.sh holds the text \$(date +\\%F); it prints $(date +\%F).
  grep -F 'weekly cron entry' "$ROOT/scripts/install-scorer.sh" | grep -qF '\$(date +\\%F)"' || { echo "FAIL: cron hint does not escape % as \\%"; exit 1; }
  if grep -F 'weekly cron entry' "$ROOT/scripts/install-scorer.sh" | grep -qF '+%F'; then echo "FAIL: cron hint still has an unescaped %F"; exit 1; fi
  echo "PASS: test_cron_hint_escapes_percent"
}

test_plist_exists_and_is_valid
test_plist_runs_weekly_audit_into_dated_dir
test_installer_installs_both_plists
test_cron_hint_escapes_percent
