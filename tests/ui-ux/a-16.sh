#!/usr/bin/env bash
# A-16 (UX pass 2026-10-25): a leftover sheet edge tab with a raw instance id stays on every page.
# Expected: after an instance sheet is closed with Esc, its edge tab reads the instance's readable title (not the raw
# address), and the tab goes when the page it belongs to is left (Work, Agents, System).
source "$(dirname "$0")/lib.sh"
uxu_start up recipes workflows

# The strip row's open button is x:Name StripTitle (its accessible name is "<item title>, <recipe>"; a --name or --contains on
# either misses it when the title differs), so find it in WorkflowsRows by automationId and click it by node id.
d click --type ListBoxItem --name Home >/dev/null || uxu_die "no Home tab"
row=""
for _ in $(seq 1 40); do
  row="$(d tree --name WorkflowsRows --depth 14 2>/dev/null | jq -r '[.. | objects | select(.automationId == "StripTitle" or .name == "StripTitle") | .id][0] // empty')"
  [ -n "$row" ] && break
  sleep 1.5
done
if [ -z "$row" ]; then
  d tree --name WorkflowsPanel --depth 14 2>&1 | jq -r '[.. | objects | select(((.text // "") != "") or ((.name // "") != "")) | "\(.type) name=\(.name) text=\(.text)"] | .[]' >&2
  uxu_die "no workflow row on Home"
fi
d click --node-id "$row" >/dev/null || uxu_die "could not open the instance sheet"
d wait-for --name InstanceTitle --state visible --wait-ms 30000 >/dev/null || uxu_die "the instance sheet never showed its title"
sleep 2
title="$(d tree --name InstanceTitle --depth 0 | jq -r '[.. | objects | .text? // empty] | .[0]')"
[ -n "$title" ] || uxu_die "the sheet has no readable title"
d key --chord Escape >/dev/null || uxu_die "Esc failed"
d wait-for --name EdgeTab --state visible --wait-ms 10000 >/dev/null || uxu_die "no edge tab after Esc"

expect "the edge tab reads the readable title ($title)" \
  bash -c '[ "$("$0" --dir "$1" tree --name EdgeTab --depth 4 | jq -r "[.. | objects | .text? // empty] | .[0]")" = "$2" ]' \
  "$UXU_DRIVER" "$AW_UI_DRIVER_DIR" "$title"
expect "the edge tab text is not a raw address" \
  bash -c '! "$0" --dir "$1" tree --name EdgeTab --depth 4 | jq -r "[.. | objects | .text? // empty] | .[0]" | grep -qE "^ITM-|^desk:WFI-|/[0-9]+$"' \
  "$UXU_DRIVER" "$AW_UI_DRIVER_DIR"

for page in Work Agents System; do
  d click --type ListBoxItem --name "$page" >/dev/null || uxu_die "no $page tab"
  sleep 2
  expect "the edge tab is gone on $page" \
    bash -c '! "$0" --dir "$1" props --name EdgeTab | grep -E "IsVisible" | grep -qi true' \
    "$UXU_DRIVER" "$AW_UI_DRIVER_DIR"
done

uxu_done
