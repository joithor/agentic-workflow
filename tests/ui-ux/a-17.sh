#!/usr/bin/env bash
# A-17 (UX pass 2026-10-25): Create item dialog jumps down when the 'Title is required' error shows; Repo is a bare text box.
# Expected: the space for the error is reserved (an empty submit moves nothing: the card keeps its height and the Repo, Body
# and Cancel keep their places); the Repo field offers the known repos (a choice box, not a bare TextBox).
source "$(dirname "$0")/lib.sh"
uxu_start up items

d click --type ListBoxItem --name Work >/dev/null || uxu_die "no Work tab"
sleep 2
d click --type Button --name "Create item" >/dev/null || uxu_die "no Create item button"
d wait-for --type Border --name "Create item" --state visible --wait-ms 15000 >/dev/null || uxu_die "the Create item dialog never opened"
sleep 1

card_id="$(d search --type Border --name "Create item" | jq -r '.matches[0].id // empty')"
[ -n "$card_id" ] || uxu_die "no dialog card"
# Layout is read through props (the Bounds line); an element that cannot be found or has no Bounds is a setup error.
node_id() { d search "$@" | jq -r '[.matches[] | select(.visible != false)] | last | .id // empty'; }
bounds_of() {
  local id out
  id="$(node_id "$@")"; [ -n "$id" ] || uxu_die "no visible element for: $*"
  out="$(d props --node-id "$id" | grep -iE '^ *"?Bounds' | head -1)"; [ -n "$out" ] || uxu_die "no Bounds for: $*"
  printf '%s' "$out"
}
card_before="$(bounds_of --type Border --name "Create item")"
cancel_before="$(bounds_of --name Cancel)"
body_before="$(bounds_of --name Body)"

d click --name CreateControl >/dev/null || uxu_die "no Create control in the dialog"
d wait-for --contains "required" --state visible --wait-ms 15000 >/dev/null || uxu_die "the blank-title error never showed"
sleep 1

card_after="$(bounds_of --type Border --name "Create item")"
cancel_after="$(bounds_of --name Cancel)"
body_after="$(bounds_of --name Body)"

expect "the dialog card keeps its size when the error shows ($card_before -> $card_after)" test "$card_before" = "$card_after"
expect "Cancel keeps its place when the error shows ($cancel_before -> $cancel_after)" test "$cancel_before" = "$cancel_after"
expect "the Body field keeps its place when the error shows ($body_before -> $body_after)" test "$body_before" = "$body_after"
expect "the Repo field is a choice box (ComboBox or AutoCompleteBox) offering the known repos" \
  bash -c '[ "$("$0" --dir "$1" tree --node-id "$2" --depth 12 | jq "[.. | objects | select(.type? == \"ComboBox\" or .type? == \"AutoCompleteBox\")] | length")" -ge 1 ]' \
  "$UXU_DRIVER" "$AW_UI_DRIVER_DIR" "$card_id"

uxu_done
