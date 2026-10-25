#!/usr/bin/env bash
# A-11 (UX pass 2026-10-25): Work list: the id runs into the Status column ('...0872ddacNew') when narrow or with a sheet open.
# Expected: the columns keep a gap and the id truncates: in a Work row the id's right edge stays at least 8 px left of the
# Repo and Status cells, at the 900 px minimum window and at 1280 with the item sheet open.
source "$(dirname "$0")/lib.sh"
uxu_start up items

d click --type ListBoxItem --name Work >/dev/null || uxu_die "no Work tab"
d wait-for --name IdText --state visible --wait-ms 30000 >/dev/null || uxu_die "no Work row showed its id"
sleep 2

# xof <node id>: the node's left edge in window coordinates (empty when it cannot be read).
xof() { d tree --node-id "$1" --depth 0 | jq -r 'first(.. | objects | select(has("x"))) | .x // empty'; }
# The first visible row title button (named "Open <id>", text null) is the row grid's child 0: the Repo cell is child 1 and
# the Status word is the first child of child 2 (their names are bound, so they are found through the row).
row_button() { d search --type Button --limit 200 | jq -r '[.matches[] | select(.visible != false and ((.name // "") | startswith("Open ")))][0].id // empty'; }

# measure <label>: set GAP_REPO and GAP_STATE to the px between the first row's id right edge and the Repo and Status cells.
measure() {
  local btn row idid idx idw repo state
  btn="$(row_button)"; [ -n "$btn" ] || uxu_die "$1: no Work row title button"
  row="${btn%/*}"
  idid="$(d search --name IdText --limit 1 | jq -r '.matches[0].id // empty')"; [ -n "$idid" ] || uxu_die "$1: no IdText"
  idx="$(xof "$idid")"; idw="$(d search --name IdText --limit 1 | jq -r '.matches[0].width // empty')"
  repo="$(xof "$row/1")"; state="$(xof "$row/2/0")"
  [ -n "$idx" ] && [ -n "$idw" ] && [ -n "$repo" ] && [ -n "$state" ] || uxu_die "$1: could not measure (id=$idx+$idw repo=$repo state=$state)"
  GAP_REPO="$(jq -n --argjson i "$idx" --argjson w "$idw" --argjson r "$repo" '$r - ($i + $w)')"
  GAP_STATE="$(jq -n --argjson i "$idx" --argjson w "$idw" --argjson s "$state" '$s - ($i + $w)')"
  echo "$1: gap to Repo $GAP_REPO px, to Status $GAP_STATE px"
}
at_least_8() { echo "gap $1 px"; jq -n -e --argjson g "$1" '$g >= 8' >/dev/null; }

d window --width 900 --height 700 >/dev/null || uxu_die "could not size the window"
sleep 2
measure "900 wide"
expect "at 900 wide the id keeps a gap of 8 px before the Repo cell" at_least_8 "$GAP_REPO"
expect "at 900 wide the id keeps a gap of 8 px before the Status cell" at_least_8 "$GAP_STATE"

d window --width 1280 --height 900 >/dev/null || uxu_die "could not size the window"
sleep 1
btn="$(row_button)"; [ -n "$btn" ] || uxu_die "no row title button to open the item sheet"
d click --node-id "$btn" >/dev/null || uxu_die "could not open the item sheet"
sleep 2
measure "1280 with the sheet open"
expect "with the item sheet open the id keeps a gap of 8 px before the Repo cell" at_least_8 "$GAP_REPO"
expect "with the item sheet open the id keeps a gap of 8 px before the Status cell" at_least_8 "$GAP_STATE"

uxu_done
