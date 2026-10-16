#!/usr/bin/env bash
# scripts/onyx-lock.sh — check the shape of context/onyx.lock, the pins Sindri-managed Onyx is built from
# (spec docs/superpowers/specs/2026-10-16-sindri-onyx-managed-design.md §6).
#
# Usage: scripts/onyx-lock.sh check [--file F]
#        scripts/onyx-lock.sh verify [--file F]
#   check  every row is well formed, there is exactly one version row, every image has both platforms once, and the two
#          need rows are present once. Prints one line per problem: 'ONYX-LOCK-00n <line>: <what>'.
#   verify maintainer-run, needs the network and docker buildx, never in CI: runs check, then downloads every `file` row from
#          raw.githubusercontent.com/onyx-dot-app/onyx/<commit>/deployment/<path> and compares its sha256, compares every
#          `image` row with `docker buildx imagetools inspect <repository>:<tag> --raw` for its platform, and confirms the
#          compose file at the commit names only pinned images (each `image:` line, with ${VAR:-default} resolved and
#          ${IMAGE_TAG...} taken as the version, is a repository and tag of an image row). Prints one line per mismatch
#          'ONYX-LOCK-01n <what>: expected X, got Y'. Needs curl, jq, docker and a sha256 tool. Exit: 0 all match, 1 a
#          mismatch (or check fails), 2 usage, I/O or network error.
# Rows (whitespace-separated; '#' comments and blank lines are ignored):
#   version <tag> <commit-sha-40>                    exactly one; tag is vX.Y.Z
#   file <path under deployment/> <sha256-64>        a deployment file Sindri fetches at <commit> and checks
#   image <repository> <tag> <platform> <sha256:digest>   every image, for linux/arm64 and linux/amd64; one tag per repository
#   need disk-bytes <n>                              exactly one
#   need docker-memory-bytes <n>                     exactly one
#   A repository matches ^[a-z0-9][a-z0-9._/-]*$ and a tag ^[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}$; a file path is relative, with no backslash, empty or .. segment and no NUL;
#   no file path is listed twice; a need is 1 to 18 digits and positive (it fits a long).
# Parity: OnyxPins.Parse (src/Sindri/Aw.Sindri.Core/Onyx/OnyxPins.cs) refuses exactly the files this check refuses, as far as the
#   fixtures of scripts/tests/onyx-lock.test.sh and tests/Aw.Sindri.Tests/OnyxPinsTests/OnyxPinsParseTests.cs go: both suites
#   run the same malformed cases (need of 19 digits, duplicate file or image row, bad repository name, bad file paths, NUL).
#   The C# parser reports every refusal as SND-ONYX-004 where this script uses the codes below.
# Codes: 001 version row (missing, repeated, bad tag), 002 a malformed value, 003 image platforms (missing or repeated),
#        004 need rows (missing, repeated, not a number), 005 unknown row kind or need name.
# Exit: 0 ok, 1 a check failed, 2 usage or I/O error.
# Codes of verify: 010 a file's sha256 differs, 011 an image digest differs or is absent, 012 the compose names an unpinned image.
# Bash 3.2 compatible (macOS /bin/bash).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

usage() { sed -n '5,6p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//' >&2; exit 2; }

cmd="${1:-}"; { [ "$cmd" = "check" ] || [ "$cmd" = "verify" ]; } || usage; shift
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

do_check() {
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
    if (NF != 5) { bad("ONYX-LOCK-002", "image wants 4 fields (repository tag platform digest), got " (NF - 1)); next }
    if ($2 !~ /^[a-z0-9][a-z0-9._\/-]*$/) bad("ONYX-LOCK-002", "repository \"" $2 "\" does not match ^[a-z0-9][a-z0-9._/-]*$")
    if ($3 !~ /^[A-Za-z0-9_][A-Za-z0-9_.-]*$/ || length($3) > 128) bad("ONYX-LOCK-002", "tag \"" $3 "\" does not match ^[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}$")
    if ($4 != "linux/arm64" && $4 != "linux/amd64") { bad("ONYX-LOCK-002", "platform \"" $4 "\" is not linux/arm64 or linux/amd64"); next }
    if ($5 !~ /^sha256:[0-9a-f]+$/ || length($5) != 71) bad("ONYX-LOCK-002", "digest is not sha256: and 64 lowercase hex digits")
    if (($2 SUBSEP $4) in seen) bad("ONYX-LOCK-003", $2 " " $4 " is listed twice")
    if (($2 in tagof) && tagof[$2] != $3) bad("ONYX-LOCK-002", $2 " is listed with tags " tagof[$2] " and " $3)
    tagof[$2] = $3
    seen[$2, $4] = 1; repos[$2] = 1
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
}

# resolve <image: value>: ${IMAGE_TAG:-...} is the pinned version, every other ${VAR:-default} its default, innermost first.
resolve() {
  local value="$1" next
  value="$(printf '%s' "$value" | sed -E 's/\$\{IMAGE_TAG:-[^${}]*\}/'"$version"'/g')"
  while [[ "$value" == *'${'* ]]; do
    next="$(printf '%s' "$value" | sed -E 's/\$\{[A-Za-z_][A-Za-z0-9_]*:-([^${}]*)\}/\1/')"
    [ "$next" != "$value" ] || break
    value="$next"
  done
  printf '%s' "$value"
}

# sha256 of a file, with whichever tool this box has
sha256_of() { if command -v sha256sum >/dev/null 2>&1; then sha256sum "$1" | cut -d' ' -f1; else shasum -a 256 "$1" | cut -d' ' -f1; fi; }

do_verify() {
  local out rc=0 tool
  # tmp stays global: the EXIT trap runs after this function returns
  set +e; out="$(do_check)"; rc=$?; set -e
  if [ "$rc" -ne 0 ]; then printf '%s\n' "$out"; exit 1; fi
  for tool in curl jq docker; do
    command -v "$tool" >/dev/null 2>&1 || { echo "onyx-lock verify: $tool not found" >&2; exit 2; }
  done
  command -v sha256sum >/dev/null 2>&1 || command -v shasum >/dev/null 2>&1 || { echo "onyx-lock verify: no sha256sum or shasum" >&2; exit 2; }

  local commit version base mismatches=0 files=0 images=0 refs=0
  tmp="$(mktemp -d "${TMPDIR:-/tmp}/onyx-lock-verify.XXXXXX")"
  trap 'find "$tmp" -delete' EXIT
  commit="$(awk '$1 == "version" { print $3 }' "$file")"
  version="$(awk '$1 == "version" { print $2 }' "$file")"
  base="https://raw.githubusercontent.com/onyx-dot-app/onyx/$commit/deployment"

  local _kind path want got
  while read -r _kind path want; do
    curl -fsSL --retry 2 --max-time 60 -o "$tmp/download" "$base/$path" \
      || { echo "onyx-lock verify: cannot download $base/$path" >&2; exit 2; }
    got="$(sha256_of "$tmp/download")"
    files=$((files + 1))
    if [ "$got" != "$want" ]; then
      echo "ONYX-LOCK-010 $path: expected $want, got $got"; mismatches=$((mismatches + 1))
    fi
    if [ "$path" = "docker_compose/docker-compose.yml" ]; then cp "$tmp/download" "$tmp/compose"; fi
  done < <(awk '$1 == "file"' "$file")

  local repo tag platform last="" raw os arch
  while read -r _kind repo tag platform want; do
    if [ "$repo:$tag" != "$last" ]; then
      raw="$(docker buildx imagetools inspect "$repo:$tag" --raw 2>"$tmp/error")" \
        || { echo "onyx-lock verify: cannot inspect $repo:$tag: $(head -c 300 "$tmp/error")" >&2; exit 2; }
      last="$repo:$tag"
    fi
    os="${platform%/*}"; arch="${platform#*/}"
    got="$(printf '%s' "$raw" | jq -r --arg os "$os" --arg arch "$arch" '[.manifests[]? | select(.platform.os == $os and .platform.architecture == $arch) | .digest] | (first // "absent")')" \
      || { echo "onyx-lock verify: $repo:$tag did not answer with an image index" >&2; exit 2; }
    images=$((images + 1))
    if [ "$got" != "$want" ]; then
      echo "ONYX-LOCK-011 $repo:$tag $platform: expected $want, got $got"; mismatches=$((mismatches + 1))
    fi
  done < <(awk '$1 == "image"' "$file")

  if [ -r "$tmp/compose" ]; then
    local value ref first
    while IFS= read -r value; do
      ref="$(resolve "$value")"
      refs=$((refs + 1))
      if [[ "$ref" == *'${'* ]]; then
        echo "ONYX-LOCK-012 docker-compose.yml: $value has a variable this check can't resolve"; mismatches=$((mismatches + 1)); continue
      fi
      ref="${ref%%@*}"
      first="${ref%%/*}"
      case "$first" in *.*|*:*|localhost) ;; *) ref="docker.io/$ref" ;; esac
      case "${ref##*/}" in *:*) ;; *) ref="$ref:latest" ;; esac
      if ! awk -v r="${ref%:*}" -v t="${ref##*:}" '$1 == "image" && $2 == r && $3 == t { found = 1 } END { exit !found }' "$file"; then
        echo "ONYX-LOCK-012 docker-compose.yml: names $ref, expected an image row for it, got none"; mismatches=$((mismatches + 1))
      fi
    done < <(sed -nE 's/^[[:space:]]*image:[[:space:]]*([^[:space:]#]+).*/\1/p' "$tmp/compose")
  else
    echo "ONYX-LOCK-012 docker-compose.yml: the lock has no file row for docker_compose/docker-compose.yml, so its images can't be checked"; mismatches=$((mismatches + 1))
  fi

  if [ "$mismatches" -gt 0 ]; then echo "onyx-lock verify: $mismatches mismatch(es)"; exit 1; fi
  echo "onyx-lock verify: ok ($files files, $images image rows, $refs compose image lines)"
}

if [ "$cmd" = "verify" ]; then do_verify; else do_check; fi
