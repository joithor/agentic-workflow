#!/usr/bin/env bash
# tests/fixtures/scoping/proof.sh — the SC-F owner proof (spec 14, SC-F): the fixture against a real Sindri, in a guarded
# temp home, through scripts/sindri-prove.sh only. It builds the fixture repo, approves a profile that names it, starts the
# control plane, imports map.yaml as the owner (typing the confirmation), runs probes.json twice, scores the gold, and stops.
#
#   SINDRI=<built sindri apphost> tests/fixtures/scoping/proof.sh [--keep]
#
# SINDRI is the built C# CLI as an executable (the apphost next to sindri.dll, not `dotnet sindri.dll`: serve starts itself by
# that path). Build it through the heavy wrapper: heavy dotnet build src/Sindri/Aw.Sindri.Host -c Release -o <dir>.
# Needs git and node. The pinned Temporal CLI must be installed (scripts/binaries-lock.sh fetch temporal); this
# script links the one from ${AW_REAL_STATE:-$HOME/.agentic-workflow}/cache/bin into the temp state, and installs nothing.
# Everything lives under ${TMPDIR:-/tmp}/scoping-proof.XXXXXX, which is removed on success (and with --keep never).
# Why the map and the probes are copied: sindri refuses a map or probe file inside any git worktree (SND-CONTEXT-005).
# Exit: 0 every step passed, 1 a step failed (the temp dir is kept), 2 usage or a missing tool.
set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
PROVE="$ROOT/scripts/sindri-prove.sh"
keep=0
case "${1:-}" in --keep) keep=1 ;; "") ;; *) echo "usage: SINDRI=<sindri> proof.sh [--keep]" >&2; exit 2 ;; esac
[ -n "${SINDRI:-}" ] && [ -x "$SINDRI" ] || { echo "proof: SINDRI must be the built sindri executable (got '${SINDRI:-}')" >&2; exit 2; }
command -v node >/dev/null && command -v git >/dev/null || { echo "proof: git and node are needed" >&2; exit 2; }

step() { printf '\n== %s\n' "$*"; }
fail() { echo "FAIL: $*" >&2; echo "(kept: $work)" >&2; exit 1; }

work="$(mktemp -d "${TMPDIR:-/tmp}/scoping-proof.XXXXXX")" || exit 2
export TMPDIR="${TMPDIR:-/tmp}"
real_state="${AW_REAL_STATE:-$HOME/.agentic-workflow}"
mkdir -p "$work/repos" "$work/second" "$work/files" "$work/state/cache/bin" "$work/sindri/profile" "$work/home"
temporal="$(ls -d "$real_state"/cache/bin/temporal-[0-9]* 2>/dev/null | head -1)"
[ -n "$temporal" ] || fail "no pinned temporal CLI under $real_state/cache/bin (fix: scripts/binaries-lock.sh fetch temporal)"
ln -s "$temporal" "$work/state/cache/bin/$(basename "$temporal")"

# The owner's session variables must not reach the guarded home; sindri-prove.sh human unsets the agent ones itself.
unset TMUX AW_META_CONFIG CLAUDE_LOCAL_BIN
export AW_SINDRI_HOME="$work/sindri" AW_STATE_DIR="$work/state" SINDRI
stopped=0
stop_serve() { [ "$stopped" = 1 ] || { "$PROVE" human stop >/dev/null 2>&1 || true; stopped=1; }; }
trap stop_serve EXIT

step "1. the history: build the fixture repo (about 30 commits, one past PR adding link revocation)"
head="$(bash "$HERE/history/build.sh" "$work/repos")" || fail "history/build.sh"
echo "head $head"
[ "$head" = "$(sed -n 1p "$HERE/history/golden-head.txt")" ] || fail "the head is not the pinned one"
git -C "$work/repos/fixture-notes" log main --merges --format='%s' | sed -n '1p;$p'
echo "commits on all branches: $(git -C "$work/repos/fixture-notes" rev-list --all --count)"
[ "$(git -C "$work/repos/fixture-notes" log main --merges --format=%s | grep -c link-revocation)" = 1 ] || fail "the revocation PR is not in git log"
echo "the revocation PR is in git log: yes"
# Build twice more into another dir: the same head (reproducible).
second="$(bash "$HERE/history/build.sh" "$work/second")" || fail "history/build.sh (second)"
[ "$second" = "$head" ] || fail "the second build gave $second"
echo "second build, same head: $second"

step "2. gold self-check (gold scope 1.0 / 1.0, decoy-only 0.0 / 0.0)"
node "$HERE/selfcheck.mjs" | tail -12 || fail "selfcheck"

step "3. profile: validate and approve it as the owner (typed)"
cat > "$AW_SINDRI_HOME/profile/profile.yaml" <<YAML
schemaVersion: 1
user: fixture
repos:
  - { name: fixture-notes, path: $work/repos/fixture-notes }
context:
  repos: [fixture-notes]
api: { port: 0 }
YAML
hash="$("$PROVE" profile validate --json | sed -n 's/.*"hash":"\([0-9a-f]*\)".*/\1/p')"
[ -n "$hash" ] || fail "profile validate gave no hash"
printf 'approve profile %s\n' "${hash:0:6}" | "$PROVE" human profile approve "$hash" | tail -2
(nohup "$PROVE" human --headless > "$work/serve.log" 2>&1 &)
for _ in $(seq 1 90); do [ -f "$AW_SINDRI_HOME/control.json" ] && break; sleep 1; done
[ -f "$AW_SINDRI_HOME/control.json" ] || fail "serve did not start: $(tail -3 "$work/serve.log")"
"$PROVE" status | sed -n '1,2p;$p'

step "4. context map import: map.yaml loads with 0 errors (typed confirmation)"
cp "$HERE/map.yaml" "$HERE/probes.json" "$work/files/"
preview="$(echo | "$PROVE" human context map import "$work/files/map.yaml" 2>&1)"
typed="$(printf '%s\n' "$preview" | sed -n "s/.*Type '\(import map [0-9a-f]*\)'.*/\1/p" | head -1)"
[ -n "$typed" ] || fail "no import challenge in: $preview"
out="$(printf '%s\n' "$typed" | "$PROVE" human context map import "$work/files/map.yaml" 2>&1)" || fail "import: $out"
printf '%s\n' "$out" | tail -3
case "$out" in *"Imported map"*) ;; *) fail "not imported: $out" ;; esac
"$PROVE" human context map status | head -2

step "5. context eval probes: probes.json runs (first run, then again: no regression)"
first="$("$PROVE" human context eval probes --file "$work/files/probes.json" 2>&1)" || fail "probes (first run): $first"
printf '%s\n' "$first" | sed '$d' 
"$PROVE" human context eval probes --file "$work/files/probes.json" > "$work/probes2.txt" 2>&1; rc=$?
tail -4 "$work/probes2.txt"
[ "$rc" = 0 ] || fail "second probe run exited $rc"
echo "probes exit: 0 (no regression against the first run)"

step "6. stop"
stop_serve
echo "stopped"
echo
echo "PASS: SC-F proof"
[ "$keep" = 1 ] && echo "(kept: $work)" || rm -rf "$work"
exit 0
