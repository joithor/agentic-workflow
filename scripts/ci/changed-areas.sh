#!/usr/bin/env bash
# scripts/ci/changed-areas.sh — the CI path filter.
#
# Usage: changed-areas.sh <event-name> <base-sha>
#
# Prints GitHub step outputs (key=value lines) naming which areas a change
# touches, so each CI job can decide between real work and a no-op success:
#   ts=true|false        any TS package changed
#   bash=true|false      the bash tests need to run
#   dotnet=true|false    the .NET solution needs to build
#   packages=[...]       JSON array of the TS packages that changed
#
# Anything other than a pull_request with a known base runs everything: a
# filter that can't tell must not skip a check.

TS_PACKAGES=(mcp-bridge scorer judge sindri skills/ui-evidence skills/bugFixOrchestrator)

# Packages whose tests import another package's source: a change to the key also
# runs the listed dependents (scorer's tests import judge/src/db.ts).
dependents_of() {
  case "$1" in
    judge) echo scorer ;;
  esac
}

json_array() {
  local out="" item
  for item in "$@"; do out+="${out:+,}\"$item\""; done
  printf '[%s]' "$out"
}

print_all() {
  printf 'ts=true\nbash=true\ndotnet=true\npackages=%s' "$(json_array "${TS_PACKAGES[@]}")"
}

# Reads changed paths (one per line) on stdin and prints the outputs.
classify_changes() {
  local ts=false bash=false dotnet=false file pkg
  local -a changed_packages=()
  local files
  files="$(cat)"
  while IFS= read -r file; do
    [ -n "$file" ] || continue
    case "$file" in
      .github/*|scripts/ci/*) print_all; return 0 ;;
    esac
    case "$file" in
      config/*|scripts/*|providers/*|setup.sh|mods/*|skills/*|.agents/*|AGENTS.md|CLAUDE.md|.claude/*|.cursor/*)
        bash=true ;;
      src/*|tests/*|*.cs|*.csproj|*.props|*.targets|*.slnx|global.json|nuget.config|.editorconfig|BannedSymbols.txt|packages.lock.json)
        dotnet=true ;;
    esac
    for pkg in "${TS_PACKAGES[@]}"; do
      case "$file" in
        "$pkg"/*)
          ts=true
          bash=true
          local p
          for p in "$pkg" $(dependents_of "$pkg"); do
            case " ${changed_packages[*]-} " in
              *" $p "*) ;;
              *) changed_packages+=("$p") ;;
            esac
          done
          ;;
      esac
    done
  done <<< "$files"
  printf 'ts=%s\nbash=%s\ndotnet=%s\npackages=%s' "$ts" "$bash" "$dotnet" \
    "$(json_array ${changed_packages[@]+"${changed_packages[@]}"})"
}

main() {
  local event="$1" base="$2"
  if [ "$event" != "pull_request" ] || [ -z "$base" ]; then
    print_all
    return 0
  fi
  git diff --name-only "$base" HEAD | classify_changes
}

if [ "${BASH_SOURCE[0]}" = "$0" ]; then
  set -euo pipefail
  main "${1:-}" "${2:-}"
  echo
fi
