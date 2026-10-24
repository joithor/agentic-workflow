#!/usr/bin/env bash
# scripts/images-lock.sh — check the shape of deploy/images.lock, the pins of every third-party image the Sindri deployment pulls
# (spec docs/superpowers/specs/2026-10-23-sindri-deployable-context-design.md decision K11), and that the Dockerfile uses only them.
#
# Usage: scripts/images-lock.sh check [--file F] [--dockerfile D]
#   check  every row is well formed; every `image` repository:tag has one `index` row and both linux/amd64 and linux/arm64
#          rows, each once; every `todo` row names a repository without an `image` row; and every `FROM` and `COPY --from=` in the
#          Dockerfile that names an image (not a build stage) is <repository>:<tag>@<digest> with exactly an `index` row of the lock.
#          Prints one line per problem 'IMAGES-LOCK-00n <line>: <what>', then the unresolved `todo` rows as a note (they pass).
# Rows (whitespace-separated; '#' comments and blank lines are ignored):
#   image <repository> <tag> <platform> <sha256:digest>   platform is index, linux/amd64 or linux/arm64
#   todo <repository> <phase>                             not resolved yet, to be filled in by <phase>
#   A repository matches ^[a-z0-9][a-z0-9._/-]*$ and a tag ^[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}$; a line holds no NUL, CR, VT or FF.
# Codes: 001 unknown row kind, 002 a malformed value, 003 platforms (missing or repeated), 004 a todo row for a pinned repository,
#        005 no image rows, 006 the Dockerfile names an image the lock does not pin (or pins another digest), 007 a Dockerfile
#        image reference without a digest.
# Exit: 0 ok, 1 a check failed, 2 usage or I/O error.
# Bash 3.2 compatible (macOS /bin/bash).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

usage() { sed -n '5,5p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//' >&2; exit 2; }

cmd="${1:-}"; [ "$cmd" = "check" ] || usage; shift
file="$ROOT/deploy/images.lock"
dockerfile="$ROOT/Dockerfile"
while [ $# -gt 0 ]; do
  case "$1" in
    --file) file="${2:?--file needs a path}"; shift 2 ;;
    --dockerfile) dockerfile="${2:?--dockerfile needs a path}"; shift 2 ;;
    *) usage ;;
  esac
done
[ -r "$file" ] || { echo "images-lock: cannot read $file" >&2; exit 2; }

if [ "$(LC_ALL=C tr -d '\000' <"$file" | wc -c)" -ne "$(LC_ALL=C wc -c <"$file")" ]; then
  echo "IMAGES-LOCK-002 0: the file contains a NUL byte"
  exit 1
fi
ctl="$(LC_ALL=C grep -n $'[\r\v\f]' "$file" | cut -d: -f1 | head -n1 || true)"
if [ -n "$ctl" ]; then
  echo "IMAGES-LOCK-002 $ctl: the line contains a CR, VT or FF control character"
  exit 1
fi

failed=0
out="$(LC_ALL=C awk '
  function bad(code, msg) { printf "%s %d: %s\n", code, NR, msg; failed = 1 }
  /^[[:space:]]*(#|$)/ { next }
  $1 == "image" {
    if (NF != 5) { bad("IMAGES-LOCK-002", "image wants 4 fields (repository tag platform digest), got " (NF - 1)); next }
    if ($2 !~ /^[a-z0-9][a-z0-9._\/-]*$/) bad("IMAGES-LOCK-002", "repository \"" $2 "\" does not match ^[a-z0-9][a-z0-9._/-]*$")
    if ($3 !~ /^[A-Za-z0-9_][A-Za-z0-9_.-]*$/ || length($3) > 128) bad("IMAGES-LOCK-002", "tag \"" $3 "\" does not match ^[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}$")
    if ($4 != "index" && $4 != "linux/arm64" && $4 != "linux/amd64") { bad("IMAGES-LOCK-002", "platform \"" $4 "\" is not index, linux/arm64 or linux/amd64"); next }
    if ($5 !~ /^sha256:[0-9a-f]+$/ || length($5) != 71) bad("IMAGES-LOCK-002", "digest is not sha256: and 64 lowercase hex digits")
    k = $2 ":" $3
    if ((k SUBSEP $4) in seen) bad("IMAGES-LOCK-003", k " " $4 " is listed twice")
    seen[k, $4] = 1; repos[k] = 1; repoof[k] = $2; images++
    next
  }
  $1 == "todo" {
    if (NF != 3) { bad("IMAGES-LOCK-002", "todo wants 2 fields (repository phase), got " (NF - 1)); next }
    if ($2 !~ /^[a-z0-9][a-z0-9._\/-]*$/) bad("IMAGES-LOCK-002", "repository \"" $2 "\" does not match ^[a-z0-9][a-z0-9._/-]*$")
    todo[$2] = $3
    next
  }
  { bad("IMAGES-LOCK-001", "unknown row kind \"" $1 "\"") }
  END {
    if (images == 0) { print "IMAGES-LOCK-005 0: no image rows"; failed = 1 }
    for (r in repos) {
      if (!((r SUBSEP "index") in seen)) { printf "IMAGES-LOCK-003 %s: no index row\n", r; failed = 1 }
      if (!((r SUBSEP "linux/arm64") in seen)) { printf "IMAGES-LOCK-003 %s: no linux/arm64 row\n", r; failed = 1 }
      if (!((r SUBSEP "linux/amd64") in seen)) { printf "IMAGES-LOCK-003 %s: no linux/amd64 row\n", r; failed = 1 }
      if (repoof[r] in todo) { printf "IMAGES-LOCK-004 %s: has an image row and a todo row\n", r; failed = 1 }
    }
    exit failed
  }
' "$file")" || failed=1
[ -z "$out" ] || printf '%s\n' "$out"

# The Dockerfile: every external image reference is pinned to an index row of the lock.
if [ -r "$dockerfile" ]; then
  stages=" "
  n=0
  while IFS= read -r line; do
    n=$((n + 1))
    ref=""
    case "$line" in
      FROM\ *|from\ *)
        # FROM [--platform=x] image[:tag][@digest] [AS name]
        set -- $line; shift
        while [ $# -gt 0 ] && [ "${1#--}" != "$1" ]; do shift; done
        ref="${1:-}"
        if [ "${2:-}" = "AS" ] || [ "${2:-}" = "as" ]; then newstage="${3:-}"; else newstage=""; fi
        case "$stages" in *" $ref "*) ref="" ;; esac       # a build stage, not an image
        [ -z "$newstage" ] || stages="$stages$newstage "
        [ "$ref" != "scratch" ] || ref=""
        ;;
      COPY\ *--from=*)
        for w in $line; do case "$w" in --from=*) ref="${w#--from=}" ;; esac; done
        case "$stages" in *" $ref "*) ref="" ;; esac
        ;;
    esac
    [ -n "$ref" ] || continue
    case "$ref" in
      *@sha256:*)
        base="${ref%@*}"; digest="${ref#*@}"
        repo="${base%:*}"; tag="${base##*:}"
        # Docker Hub short names carry no registry; the lock holds the full docker.io/ name
        if ! grep -qE "^image[[:space:]]+(docker\.io/)?${repo//./\\.}[[:space:]]+${tag//./\\.}[[:space:]]+index[[:space:]]+$digest([[:space:]]|$)" "$file"; then
          echo "IMAGES-LOCK-006 $n: $ref is not an index row of $(basename "$file")"; failed=1
        fi
        ;;
      *) echo "IMAGES-LOCK-007 $n: $ref has no @sha256 digest"; failed=1 ;;
    esac
  done <"$dockerfile"
fi

grep -E '^todo[[:space:]]' "$file" | awk '{ printf "note: %s is not pinned yet (%s)\n", $2, $3 }' || true
exit "$failed"
