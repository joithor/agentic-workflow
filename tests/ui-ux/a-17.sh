#!/usr/bin/env bash
# A-17 (UX pass 2026-10-25): Create item dialog jumps down when the 'Title is required' error shows; Repo is a bare text box.
# Expected: the space for the error is reserved (an empty submit moves nothing: the card keeps its height and the Repo, Body
# and Cancel keep their places); the Repo field offers the known repos (a choice box, not a bare TextBox).
source "$(dirname "$0")/lib.sh"
uxu_start up items

# Every driver call is bounded (a hung driver is a setup error, not a hang).
dt() { timeout 120 "$UXU_DRIVER" --dir "$AW_UI_DRIVER_DIR" "$@"; }

dt click --type ListBoxItem --name Work >/dev/null || uxu_die "no Work tab"
sleep 2
dt click --type Button --name "Create item" >/dev/null || uxu_die "no Create item button"
dt wait-for --type Border --name "Create item" --state visible --wait-ms 15000 >/dev/null || uxu_die "the Create item dialog never opened"
sleep 1

card_id="$(dt search --type Border --name "Create item" | jq -r '.matches[0].id // empty')"
[ -n "$card_id" ] || uxu_die "no dialog card"
# Layout is the x, y, width, height of a search match.
bounds_of() {
  local out
  out="$(dt search "$@" | jq -c '[.matches[] | select(.visible != false)] | last | select(. != null) | [.x, .y, .width, .height]')"
  { [ -n "$out" ] && [ "$out" != "[null,null,null,null]" ]; } || uxu_die "no bounds for: $*"
  printf '%s' "$out"
}
card_before="$(bounds_of --type Border --name "Create item")"
cancel_before="$(bounds_of --name Cancel)"
body_before="$(bounds_of --name Body)"

# The Create control is a CommandControl wrapper; its Button is the first child.
create_id="$(dt search --type CommandControl | jq -r '[.matches[] | select(.visible != false)] | last | .id // empty')"
[ -n "$create_id" ] || uxu_die "no Create control in the dialog"
dt click --node-id "$create_id/0" >/dev/null || uxu_die "the Create control's button did not click"
dt wait-for --contains "required" --state visible --wait-ms 15000 >/dev/null || uxu_die "the blank-title error never showed"
sleep 1

card_after="$(bounds_of --type Border --name "Create item")"
cancel_after="$(bounds_of --name Cancel)"
body_after="$(bounds_of --name Body)"

expect "the dialog card keeps its size when the error shows ($card_before -> $card_after)" test "$card_before" = "$card_after"
expect "Cancel keeps its place when the error shows ($cancel_before -> $cancel_after)" test "$cancel_before" = "$cancel_after"
expect "the Body field keeps its place when the error shows ($body_before -> $body_after)" test "$body_before" = "$body_after"
expect "the Repo field is a choice box (ComboBox or AutoCompleteBox) offering the known repos" \
  bash -c '[ "$(timeout 120 "$0" --dir "$1" tree --node-id "$2" --depth 12 | jq "[.. | objects | select(.type? == \"ComboBox\" or .type? == \"AutoCompleteBox\")] | length")" -ge 1 ]' \
  "$UXU_DRIVER" "$AW_UI_DRIVER_DIR" "$card_id"

uxu_done
