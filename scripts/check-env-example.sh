#!/usr/bin/env bash
# scripts/check-env-example.sh — every environment variable the repo's code reads is documented in
# .env.example (readiness spec §6 W5). Exit 0 when it is, 1 on drift, 2 when the root, .env.example or a
# temp directory is missing (other tool failures are not detected separately).
#
# What counts as a read (a heuristic scan, not a proof):
#   TS / JS   process.env.<NAME>, process.env["<NAME>"], <x>.env.<NAME>, <x>.env['<NAME>'],
#             <x>.env.get('<NAME>') (the mods' $.env), in .ts .tsx .mts .cts .js .mjs .cjs
#   C#        Environment.GetEnvironmentVariable("<NAME>"), environment("<NAME>") (the Compose lambda)
#   shell     ${<NAME>:-…}, ${<NAME>-…}, ${<NAME>:=…}, ${<NAME>:?…}, ${<NAME>:+…} (an override with a default)
# Only UPPER_CASE names. Not scanned: test files and directories (tests/, test/, __tests__/, fixtures/,
# *.test.*, *.spec.*), node_modules/, dist/, bin/, obj/, .git/, .claude/. A name read only indirectly
# (through a configured name, a list or a pointer such as `env:NAME`) is invisible to the scan: document
# it with "(indirect: <where>)" in a comment of its entry's block (blank-line separated) so the stale
# check skips every entry in that block.
#
# Rules:
#   ENV-EXAMPLE-001  a name is read but neither in .env.example nor in scripts/env-scan-ignore.txt
#   ENV-EXAMPLE-002  a name is in .env.example but nothing reads it (and it isn't marked indirect)
#   ENV-EXAMPLE-003  scripts/env-scan-ignore.txt: an entry without a reason, or a name also in .env.example
#   ENV-EXAMPLE-004  a secret-named entry (KEY, TOKEN, SECRET, PASSWORD, CREDENTIAL) carries a value
#
# .env.example format: each entry is one or more "# what it is" lines, then "NAME=value" (a safe default)
# or "# NAME=" (unset by default). scripts/env-scan-ignore.txt: "NAME # reason" per line, for names the
# OS, the shell or CI sets (HOME, PATH, CI) and for script-internal UPPER_CASE locals the scan can't tell
# from overrides.
#
# Usage: scripts/check-env-example.sh [--root DIR] [--list]
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LIST=0
while [ $# -gt 0 ]; do
  case "$1" in
    --root) ROOT="${2:?--root needs a directory}"; shift 2 ;;
    --list) LIST=1; shift ;;
    *) echo "usage: $0 [--root DIR] [--list]" >&2; exit 2 ;;
  esac
done
cd "$ROOT" || { echo "ENV-EXAMPLE: cannot cd to $ROOT" >&2; exit 2; }
[ -f .env.example ] || { echo "ENV-EXAMPLE: .env.example is missing in $ROOT" >&2; exit 2; }
IGNORE_FILE=scripts/env-scan-ignore.txt

TMP="$(mktemp -d "${TMPDIR:-/tmp}/check-env-example.XXXXXX")" || exit 2
trap 'rm -rf "$TMP"' EXIT

# find(1) with the pruned directories; "$@" is the -name expression for the file kinds.
source_files() {
  find . \( -name node_modules -o -name dist -o -name bin -o -name obj -o -name .git -o -name .claude \
    -o -name tests -o -name test -o -name __tests__ -o -name fixtures \) -prune \
    -o -type f \( "$@" \) ! -name '*.test.*' ! -name '*.spec.*' -print | LC_ALL=C sort
}

# Each scan prints "NAME<TAB>path<TAB>line".
scan() { # $1 = grep -E pattern, $2 = sed script that reduces a match to the name; files on stdin
  local f
  while IFS= read -r f; do
    grep -HnoE -- "$1" "$f" 2>/dev/null | sed -E 's|^\./||' | while IFS= read -r hit; do
      local path="${hit%%:*}" rest="${hit#*:}"
      local line="${rest%%:*}" match="${rest#*:}"
      printf '%s\t%s\t%s\n' "$(printf '%s' "$match" | sed -E "$2")" "$path" "$line"
    done
  done
}

{
  source_files -name '*.ts' -o -name '*.tsx' -o -name '*.mts' -o -name '*.cts' \
    -o -name '*.js' -o -name '*.mjs' -o -name '*.cjs' |
    scan "(^|[^A-Za-z0-9_\$])env(\\.[A-Z][A-Z0-9_]*|\\[[\"'][A-Z][A-Z0-9_]*[\"']\\]|\\.get\\([\"'][A-Z][A-Z0-9_]*[\"'])" \
      "s/.*env(\\.get\\([\"']|\\.|\\[[\"'])//; s/[]\"']+\$//"
  source_files -name '*.cs' |
    scan "(GetEnvironmentVariable|environment)\\(\"[A-Z][A-Z0-9_]*\"" 's/.*\("//; s/"$//'
  source_files -name '*.sh' -o -name '*.bash' |
    scan '\$\{[A-Z][A-Z0-9_]*:?[-=?+]' 's/^\$\{//; s/:?[-=?+]$//'
} > "$TMP/reads.raw"
# First location per name: paths in byte order, then line numbers numerically, so the report is stable.
LC_ALL=C sort -t "$(printf '\t')" -k1,1 -k2,2 -k3,3n "$TMP/reads.raw" |
  awk -F'\t' '!seen[$1]++ { printf "%s\t%s:%s\n", $1, $2, $3 }' > "$TMP/reads"

# .env.example → "NAME<TAB>indirect(0|1)<TAB>has-value(0|1)" per entry. A block (blank-line separated) is
# held until it ends, so an "(indirect" comment anywhere in it covers every entry in it.
awk '
  function flush(  i) { for (i = 1; i <= n; i++) printf "%s\t%d\t%d\n", names[i], indirect, values[i]; n = 0; indirect = 0 }
  /^#[[:space:]]*[A-Z][A-Z0-9_]*=/ || /^[A-Z][A-Z0-9_]*=/ {
    line = $0; sub(/^#[[:space:]]*/, "", line)
    name = line; sub(/=.*/, "", name)
    value = line; sub(/^[^=]*=/, "", value); gsub(/[[:space:]]/, "", value)
    names[++n] = name; values[n] = (value != "")
    next
  }
  /^#/ { if (index($0, "(indirect") > 0) indirect = 1; next }
  /^[[:space:]]*$/ { flush(); next }
  END { flush() }
' .env.example | LC_ALL=C sort -u > "$TMP/documented"

# scripts/env-scan-ignore.txt → "NAME<TAB>has-reason(0|1)".
if [ -f "$IGNORE_FILE" ]; then
  awk '
    /^[[:space:]]*(#|$)/ { next }
    { name = $1; reason = $0; sub(/^[^#]*#?/, "", reason); gsub(/[[:space:]]/, "", reason)
      printf "%s\t%d\n", name, (index($0, "#") > 0 && reason != "") }
  ' "$IGNORE_FILE" | LC_ALL=C sort -u > "$TMP/ignored"
else
  : > "$TMP/ignored"
fi

if [ "$LIST" -eq 1 ]; then
  cat "$TMP/reads"
  exit 0
fi

awk -F'\t' -v ignore_file="$IGNORE_FILE" '
  # A name listed twice keeps its worst case: any copy with a value, any ignore entry without a reason.
  FILENAME == ARGV[1] { doc[$1] = 1; ind[$1] = $2; val[$1] = val[$1] || $3; next }
  FILENAME == ARGV[2] { why[$1] = (($1 in ign) ? why[$1] : 1) && $2; ign[$1] = 1; next }
  { read[$1] = $2 }
  function problem(text) { print text | sorted; bad++ }
  END {
    sorted = "LC_ALL=C sort"
    bad = 0
    for (n in read) if (!(n in doc) && !(n in ign)) {
      problem(sprintf("ENV-EXAMPLE-001: %s is read at %s but is not in .env.example — fix: add a \"# <what it does>\" line and \"# %s=\" to .env.example, or \"%s # <reason>\" to %s if the OS, shell or CI sets it", n, read[n], n, n, ignore_file))
    }
    for (n in doc) if (!(n in read) && !ind[n] && !(n in ign)) {
      problem(sprintf("ENV-EXAMPLE-002: %s is in .env.example but nothing reads it — fix: remove the entry, or mark its comment \"(indirect: <where it is read>)\" if it is read through a configured name", n))
    }
    for (n in ign) {
      if (!why[n]) problem(sprintf("ENV-EXAMPLE-003: %s in %s has no reason — fix: write \"%s # <why the scan should skip it>\"", n, ignore_file, n))
      if (n in doc) problem(sprintf("ENV-EXAMPLE-003: %s is both in %s and in .env.example — fix: keep one (document a variable users set; ignore one the OS, shell or CI sets)", n, ignore_file))
    }
    for (n in doc) if (val[n] && n ~ /(KEY|TOKEN|SECRET|PASSWORD|PASSWD|CREDENTIAL)/) {
      problem(sprintf("ENV-EXAMPLE-004: %s in .env.example carries a value — fix: leave secret-named entries empty (\"# %s=\")", n, n))
    }
    close(sorted)
    nread = 0; for (n in read) nread++
    nign = 0; for (n in ign) nign++
    if (bad > 0) { printf "ENV-EXAMPLE: %d problem(s); %d names read\n", bad, nread; exit 1 }
    printf "ENV-EXAMPLE: %d names read, all documented (%d ignored by %s)\n", nread, nign, ignore_file
  }
' "$TMP/documented" "$TMP/ignored" "$TMP/reads"
