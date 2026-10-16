#!/usr/bin/env bash
# scripts/onyx-lock.sh — check the shape of context/onyx.lock, the pins Sindri-managed Onyx is built from
# (spec docs/superpowers/specs/2026-10-16-sindri-onyx-managed-design.md §6).
#
# Usage: scripts/onyx-lock.sh check [--file F]
#   check  every row is well formed, there is exactly one version row, every image has both platforms once, and the two
#          need rows are present once. Prints one line per problem: 'ONYX-LOCK-00n <line>: <what>'.
# Rows (whitespace-separated; '#' comments and blank lines are ignored):
#   version <tag> <commit-sha-40>                    exactly one; tag is vX.Y.Z
#   file <path under deployment/> <sha256-64>        a deployment file Sindri fetches at <commit> and checks
#   image <repository> <platform> <sha256:digest>    every image, for linux/arm64 and linux/amd64
#   need disk-bytes <n>                              exactly one
#   need docker-memory-bytes <n>                     exactly one
#   A repository matches ^[a-z0-9][a-z0-9._/-]*$; a file path is relative, with no backslash, empty or .. segment and no NUL;
#   no file path is listed twice; a need is 1 to 18 digits and positive (it fits a long).
# Parity: OnyxPins.Parse (src/Sindri/Aw.Sindri.Core/Onyx/OnyxPins.cs) refuses exactly the files this check refuses, as far as the
#   fixtures of scripts/tests/onyx-lock.test.sh and tests/Aw.Sindri.Tests/OnyxPinsTests/OnyxPinsParseTests.cs go: both suites
#   run the same malformed cases (need of 19 digits, duplicate file or image row, bad repository name, bad file paths, NUL).
#   The C# parser reports every refusal as SND-ONYX-004 where this script uses the codes below.
# Codes: 001 version row (missing, repeated, bad tag), 002 a malformed value, 003 image platforms (missing or repeated),
#        004 need rows (missing, repeated, not a number), 005 unknown row kind or need name.
# Exit: 0 ok, 1 a check failed, 2 usage or I/O error.
# Bash 3.2 compatible (macOS /bin/bash).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

usage() { sed -n '4,5p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//' >&2; exit 2; }

cmd="${1:-}"; [ "$cmd" = "check" ] || usage; shift
file="$ROOT/context/onyx.lock"
while [ $# -gt 0 ]; do
  case "$1" in
    --file) file="${2:?--file needs a path}"; shift 2 ;;
    *) usage ;;
  esac
done
[ -r "$file" ] || { echo "onyx-lock: cannot read $file" >&2; exit 2; }

# A NUL byte anywhere is refused here, before awk (which would truncate at it); OnyxPins.Parse refuses it the same way.
if [ "$(LC_ALL=C tr -d '\000' <"$file" | wc -c)" -ne "$(LC_ALL=C wc -c <"$file")" ]; then
  echo "ONYX-LOCK-002 0: the file contains a NUL byte"
  exit 1
fi

awk '
  function bad(code, msg) { printf "%s %d: %s\n", code, NR, msg; failed = 1 }
  function hex(s, n) { return s ~ /^[0-9a-f]+$/ && length(s) == n }
  /^[[:space:]]*(#|$)/ { next }
  $1 == "version" {
    versions++
    if (NF != 3) { bad("ONYX-LOCK-001", "version wants 2 fields (tag commit), got " (NF - 1)); next }
    if ($2 !~ /^v[0-9]+\.[0-9]+\.[0-9]+$/) bad("ONYX-LOCK-001", "tag \"" $2 "\" is not vX.Y.Z")
    if (!hex($3, 40)) bad("ONYX-LOCK-002", "commit is not 40 lowercase hex digits")
    next
  }
  $1 == "file" {
    if (NF != 3) { bad("ONYX-LOCK-002", "file wants 2 fields (path sha256), got " (NF - 1)); next }
    if ($2 ~ /^\// || $2 ~ /\\/ || $2 ~ /\/\// || $2 ~ /\/$/ || $2 ~ /(^|\/)\.\.(\/|$)/) bad("ONYX-LOCK-002", "path \"" $2 "\" is not a relative path of non-empty segments without .. or backslash inside deployment/")
    if ($2 in files) bad("ONYX-LOCK-002", "path " $2 " is listed twice")
    files[$2] = 1
    if (!hex($3, 64)) bad("ONYX-LOCK-002", "sha256 is not 64 lowercase hex digits")
    next
  }
  $1 == "image" {
    if (NF != 4) { bad("ONYX-LOCK-002", "image wants 3 fields (repository platform digest), got " (NF - 1)); next }
    if ($2 !~ /^[a-z0-9][a-z0-9._\/-]*$/) bad("ONYX-LOCK-002", "repository \"" $2 "\" does not match ^[a-z0-9][a-z0-9._/-]*$")
    if ($3 != "linux/arm64" && $3 != "linux/amd64") { bad("ONYX-LOCK-002", "platform \"" $3 "\" is not linux/arm64 or linux/amd64"); next }
    if ($4 !~ /^sha256:[0-9a-f]+$/ || length($4) != 71) bad("ONYX-LOCK-002", "digest is not sha256: and 64 lowercase hex digits")
    if (($2 SUBSEP $3) in seen) bad("ONYX-LOCK-003", $2 " " $3 " is listed twice")
    seen[$2, $3] = 1; repos[$2] = 1
    next
  }
  $1 == "need" {
    if (NF != 3) { bad("ONYX-LOCK-004", "need wants 2 fields (name number), got " (NF - 1)); next }
    if ($2 != "disk-bytes" && $2 != "docker-memory-bytes") { bad("ONYX-LOCK-005", "need \"" $2 "\" is not disk-bytes or docker-memory-bytes"); next }
    if ($3 !~ /^[0-9]+$/ || length($3) > 18 || $3 + 0 == 0) bad("ONYX-LOCK-004", $2 " \"" $3 "\" is not a positive integer of at most 18 digits")
    if ($2 in needs) bad("ONYX-LOCK-004", $2 " is listed twice")
    needs[$2] = 1
    next
  }
  { bad("ONYX-LOCK-005", "unknown row kind \"" $1 "\"") }
  END {
    if (versions == 0) { print "ONYX-LOCK-001 0: no version row"; failed = 1 }
    if (versions > 1) { print "ONYX-LOCK-001 0: " versions " version rows, want 1"; failed = 1 }
    for (r in repos) {
      if (!((r SUBSEP "linux/arm64") in seen)) { printf "ONYX-LOCK-003 %s: no linux/arm64 row\n", r; failed = 1 }
      if (!((r SUBSEP "linux/amd64") in seen)) { printf "ONYX-LOCK-003 %s: no linux/amd64 row\n", r; failed = 1 }
    }
    if (!("disk-bytes" in needs)) { print "ONYX-LOCK-004 0: no need disk-bytes row"; failed = 1 }
    if (!("docker-memory-bytes" in needs)) { print "ONYX-LOCK-004 0: no need docker-memory-bytes row"; failed = 1 }
    exit failed
  }
' "$file"
