#!/usr/bin/env bash
# scripts/forge-fixture.sh — the PR babysitter's fixture PR set (spec docs/superpowers/specs/2026-10-15-sindri-pr-babysitter-docs-sync-design.md,
# Appendix B.3; phase PB-X). It loads one variant of tests/fixtures/forge/pr-set/ into the file-backed fake forge (aw-forge-fake) in a
# guarded temp home and prints the pull request link, so the babysitter's blocks, prompt evals (PB-S1) and desk walk (PB-P1) run against a
# repository that can be rebuilt byte for byte.
#
#   forge-fixture.sh up --fake <variant>|all
#                                  build the variant's repository (a small CLI; the PR adds --json and leaves docs/usage.md stale),
#                                  open its pull request(s) on forge.test/fixture/<variant>, seed the threads, checks and flags,
#                                  render the hand-off and phase packet, and print: link, workspace, handoff, phase-packet, expected
#                                  (the answer key: each thread's queue outcome and judge type; never in the forge) and the env
#                                  that points aw-forge-fake and the file adapter at the world. A repeat up replaces the variant.
#                                  Variants: comments ciRed ciFlaky conflict stacked noDocs sourceFix foreignPush secret
#   forge-fixture.sh up --github <owner/repo> <variant> [--dry-run]
#                                  OWNER ONLY, on a scratch repository you do not mind writing to: pushes the variant's branches as
#                                  fixture/<variant>/* and opens its pull request(s) through the github adapter. Threads, checks and
#                                  flags cannot be seeded on GitHub: the thread list is printed to paste by hand from a second
#                                  account. Needs AW_FORGE_TOKEN (contents and pull-request write) and
#                                  FORGE_FIXTURE_GITHUB_SCRATCH=<owner/repo> (the same slug: the typed confirmation). --dry-run
#                                  builds the local repository and prints what would be pushed and opened, sending nothing.
#   forge-fixture.sh event merge-base [--variant stacked]
#                                  the mid-run event of the stacked variant: the base pull request merges (the PR is then a restack)
#   forge-fixture.sh status        the links of the variants that are up (exit 1 when none)
#   forge-fixture.sh list          the variants and what each is
#   forge-fixture.sh down [--all]  remove the variants and the world (only those: other entries of the home stay); --all removes the whole
#                                  home, including the aw-forge-fake build kept in <home>/bin
#
# The home is FORGE_FIXTURE_HOME (default ${TMPDIR:-/tmp}/aw-forge-fixture-<uid>). The guard runs first for every verb that touches it:
# its real path must be strictly inside realpath(${TMPDIR:-/tmp}) or inside a proof folder, <AW_STATE_DIR or ~/.agentic-workflow>/<repo
# slug>/proof/…, and contain no '..'. Anything else exits 2, so the fixture can never write to the owner's real forge world
# ($AW_STATE_DIR/forge/file): the CLI is always run with AW_STATE_DIR=<home>/state.
# Environment: AW_FORGE_FAKE (the aw-forge-fake command line, split on spaces; default: dotnet build of src/Forge/Aw.Forge.Host into
# <home>/bin, rebuilt when a source is newer, run as dotnet <home>/bin/aw-forge-fake.dll). Needs git, jq and (without AW_FORGE_FAKE) dotnet.
# Every file here is synthetic; the planted token of 'secret' is built at run time and is in no tracked file.
# Exit: 0 done; 1 status found nothing up; 2 usage, the guard, a refusal or a failed step.
# Tests: bash scripts/tests/forge-fixture.test.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SET="$ROOT/tests/fixtures/forge/pr-set"
OWNER="fixture"
HOST="forge.test"
BRANCH="feature/json-flag"
BASE_BRANCH="feature/json-base"
GIT_DATE="2026-10-15T09:00:00Z"
VARIANTS="comments ciRed ciFlaky conflict stacked noDocs sourceFix foreignPush secret"

die() { echo "forge-fixture: $*" >&2; exit 2; }
usage() { awk 'NR >= 2 { if (/^#/) print; else exit }' "$0" | sed 's/^# \{0,1\}//'; }
need() { command -v "$1" >/dev/null 2>&1 || die "$1 is needed"; }

kebab() {
  case "$1" in
    ciRed) echo ci-red ;; ciFlaky) echo ci-flaky ;; noDocs) echo no-docs ;;
    sourceFix) echo source-fix ;; foreignPush) echo foreign-push ;;
    *) echo "$1" ;;
  esac
}
known_variant() { case " $VARIANTS " in *" $1 "*) return 0 ;; *) return 1 ;; esac; }

# --- the guard -------------------------------------------------------------------------------------------------------
real() { (cd "$1" 2>/dev/null && pwd -P); }
guard() {
  local tmp_root="${TMPDIR:-/tmp}"; tmp_root="${tmp_root%/}"
  HOME_DIR="${FORGE_FIXTURE_HOME:-${tmp_root}/aw-forge-fixture-$(id -u)}"
  local msg="FORGE_FIXTURE_HOME must be under \$TMPDIR or a proof folder, with no '..' (got ${HOME_DIR})"
  case "$HOME_DIR" in /*) ;; *) die "$msg" ;; esac
  case "$HOME_DIR/" in */../* | */./* | *//*) die "$msg" ;; esac
  # the deepest existing ancestor is resolved (links followed); the part that doesn't exist yet is judged as written
  local ancestor="$HOME_DIR" rest="" temp state resolved below slug
  while [ ! -d "$ancestor" ]; do rest="/$(basename "$ancestor")$rest"; ancestor="$(dirname "$ancestor")"; done
  resolved="$(real "$ancestor")$rest"
  temp="$(real "${TMPDIR:-/tmp}")" || die "$msg"
  case "$resolved/" in
    "$temp"/?*) return 0 ;;
  esac
  state="${AW_STATE_DIR:-$HOME/.agentic-workflow}"
  if [ -d "$state" ]; then
    state="$(real "$state")"
    case "$resolved/" in
      "$state"/?*/proof/?*) below="${resolved#"$state"/}"; slug="${below%%/*}"; [ -n "$slug" ] && return 0 ;;
    esac
  fi
  die "$msg"
}

# --- the aw-forge-fake command ----------------------------------------------------------------------------------------
FORGE_CMD=()
ensure_bin() {
  if [ -n "${AW_FORGE_FAKE:-}" ]; then
    # shellcheck disable=SC2206
    FORGE_CMD=($AW_FORGE_FAKE)
    return
  fi
  need dotnet
  local dll="$HOME_DIR/bin/aw-forge-fake.dll"
  if [ ! -f "$dll" ] || [ -n "$(find "$ROOT/src/Forge" "$ROOT/src/Aw.Kernel" "$ROOT/src/Aw.Kernel.Telemetry" "$ROOT/src/Redaction/Aw.Redaction" -name '*.cs' -newer "$dll" -print -quit 2>/dev/null)" ]; then
    echo "forge-fixture: building aw-forge-fake into $HOME_DIR/bin" >&2
    mkdir -p "$HOME_DIR/bin"
    dotnet build "$ROOT/src/Forge/Aw.Forge.Host" -c Release -o "$HOME_DIR/bin" -v q --nologo >&2 || die "dotnet build of aw-forge-fake failed"
  fi
  FORGE_CMD=(dotnet "$dll")
}
forge() { AW_STATE_DIR="$HOME_DIR/state" AW_FORGE_ADAPTER=file AW_FORGE_HOSTS="$HOST" "${FORGE_CMD[@]}" "$@" </dev/null; }
WORLD() { echo "$HOME_DIR/state/forge/file/forge.json"; }

# Edits the world file for what the seed commands cannot do (resolved, edited, a conflict, a merged base, check logs). The world has one
# user while a fixture is being built, so no lock is taken. Enums are numbers in the file: PullStatus Merged = 3.
patch_pull() { # <repo> <number> <jq filter on the pull> [jq args…]
  local repo="$1" n="$2" filter="$3"; shift 3
  local tmp; tmp="$(mktemp "$HOME_DIR/state/forge/file/patch.XXXXXX")"
  jq --arg o "$OWNER" --arg r "$repo" --argjson n "$n" "$@" \
     "(.pulls[] | select(.owner==\$o and .repo==\$r and .number==\$n)) |= ($filter)" "$(WORLD)" >"$tmp" && mv "$tmp" "$(WORLD)"
}
patch_thread() { # <repo> <number> <key> <jq filter on the thread> [jq args…]
  local repo="$1" n="$2" key="$3" filter="$4"; shift 4
  patch_pull "$repo" "$n" ".threads |= map(if .key == \$key then ($filter) else . end)" --arg key "$key" "$@"
}

# --- building one variant's repository -----------------------------------------------------------------------------------
git_fx() { # <dir> <author name> <args…>: a commit-capable git with a fixed identity and date, and none of the user's config
  local dir="$1" who="$2"; shift 2
  GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_SYSTEM=/dev/null GIT_AUTHOR_NAME="$who" GIT_AUTHOR_EMAIL="$who@$HOST" \
    GIT_COMMITTER_NAME="$who" GIT_COMMITTER_EMAIL="$who@$HOST" GIT_AUTHOR_DATE="$GIT_DATE" GIT_COMMITTER_DATE="$GIT_DATE" \
    git -C "$dir" -c commit.gpgsign=false -c core.hooksPath=/dev/null "$@"
}
overlay() { cp -R "$SET/repo/$1/." "$2/"; }

build_repo() { # <variant> <work dir>
  local v="$1" w="$2"
  mkdir -p "$w"
  git -C "$w" init -q -b main
  overlay base "$w"
  if [ "$v" = noDocs ]; then
    rm -r "$w/docs"
    sed -i.bak '/docs\/usage.md/d' "$w/README.md" && rm -f "$w/README.md.bak"
    sed -i.bak 's/ and `docs\/`//' "$w/AGENTS.md" && rm -f "$w/AGENTS.md.bak"
  fi
  git_fx "$w" tally-dev add -A && git_fx "$w" tally-dev commit -q -m "chore: tally 0.3.0"
  if [ "$v" = stacked ]; then
    git_fx "$w" tally-dev checkout -q -b "$BASE_BRANCH"
    overlay overlays/stacked-base "$w"
    git_fx "$w" tally-dev add -A && git_fx "$w" tally-dev commit -q -m "feat: read standard input with -"
  else
    git_fx "$w" tally-dev checkout -q -b "$BRANCH"
  fi
  [ "$v" != stacked ] || git_fx "$w" tally-dev checkout -q -b "$BRANCH"
  overlay head "$w"
  [ "$v" != noDocs ] || git -C "$w" show main:CHANGELOG.md >"$w/CHANGELOG.md"   # the changelog line would name the change
  git_fx "$w" tally-dev add -A && git_fx "$w" tally-dev commit -q -m "feat: print results as JSON with --json"
}

render() { # <template dir or file> <dest> <proven> <base> <stack parent json>
  local src="$1" dest="$2" proven="$3" base="$4" stack="$5"
  sed -e "s|@PROVEN@|$proven|g" -e "s|@BASE@|$base|g" -e "s|@HEADREF@|$BRANCH|g" -e "s|@STACKPARENT@|$stack|g" "$src" >"$dest"
}

seed_threads() { # <repo> <number> <variant> -> writes the thread answer key rows to stdout as a JSON array
  local repo="$1" n="$2" link="https://$HOST/$OWNER/$1/pull/$2" rows="[]"
  local row id author assoc kind path body bot out key
  while IFS= read -r row; do
    id="$(jq -r .id <<<"$row")"; author="$(jq -r .author <<<"$row")"; assoc="$(jq -r .association <<<"$row")"
    kind="$(jq -r .kind <<<"$row")"; path="$(jq -r '.path // empty' <<<"$row")"; body="$(jq -r .body <<<"$row")"
    bot="$(jq -r '.bot // false' <<<"$row")"
    local args=(seed thread "$link" --author "$author" --body "$body" --kind "$kind" --association "$assoc")
    [ -z "$path" ] || args+=(--path "$path")
    [ "$bot" != true ] || args+=(--bot)
    out="$(forge "${args[@]}")" || die "seeding thread '$id' failed"
    key="$(jq -r .key <<<"$out")"
    jq -e '.ops.resolved' <<<"$row" >/dev/null 2>&1 && patch_thread "$repo" "$n" "$key" '.resolved = true'
    if jq -e '.ops.edited' <<<"$row" >/dev/null 2>&1; then
      patch_thread "$repo" "$n" "$key" '.body = $text' --arg text "$(jq -r .ops.edited <<<"$row")"
    fi
    if jq -e '.ops.marker' <<<"$row" >/dev/null 2>&1; then
      patch_thread "$repo" "$n" "$key" '.body = ($b + "\n\n<!-- aw-marker:" + $m + " -->")' --arg b "$body" --arg m "$(jq -r .ops.marker <<<"$row")"
    fi
    rows="$(jq -c --arg id "$id" --arg key "$key" --argjson row "$row" '. + [{id: $id, key: $key, expect: $row.expect}]' <<<"$rows")"
  done < <(jq -c '.threads[]' "$SET/comments.json")
  echo "$rows"
}

open_fake() { # <repo> <head> <base> <sha> <title> -> the pull number
  forge open --repo "$HOST/$OWNER/$1" --head "$2" --base "$3" --head-sha "$4" --title "$5" \
    --body "Adds --json. The fixture's PR: it leaves docs/usage.md stale." | jq -r .number
}

up_variant() { # <variant>
  local v="$1" repo dir w head base number link proven stack="null" base_pr="" threads="[]" flaky=false extra="{}"
  repo="$(kebab "$v")"; dir="$HOME_DIR/$repo"; w="$dir/work"
  [ ! -e "$dir" ] || remove_variant "$repo"
  mkdir -p "$dir"
  build_repo "$v" "$w"
  head="$(git -C "$w" rev-parse "$BRANCH")"
  base=main
  if [ "$v" = stacked ]; then
    base="$BASE_BRANCH"; stack="\"$BASE_BRANCH\""
    base_pr="$(open_fake "$repo" "$BASE_BRANCH" main "$(git -C "$w" rev-parse "$BASE_BRANCH")" "feat: read standard input with -")"
  fi
  number="$(open_fake "$repo" "$BRANCH" "$base" "$head" "feat: print results as JSON with --json")"
  link="https://$HOST/$OWNER/$repo/pull/$number"
  proven="$head"
  forge seed checks "$link" --state passing --passed 7 >/dev/null   # counts the UI shows beside the PR: 7 passed

  case "$v" in
    comments)
      threads="$(seed_threads "$repo" "$number")" ;;
    ciRed)
      forge seed checks "$link" --state failing --failing unit --passed 5 >/dev/null
      patch_pull "$repo" "$number" '.logs = {unit: $log}' --arg log $'not ok 2 - prints json\n  expected: [{"file":"a","count":7}]\n  actual:   [{"file":"a","count":"7"}]\n  at test/format.test.js:7:3\n# fail 1' ;;
    ciFlaky)
      forge seed checks "$link" --state failing --failing integration --passed 6 --pending 1 >/dev/null
      patch_pull "$repo" "$number" '.logs = {integration: $log}' --arg log $'Error: connect ETIMEDOUT 10.0.0.7:5432\n    at TCPConnectWrap.afterConnect (node:net:1611:16)\nrunner: shared-pool-4, retry 0 of 0\n# fail 1 (no test under test/ reads the network)'
      flaky=true ;;
    conflict)
      patch_pull "$repo" "$number" '.mergeable = false' ;;
    sourceFix)
      local out key; out="$(forge seed thread "$link" --author reviewer-kai --association COLLABORATOR --kind thread --path src/count.js \
        --body "count() gives 1 for an empty file with --lines: split gives [\"\"]. An empty file should count as 0.")"
      key="$(jq -r .key <<<"$out")"
      git_fx "$w" tally-dev checkout -q -b fixture/source-fix "$BRANCH"
      overlay overlays/source-fix "$w"
      git_fx "$w" tally-dev add -A
      git_fx "$w" tally-dev commit -q -m "fix: count an empty file as 0" -m "Sindri-Comment: $key"
      git_fx "$w" tally-dev checkout -q "$BRANCH"
      threads="$(jq -nc --arg key "$key" '[{id: "bug", key: $key, expect: {queue: "comment", type: "bug", note: "the fix changes src/count.js: the head moves in source, so the recheck runs"}}]')" ;;
    foreignPush)
      git_fx "$w" reviewer-kai checkout -q "$BRANCH"
      overlay overlays/foreign-push "$w"
      git_fx "$w" reviewer-kai add -A
      git_fx "$w" reviewer-kai commit -q -m "tweak: pretty-print the json output"
      forge seed advance "$link" --sha "$(git -C "$w" rev-parse "$BRANCH")" >/dev/null
      forge seed checks "$link" --state passing --passed 7 >/dev/null ;;
    secret)
      local out key token; out="$(forge seed thread "$link" --author maintainer-ines --association OWNER --kind thread --path src/cli.js \
        --body "Please read the API endpoint and its credential from a config file, so the tool can post the counts.")"
      key="$(jq -r .key <<<"$out")"
      # built from parts so no tracked file holds a token-shaped string
      token="ghp_$(printf 'FIXTURE%.0s' 1 2 3 4 5 | cut -c1-34)00"
      git_fx "$w" tally-dev checkout -q -b fixture/fix-with-token "$BRANCH"
      printf '// API settings.\nexport const config = { endpoint: "https://counts.forge.test/v1", token: "%s" };\n' "$token" >"$w/src/config.js"
      git_fx "$w" tally-dev add -A
      git_fx "$w" tally-dev commit -q -m "feat: read the API settings from a config" -m "Sindri-Comment: $key"
      git_fx "$w" tally-dev checkout -q "$BRANCH"
      threads="$(jq -nc --arg key "$key" '[{id: "change", key: $key, expect: {queue: "comment", type: "change", note: "the candidate fix on fixture/fix-with-token commits a planted token: gate.pr.secret-scan must refuse its push"}}]')" ;;
  esac

  mkdir -p "$dir/handoff"
  local f
  for f in packet.json pr.json green.json body.md index.json; do render "$SET/handoff/$f" "$dir/handoff/$f" "$proven" "$base" "$stack"; done
  render "$SET/phase-packet.json" "$dir/phase-packet.json" "$proven" "$base" "$stack"
  jq -n '{workflow: {pr: {bots: ["scanbot[bot]"]}}}' >"$dir/profile-bots.json"
  jq -n --arg v "$v" --arg link "$link" --arg head "$(git -C "$w" rev-parse "$BRANCH")" --arg proven "$proven" \
        --argjson flaky "$flaky" --argjson threads "$threads" --arg basePr "$base_pr" --arg repo "$repo" \
        --slurpfile bots <(jq -c .profileBots "$SET/comments.json") \
     '{variant: $v, repo: $repo, link: $link, head: $head, provenSha: $proven, flaky: $flaky, profileBots: $bots[0], threads: $threads}
      + (if $basePr != "" then {stackBase: {number: ($basePr | tonumber), branch: "feature/json-base"}} else {} end)' >"$dir/expected.json"

  echo "variant: $v"
  echo "link: $link"
  echo "workspace: $w"
  echo "handoff: $dir/handoff"
  echo "phase-packet: $dir/phase-packet.json"
  echo "expected: $dir/expected.json"
}

remove_variant() { # <repo name>
  local dir="$HOME_DIR/$1"
  [ -n "$1" ] && [ -d "$dir" ] && rm -r -- "$dir"
  if [ -f "$(WORLD)" ]; then
    local tmp; tmp="$(mktemp "$HOME_DIR/state/forge/file/patch.XXXXXX")"
    jq --arg o "$OWNER" --arg r "$1" '.pulls |= map(select((.owner==$o and .repo==$r) | not)) | .repos |= map(select(.key != ("forge.test/" + $o + "/" + $r)))' "$(WORLD)" >"$tmp" && mv "$tmp" "$(WORLD)"
  fi
  return 0
}

print_env() {
  echo "forge-env: AW_STATE_DIR=$HOME_DIR/state AW_FORGE_ADAPTER=file AW_FORGE_HOSTS=$HOST"
}

# --- up --github (owner only) --------------------------------------------------------------------------------------------
up_github() { # <owner/repo> <variant> [--dry-run]
  local slug="$1" v="$2" dry="${3:-}" repo dir w url token_header
  [[ "$slug" =~ ^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$ ]] || die "--github needs <owner/repo>, got '$slug'"
  known_variant "$v" || die "unknown variant: $v"
  [ -n "${AW_FORGE_TOKEN:-}" ] || die "--github needs AW_FORGE_TOKEN (a token with contents and pull-request write on the scratch repository)"
  [ "${FORGE_FIXTURE_GITHUB_SCRATCH:-}" = "$slug" ] || die "--github writes branches and pull requests: set FORGE_FIXTURE_GITHUB_SCRATCH=$slug to confirm $slug is a scratch repository"
  guard; need git; need jq
  repo="$(kebab "$v")"; dir="$HOME_DIR/$repo"; w="$dir/work"
  [ ! -d "$dir" ] || rm -r -- "$dir"
  mkdir -p "$dir"; build_repo "$v" "$w"
  url="https://github.com/$slug.git"
  local -a pairs=("main:fixture/$v/base" "$BRANCH:fixture/$v/head")
  [ "$v" != stacked ] || pairs+=("$BASE_BRANCH:fixture/$v/base-pr")
  local pair
  for pair in "${pairs[@]}"; do
    echo "push: $(git -C "$w" rev-parse "${pair%%:*}") -> $slug refs/heads/${pair#*:}"
  done
  echo "open: a pull request from fixture/$v/head on $slug"
  if [ "$dry" = --dry-run ]; then echo "dry run: nothing was pushed or opened"; return 0; fi
  need dotnet
  ensure_bin
  # the credential travels in the environment of this one git process, never in an argument or a log
  token_header="Authorization: basic $(printf 'x-access-token:%s' "$AW_FORGE_TOKEN" | base64 | tr -d '\n')"
  for pair in "${pairs[@]}"; do
    GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_SYSTEM=/dev/null GIT_TERMINAL_PROMPT=0 GIT_CONFIG_COUNT=1 \
      GIT_CONFIG_KEY_0="http.https://github.com/.extraheader" GIT_CONFIG_VALUE_0="$token_header" \
      git -C "$w" -c core.hooksPath=/dev/null push -q "$url" "refs/heads/${pair%%:*}:refs/heads/${pair#*:}" || die "push of ${pair#*:} failed"
  done
  local head_sha base_ref="fixture/$v/base" out
  [ "$v" != stacked ] || base_ref="fixture/$v/base-pr"
  head_sha="$(git -C "$w" rev-parse "$BRANCH")"
  if [ "$v" = stacked ]; then
    out="$(AW_FORGE_ADAPTER=github AW_FORGE_HOSTS=github.com "${FORGE_CMD[@]}" open --repo "github.com/$slug" --head "fixture/$v/base-pr" --base "fixture/$v/base" \
      --head-sha "$(git -C "$w" rev-parse "$BASE_BRANCH")" --title "fixture: stacked base" </dev/null)" || die "opening the base pull request failed"
    echo "link: $(jq -r .link <<<"$out")"
  fi
  out="$(AW_FORGE_ADAPTER=github AW_FORGE_HOSTS=github.com "${FORGE_CMD[@]}" open --repo "github.com/$slug" --head "fixture/$v/head" --base "$base_ref" \
    --head-sha "$head_sha" --title "fixture: print results as JSON with --json" --body "A fixture pull request of scripts/forge-fixture.sh. Safe to close." </dev/null)" || die "opening the pull request failed"
  echo "link: $(jq -r .link <<<"$out")"
  if [ "$v" = comments ]; then
    echo "threads to add by hand from a second account (the github adapter cannot seed them):"
    jq -r '.threads[] | "  - [" + .author + (if .bot then ", bot" else "" end) + "] " + .body' "$SET/comments.json"
  fi
  echo "scratch branches: fixture/$v/*  (delete them to close the pull requests)"
}

# --- verbs -------------------------------------------------------------------------------------------------------------------
verb="${1:-}"
case "$verb" in
  "") usage >&2; exit 2 ;;
  -h | --help) usage; exit 0 ;;
esac
shift

case "$verb" in
  list)
    need jq
    jq -r '.variants | to_entries[] | "\(.key)\t\(.value)"' "$SET/variants.json" | awk -F'\t' '{ printf "%-12s %s\n", $1, $2 }'
    ;;
  up)
    mode="${1:-}"; [ -n "$mode" ] || die "up needs --fake <variant> or --github <owner/repo> <variant>"
    shift
    case "$mode" in
      --fake)
        target="${1:-}"; [ -n "$target" ] || die "up --fake needs a variant (or 'all')"
        [ "$target" = all ] || known_variant "$target" || die "unknown variant: $target"
        [ $# -le 1 ] || die "up --fake takes one variant"
        guard; need git; need jq; mkdir -p "$HOME_DIR/state/forge/file"; ensure_bin
        if [ "$target" = all ]; then for v in $VARIANTS; do up_variant "$v"; echo; done; else up_variant "$target"; fi
        print_env
        ;;
      --github)
        [ -n "${1:-}" ] || die "up --github needs <owner/repo> and a variant"
        slug="$1"; v="${2:-}"; [ -n "$v" ] || die "up --github needs a variant after $slug"
        case "${3:-}" in "" | --dry-run) ;; *) die "unknown argument: $3" ;; esac
        up_github "$slug" "$v" "${3:-}"
        ;;
      *) die "up takes --fake or --github, got '$mode'" ;;
    esac
    ;;
  event)
    ev="${1:-}"; [ "$ev" = merge-base ] || die "event takes merge-base, got '${ev:-nothing}'"
    shift; variant=stacked
    if [ "${1:-}" = --variant ]; then variant="${2:-}"; [ -n "$variant" ] || die "--variant needs a name"; fi
    known_variant "$variant" || die "unknown variant: $variant"
    guard; need jq
    repo="$(kebab "$variant")"; exp="$HOME_DIR/$repo/expected.json"
    [ -f "$exp" ] || die "$variant is not up"
    jq -e '.stackBase' "$exp" >/dev/null 2>&1 || die "$variant has no base pull request to merge (only stacked has one)"
    num="$(jq -r .stackBase.number "$exp")"
    sha="$(git -C "$HOME_DIR/$repo/work" rev-parse "$BASE_BRANCH")"
    patch_pull "$repo" "$num" '.status = 3 | .mergedSha = $sha' --arg sha "$sha"
    echo "merged: https://$HOST/$OWNER/$repo/pull/$num"
    ;;
  status)
    guard; need jq
    found=0
    for exp in "$HOME_DIR"/*/expected.json; do
      [ -f "$exp" ] || continue
      found=1; echo "$(jq -r .variant "$exp"): $(jq -r .link "$exp")"
    done
    [ "$found" = 1 ] || { echo "nothing is up in $HOME_DIR"; exit 1; }
    ;;
  down)
    all=0
    case "${1:-}" in "") ;; --all) all=1 ;; *) die "down takes --all or nothing" ;; esac
    guard
    if [ ! -d "$HOME_DIR" ]; then echo "nothing to remove"; exit 0; fi
    if [ "$all" = 1 ]; then
      rm -r -- "$HOME_DIR"
    else
      # only what this script makes: the world and the variant folders, never other entries of the home
      [ ! -d "$HOME_DIR/state" ] || rm -r -- "$HOME_DIR/state"
      for v in $VARIANTS; do
        [ ! -d "$HOME_DIR/$(kebab "$v")" ] || rm -r -- "$HOME_DIR/$(kebab "$v")"
      done
    fi
    echo "removed"
    ;;
  *) die "unknown verb: $verb" ;;
esac
