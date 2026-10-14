#!/usr/bin/env bash
# denylist-check.sh <file>... — finds work identifiers in PR text without revealing them.
#
# The denylist is injected, never passed as an argument and never a path baked into code or briefs, so the check
# works unchanged when a module runs as its own service or container (mount the file as the process user with mode
# 0400 or 0600, or use env:). AW_DENYLIST is a pointer in two of the forms Sindri's context secret pointers use:
#   file:<absolute path>  a mounted file; refused unless it is a regular file, mode 0600 or stricter (no group, other
#                         or execute bits), owned by the current user
#   env:<VAR>             the entries are in that environment variable, newline-separated
# Anything else, a bare path included, is refused. The owner sets AW_DENYLIST in their own environment.
#
# The list holds one entry per line (team keys such as "ABC-", the Slack workspace domain, linear.app/<workspace>,
# project names). Blank lines and lines starting with # are ignored (CRLF too); each entry is a case-insensitive
# fixed string (a substring, so write a team key with its dash). Stage owners run the check before marking a PR ready,
# over the PR title, body and commit messages written to files and over evidence.md and owner-check.md (context spec,
# "Work data stays with the owner"); they never open the list.
#
# Output: one "<file>:<line>" per line that holds an entry, then "denylist-check: N hit(s) in M file(s) checked".
# It never prints an entry or the text of a line, so the agent running it never reads the identifiers; a hit is
# removed by rewording the flagged line.
# Exit codes: 0 no hits; 1 hits; 2 fails closed: AW_DENYLIST unset, empty or not a valid pointer, the target missing,
# unreadable or too open, no entries, an input file missing, or a usage error.
set -uo pipefail

die() { echo "denylist-check: $*" >&2; exit 2; }
usage() { echo "usage: scripts/denylist-check.sh <file>...   (the list comes from AW_DENYLIST=file:<absolute path>|env:<VAR>)" >&2; exit 2; }
[ "$#" -ge 1 ] || usage

ptr="${AW_DENYLIST:-}"
if [ -z "$ptr" ]; then
  die "AW_DENYLIST is not set; set AW_DENYLIST to file:<absolute path> or env:<VAR> (the denylist is no longer an argument; failing closed)"
fi
for f in "$@"; do
  [ -f "$f" ] && [ -r "$f" ] || die "$f is not a readable file"
done

raw=""; entries=""
trap 'rm -f "$raw" "$entries"' EXIT
raw="$(mktemp "${TMPDIR:-/tmp}/denylist-check.XXXXXX")" || exit 2
entries="$(mktemp "${TMPDIR:-/tmp}/denylist-check.XXXXXX")" || exit 2
chmod 600 "$raw" "$entries"

# Mode (octal) and owner uid of a path, following links: GNU stat first, then BSD/macOS.
mode_and_owner() { stat -L -c '%a %u' "$1" 2>/dev/null || stat -L -f '%Lp %u' "$1" 2>/dev/null; }

src=""
case "$ptr" in
  file:/*)
    path="${ptr#file:}"
    [ -e "$path" ] || die "the denylist is missing: the file AW_DENYLIST points at does not exist (the owner writes it; failing closed)"
    [ -f "$path" ] || die "the denylist is not a regular file (AW_DENYLIST file: pointer)"
    [ -r "$path" ] || die "the denylist is unreadable (AW_DENYLIST file: pointer; failing closed)"
    mo="$(mode_and_owner "$path")" || die "cannot stat the denylist file (failing closed)"
    mode="${mo% *}"; owner="${mo#* }"
    case "$mode" in ""|*[!0-7]*) die "cannot read the denylist file's mode (failing closed)" ;; esac
    if [ $(( 8#$mode & 8#177 )) -ne 0 ]; then
      die "the denylist file must be mode 0600 or stricter (chmod 600 it); it is $mode, so it was not read"
    fi
    [ "$owner" = "$(id -u)" ] || die "the denylist file must be owned by the current user, so it was not read"
    for f in "$@"; do
      [ "$f" -ef "$path" ] && die "the denylist is injected through AW_DENYLIST; it is not an input file (the old form <denylist> <file>... is gone)"
    done
    src="$path"
    ;;
  file:*)
    die "AW_DENYLIST file: pointer needs an absolute path (file:<absolute path>)"
    ;;
  env:*)
    var="${ptr#env:}"
    case "$var" in
      ""|[0-9]*|*[!A-Za-z0-9_]*) die "AW_DENYLIST env: pointer needs a variable name (env:<VAR>)" ;;
    esac
    [ "$var" != "AW_DENYLIST" ] || die "AW_DENYLIST env: pointer cannot name AW_DENYLIST itself (env:<VAR>)"
    # printenv sees only the exported environment, never this script's own variables.
    val="$(printenv "$var")" && [ -n "$val" ] || die "the environment variable $var named by AW_DENYLIST is unset, empty or not exported (failing closed)"
    printf '%s\n' "$val" >"$raw" || exit 2
    src="$raw"
    ;;
  *)
    die "AW_DENYLIST must be file:<absolute path> or env:<VAR>; a bare path or any other scheme is refused (failing closed)"
    ;;
esac

# Entries without CR, surrounding blanks, comments or empty lines (an empty pattern would match every line).
sed -e 's/\r$//' -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//' "$src" | grep -v -e '^#' -e '^$' >"$entries"
if [ ! -s "$entries" ]; then
  die "the denylist has no entries (failing closed)"
fi

hits=0
for f in "$@"; do
  # -n prints "<line>:<text>"; only the line number is kept, so the matched text never reaches the output.
  lines="$(grep -a -n -i -F -f "$entries" -- "$f" | cut -d: -f1)"
  rc=$?   # pipefail: grep's 1 (no match) or 2 (an error)
  [ "$rc" -le 1 ] || { echo "denylist-check: grep failed on $f" >&2; exit 2; }
  for n in $lines; do
    echo "$f:$n"
    hits=$((hits + 1))
  done
done

plural() { [ "$1" -eq 1 ] && echo "$1 $2" || echo "$1 $2s"; }
echo "denylist-check: $(plural "$hits" hit) in $(plural "$#" file) checked"
[ "$hits" -eq 0 ]
