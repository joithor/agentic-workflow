#!/usr/bin/env bash
# scripts/sindri-app.sh — the C# Sindri app in one command, `sindri-app`: publish it for this platform when the build is
# stale, then run it. Bare `sindri-app` is bare `sindri`: it starts the control plane (serve and its pinned Temporal dev
# server) or finds the running one, and opens the desktop app. Any other arguments go to the C# `sindri` unchanged
# (`sindri-app status`, `sindri-app stop`, `sindri-app item add …`).
#
#   sindri-app [--rebuild] [<sindri arguments>...]
#   --rebuild  publish again although the build matches the source
#
# A side name: the TypeScript `sindri` (~/.local/bin/sindri, its launchd jobs and the nudge hook) stays as it is until the
# switch (AGENTS.md "C# build"), and nothing in setup.sh, the providers or launchd runs this.
#
# The build is one directory, $AW_STATE_DIR/bin/sindri-app/<stamp>/: `sindri` framework-dependent (the Temporal .NET SDK
# can't be trimmed; .agents/rules/csharp.md "AOT scope") and `sindri-ui` NativeAOT for this RID with its native
# libraries (src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh <dir>), side by side, which is where bare `sindri` looks for
# `sindri-ui` (docs/csharp/sindri-ui.md "Launching"). <stamp> hashes the content of every tracked and untracked file
# under the paths the host build reads, the pinned SDK and the platform, so a source change, committed or not, rebuilds
# and nothing else does. The newest two builds are kept: a control plane already running may be using the older one.
# The host publish runs on a copy of those files (as the smoke scripts do, so the checkout's lock files never change),
# under the box-wide heavy-job slot when it exists ($AW_STATE_DIR/bin-r1b/heavy), one publish at a time per state dir.
# Progress goes to stderr, so stdout is only sindri's (`sindri-app status --json` stays parseable).
#
# Every run also: installs the .NET SDK global.json pins into $DOTNET_ROOT (default ~/.dotnet) when missing, with
# Microsoft's dotnet-install.sh; fetches the pinned temporal CLI when missing (scripts/binaries-lock.sh fetch temporal);
# and links $CLAUDE_LOCAL_BIN/sindri-app (default ~/.local/bin) to this script, unless AW_SINDRI_APP_NO_LINK=1, this tree
# is a linked git worktree, or something else already has that name (a dangling link is replaced).
#
# Exit: sindri's exit code; 1 when setup or a publish failed (the publish log's tail on stderr).
# Bash 3.2 compatible (macOS /bin/bash).
set -euo pipefail

src="${BASH_SOURCE[0]}"
while [ -L "$src" ]; do
  t="$(readlink "$src")"
  case "$t" in /*) src="$t" ;; *) src="$(dirname "$src")/$t" ;; esac
done
here="$(cd "$(dirname "$src")" && pwd -P)"
root="$(cd "$here/.." && pwd -P)"
self="$here/$(basename "$src")"

say() { echo "sindri-app: $*" >&2; }
die() { say "$*"; exit 1; }

rebuild=0
if [ "${1:-}" = --rebuild ]; then rebuild=1; shift; fi

state="${AW_STATE_DIR:-$HOME/.agentic-workflow}"
base="$state/bin/sindri-app"
cache="$state/cache/sindri-app-build"
heavy="$state/bin-r1b/heavy"

# 1. The PATH link (default on). Only from the canonical checkout: a worktree's link would dangle once it is removed.
link_dir="${CLAUDE_LOCAL_BIN:-$HOME/.local/bin}"
link="$link_dir/sindri-app"
if [ "${AW_SINDRI_APP_NO_LINK:-0}" != 1 ]; then
  gd="$(git -C "$root" rev-parse --absolute-git-dir 2>/dev/null || true)"
  gc="$(git -C "$root" rev-parse --path-format=absolute --git-common-dir 2>/dev/null || true)"
  if [ -n "$gd" ] && [ "$gd" != "$gc" ]; then
    :
  elif [ -L "$link" ] && [ "$(readlink "$link")" = "$self" ]; then
    :
  elif [ -e "$link" ]; then
    say "$link is already something else; left as it is (run $self directly, or remove it to link this one)"
  else
    mkdir -p "$link_dir"
    ln -sfn "$self" "$link"
    say "linked $link -> $self"
    case ":$PATH:" in *":$link_dir:"*) ;; *) say "$link_dir is not on your PATH; add it to run sindri-app by name" ;; esac
  fi
fi

# 2. The pinned SDK, on PATH for this run and the apphost (which finds the runtime through DOTNET_ROOT).
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
sdk="$(sed -n 's/.*"version": *"\([^"]*\)".*/\1/p' "$root/global.json" | head -1)"
[ -n "$sdk" ] || die "no sdk version in $root/global.json"
if ! dotnet --list-sdks 2>/dev/null | grep -q "^$sdk "; then
  say "installing the .NET SDK $sdk into $DOTNET_ROOT (global.json)"
  curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --version "$sdk" --install-dir "$DOTNET_ROOT" --no-path >&2 \
    || die "installing the .NET SDK $sdk failed"
fi

# 3. The pinned temporal CLI that serve starts (kept when already installed).
"$root/scripts/binaries-lock.sh" fetch temporal >/dev/null || die "fetching the pinned temporal CLI failed (scripts/binaries-lock.sh fetch temporal)"

# 4. The stamp: what the build is made of.
case "$(uname -s)/$(uname -m)" in
  Darwin/arm64) platform=osx-arm64 ;; Darwin/x86_64) platform=osx-x64 ;;
  Linux/aarch64|Linux/arm64) platform=linux-arm64 ;; Linux/x86_64) platform=linux-x64 ;;
  *) die "unsupported platform $(uname -s)/$(uname -m)" ;;
esac
sha256() { shasum -a 256 2>/dev/null || sha256sum; }
mkdir -p "$base" "$cache"
inputs="$cache/inputs.$$"
trap 'rm -f "$inputs"' EXIT
# A tracked file deleted in the working tree is left out: it isn't part of the build, and tar would fail on it.
git -C "$root" ls-files -z --cached --others --exclude-standard -- global.json nuget.config .editorconfig 'Directory.*' \
  BannedSymbols.txt CodeMetricsConfig.txt src/Aw.Kernel src/Aw.Kernel.Data src/Aw.Kernel.Telemetry src/Redaction \
  src/Aw.Analyzers src/Context src/Judge src/Sindri context \
  | while IFS= read -r -d '' f; do if [ -f "$root/$f" ]; then printf '%s\n' "$f"; fi; done | LC_ALL=C sort -u > "$inputs"
[ -s "$inputs" ] || die "no build inputs found under $root"
stamp="$( { printf '%s\n' "$sdk" "$platform"; cat "$inputs"; (cd "$root" && git hash-object --stdin-paths < "$inputs"); } | sha256 | cut -c1-16)"
current="$(cat "$base/current" 2>/dev/null || true)"
built() { [ -x "$base/$1/sindri" ] && [ -x "$base/$1/sindri-ui" ]; }

# 5. Publish when stale, one publish at a time (a lock dir holding the owner's pid; a dead owner's lock is taken over).
if [ "$rebuild" = 1 ] || [ "$current" != "$stamp" ] || ! built "$stamp"; then
  lock="$base/.lock"
  until mkdir "$lock" 2>/dev/null; do
    owner="$(cat "$lock/pid" 2>/dev/null || true)"
    if [ -n "$owner" ] && ! kill -0 "$owner" 2>/dev/null; then rm -rf "$lock"; continue; fi
    say "waiting for another sindri-app publish (pid ${owner:-?})"
    sleep 2
  done
  echo $$ > "$lock/pid"
  tree=""
  stage="$base/.stage.$$"
  trap 'rm -f "$inputs"; rm -rf "$lock" "$stage"; [ -z "$tree" ] || rm -rf "$tree"' EXIT
  current="$(cat "$base/current" 2>/dev/null || true)"
  # Another run may have published this stamp while this one waited.
  if [ "$rebuild" = 1 ] || [ "$current" != "$stamp" ] || ! built "$stamp"; then
    log="$cache/publish.log"
    : > "$log"
    pre=()
    if [ -x "$heavy" ]; then pre=("$heavy"); fi
    tree="$(mktemp -d "$cache/tree.XXXXXX")"
    (cd "$root" && tar -cf - -T "$inputs") | tar -xf - -C "$tree"
    rm -rf "$stage"
    say "publishing sindri (framework-dependent) and sindri-ui (NativeAOT, $platform), build $stamp; log: $log"
    ${pre[@]+"${pre[@]}"} dotnet publish "$tree/src/Sindri/Aw.Sindri.Host/Aw.Sindri.Host.csproj" -c Release --no-self-contained \
      -o "$stage" </dev/null >>"$log" 2>&1 || { tail -20 "$log" >&2; die "publishing sindri failed"; }
    ${pre[@]+"${pre[@]}"} bash "$root/src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh" "$stage" </dev/null >>"$log" 2>&1 \
      || { tail -20 "$log" >&2; die "publishing sindri-ui failed"; }
    [ -x "$stage/sindri" ] || die "the publish left no sindri in $stage"
    [ -x "$stage/sindri-ui" ] || die "the publish left no sindri-ui in $stage"
    rm -rf "$base/$stamp"
    mv "$stage" "$base/$stamp"
    # Keep this build and the one before it (a --rebuild of the current stamp keeps the earlier previous one).
    previous="$(cat "$base/previous" 2>/dev/null || true)"
    if [ -n "$current" ] && [ "$current" != "$stamp" ]; then previous="$current"; fi
    printf '%s\n' "$previous" > "$base/previous"
    printf '%s\n' "$stamp" > "$base/current.new"
    mv -f "$base/current.new" "$base/current"
    for d in "$base"/*; do
      n="$(basename "$d")"
      if [ -d "$d" ] && [ "$n" != "$stamp" ] && [ "$n" != "$previous" ]; then rm -rf "$d"; fi
    done
    say "built $base/$stamp"
    # A control plane already running keeps its build: bare sindri finds it rather than starting this one.
    control="${AW_SINDRI_HOME:-$state/sindri-app}/control.json"
    pid="$(jq -r '.pid // empty' "$control" 2>/dev/null || true)"
    if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
      say "a control plane from an earlier build is running (pid $pid); \`sindri-app stop\`, then \`sindri-app\`, runs this one"
    fi
  fi
  rm -rf "$lock"
fi

rm -f "$inputs"
exec "$base/$stamp/sindri" "$@"
