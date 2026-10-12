#!/usr/bin/env bash
# scripts/binaries-lock.sh — check binaries.lock's shape, or fetch one pinned binary for this platform
# (format: the header of binaries.lock; engine spec rev 8 §0 "Operations").
#
# Usage: scripts/binaries-lock.sh check [--file F]
#        scripts/binaries-lock.sh fetch <name> [--file F] [--dest DIR]
#   check  every row has six fields of the right shape, one version per name, every name has the four platforms
#          once each. Prints one line per problem: 'BIN-LOCK-00n <line>: <what>'.
#   fetch  downloads this platform's row of <name>, verifies its sha256 and installs the executable as
#          <DIR>/<name>-<version>/<basename of path> (DIR default ${AW_STATE_DIR:-~/.agentic-workflow}/cache/bin).
#          An installed binary is kept when present. Prints the installed path.
# Exit: 0 ok, 1 a check failed (or the sha256 didn't match), 2 usage or I/O error.
# Bash 3.2 compatible (macOS /bin/bash).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PLATFORMS="darwin-arm64 darwin-x64 linux-x64 linux-arm64"

usage() { sed -n '4,12p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//' >&2; exit 2; }

cmd="${1:-}"; [ -n "$cmd" ] || usage; shift
name=""
if [ "$cmd" = "fetch" ]; then name="${1:-}"; [ -n "$name" ] || usage; shift; fi
file="$ROOT/binaries.lock"
dest="${AW_STATE_DIR:-$HOME/.agentic-workflow}/cache/bin"
while [ $# -gt 0 ]; do
  case "$1" in
    --file) file="${2:?--file needs a path}"; shift 2 ;;
    --dest) dest="${2:?--dest needs a directory}"; shift 2 ;;
    *) usage ;;
  esac
done
[ -r "$file" ] || { echo "binaries-lock: cannot read $file" >&2; exit 2; }

check() {
  awk -v platforms="$PLATFORMS" '
    function bad(code, msg) { printf "%s %d: %s\n", code, NR, msg; failed = 1 }
    BEGIN { n = split(platforms, p, " "); for (i = 1; i <= n; i++) known[p[i]] = 1 }
    /^[[:space:]]*(#|$)/ { next }
    {
      rows++
      if (NF != 6) { bad("BIN-LOCK-001", "want 6 fields (name version platform sha256 url path), got " NF); next }
      name = $1; version = $2; platform = $3; sha = $4; url = $5; path = $6
      if (name !~ /^[a-z0-9][a-z0-9-]*$/) bad("BIN-LOCK-002", "name \"" name "\" is not [a-z0-9-]+")
      if (version !~ /^[0-9]+\.[0-9]+\.[0-9]+$/) bad("BIN-LOCK-002", "version \"" version "\" is not x.y.z")
      if (!(platform in known)) bad("BIN-LOCK-002", "platform \"" platform "\" is not one of " platforms)
      if (sha !~ /^[0-9a-f]+$/ || length(sha) != 64) bad("BIN-LOCK-002", "sha256 is not 64 lowercase hex digits")
      if (url !~ /^https:\/\/github\.com\/[^ ]+\.tar\.gz$/) bad("BIN-LOCK-002", "url is not an https://github.com/... .tar.gz")
      else if (index(url, version) == 0) bad("BIN-LOCK-002", "url does not carry version " version)
      if (path ~ /^\// || path ~ /(^|\/)\.\.(\/|$)/) bad("BIN-LOCK-002", "path \"" path "\" is not relative inside the archive")
      if ((name in ver) && ver[name] != version) bad("BIN-LOCK-003", name " has two versions: " ver[name] " and " version)
      ver[name] = version
      if ((name SUBSEP platform) in seen) bad("BIN-LOCK-004", name " " platform " is listed twice")
      seen[name, platform] = 1
    }
    END {
      for (nm in ver) for (i = 1; i <= n; i++) if (!((nm SUBSEP p[i]) in seen)) { printf "BIN-LOCK-004 %s: no %s row\n", nm, p[i]; failed = 1 }
      if (rows == 0) { print "BIN-LOCK-005 0: no rows"; failed = 1 }
      exit failed
    }
  ' "$file"
}

this_platform() {
  local os arch
  case "$(uname -s)" in Darwin) os=darwin ;; Linux) os=linux ;; *) echo "binaries-lock: unsupported OS $(uname -s)" >&2; exit 2 ;; esac
  case "$(uname -m)" in arm64|aarch64) arch=arm64 ;; x86_64|amd64) arch=x64 ;; *) echo "binaries-lock: unsupported arch $(uname -m)" >&2; exit 2 ;; esac
  echo "$os-$arch"
}

fetch() {
  check >/dev/null || { echo "binaries-lock: $file fails its check; run scripts/binaries-lock.sh check" >&2; exit 1; }
  local platform row version sha url path target got
  platform="$(this_platform)"
  row="$(awk -v n="$name" -v p="$platform" '!/^[[:space:]]*(#|$)/ && $1 == n && $3 == p { print; exit }' "$file")"
  [ -n "$row" ] || { echo "binaries-lock: no row for $name on $platform in $file" >&2; exit 2; }
  read -r _ version _ sha url path <<<"$row"
  target="$dest/$name-$version/$(basename "$path")"
  if [ -x "$target" ]; then echo "$target"; return 0; fi
  tmp="$(mktemp -d "${TMPDIR:-/tmp}/binaries-lock.XXXXXX")"   # global: the EXIT trap runs after fetch returns
  trap '[ -z "${tmp:-}" ] || find "$tmp" -delete' EXIT
  curl -fsSL --retry 2 -o "$tmp/archive.tar.gz" "$url" || { echo "binaries-lock: download failed: $url" >&2; exit 2; }
  got="$(shasum -a 256 "$tmp/archive.tar.gz" | awk '{print $1}')"
  if [ "$got" != "$sha" ]; then
    echo "binaries-lock: sha256 mismatch for $url: got $got, binaries.lock has $sha" >&2
    exit 1
  fi
  mkdir -p "$tmp/x"
  tar -xzf "$tmp/archive.tar.gz" -C "$tmp/x" "$path" || { echo "binaries-lock: $path not in $url" >&2; exit 2; }
  mkdir -p "$(dirname "$target")"
  install -m 0755 "$tmp/x/$path" "$target"
  echo "$target"
}

case "$cmd" in
  check) check ;;
  fetch) fetch ;;
  *) usage ;;
esac
