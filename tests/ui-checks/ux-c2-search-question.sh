#!/usr/bin/env bash
# tests/ui-checks/ux-c2-search-question.sh — frozen regression check for the UX-pass finding C-2 ('Starts at' search cuts a question at its first space and shows CLI error text).
# Expected: a question with spaces is searched as asked; a miss says what to try in the app's own words, never a CLI hint such as '(fix: sindri context map status)'.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home with the Context page's data
# (context-lib.sh: sindri-ui-fixture.sh up + context up). Red on the baseline, green after the fix.
#   bash tests/ui-checks/ux-c2-search-question.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: CTXUX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
ctx_init ux-c2-search-question
ctx_start 1280 900

"$DRIVER" type --name "Ask or look up" --value "Tab groups" --replace >/dev/null 2>&1 || setup_fail "could not type into the search box"
"$DRIVER" key --chord Enter --name "Ask or look up" >/dev/null 2>&1 || setup_fail "Enter in the search box was not accepted"
sleep 4

cli="$(ctx_text --type TextBlock --contains "(fix:")"
[ -z "$cli" ] || fail "the question 'Tab groups' ended in CLI error text: '$cli'"
cut="$(ctx_text --type TextBlock --contains "is named 'Tab'")"
[ -z "$cut" ] || fail "the question 'Tab groups' was cut at its first space: '$cut'"
echo "PASS: the question 'Tab groups' was searched whole and showed no CLI text"
