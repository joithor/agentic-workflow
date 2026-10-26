#!/usr/bin/env bash
# A-8 (UX pass 2026-10-25): Home's Workflows strip truncates titles, so two rows both read 'Fixture request for ...'.
# Expected: the strip's rows can be told apart at 1280 wide: each title button is wide enough to show its subject (not cut to
# ~14 characters), and the titles that share a prefix differ in what is shown.
source "$(dirname "$0")/lib.sh"

dt() { timeout 120 "$UXU_DRIVER" --dir "$AW_UI_DRIVER_DIR" "$@"; }

uxu_start up recipes workflows "incident start"

dt click --type ListBoxItem --name Home >/dev/null || uxu_die "no Home tab"
dt wait-for --contains "Fixture request for" --state visible --wait-ms 30000 >/dev/null || uxu_die "the strip never showed a 'Fixture request for' row"
sleep 3

# The strip's title buttons: their name is the subject and the recipe ("Fixture request for X, Dev Desk Round Trip").
title_buttons() { dt search --type Button --limit 200 | jq -c '[.matches[] | select(.visible) | select((.name // "") | test(", Dev "))]'; }

# Needs-you rows shown: their title buttons are named "Open <title> ITM-<id>" (a queue row's is "Open ITM-<id>").
needs_you_count() { dt search --type Button --limit 200 | jq '[.matches[] | select(.visible) | select((.name // "") | test("^Open .+ ITM-"))] | length'; }

# The strip shows the fold's budget (8) less the Needs-you rows shown, and at most the 4 instances the fixture started;
# Needs-you fills in a little after the strip, so wait until its count is the same on two reads 2 s apart.
settled=""
for _ in $(seq 1 15); do
  before="$(needs_you_count)"; sleep 2; after="$(needs_you_count)"
  [ -n "$before" ] && [ "$before" = "$after" ] && { settled="$after"; break; }
done
[ -n "$settled" ] || uxu_die "the Needs-you row count never settled within 30 s"
want=$((8 - settled)); [ "$want" -gt 4 ] && want=4

strip_has_expected_titles() { [ "$(title_buttons | jq 'length')" -eq "$want" ]; }
strip_titles_are_wide() { title_buttons | jq -e 'length > 0 and all(.[]; .width >= 240)'; }

expect "the strip shows $want titled rows (8 less the $settled Needs-you rows, at most 4)" strip_has_expected_titles
expect "every strip title button is at least 240 px wide at 1280 (today about 140)" strip_titles_are_wide

uxu_done
