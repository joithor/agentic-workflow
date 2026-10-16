#!/usr/bin/env bash
# Install the /meta harness (opt-in: ./setup.sh --with-meta; owner decision 2026-10-14): the aw-meta NativeAOT CLI,
# its two Claude Code hooks, and the team launcher. aw-meta is a standalone pre-switch C# CLI like aw-readiness and
# aw-packets (docs/csharp/meta.md): its state lives under $AW_STATE_DIR/meta, and
# it appends to the team's running log; its gate and merge commands act on the team's checkout and PRs.
#
#   install-meta.sh [--provider claude|codex|cursor]   default claude
#   AW_DRY_RUN=1     print what would happen, write nothing
#   AW_SKIP_BUILD=1  don't publish; install the binary already in $AW_STATE_DIR/bin (tests)
#   CLAUDE_LOCAL_BIN where the aw-meta and aw-meta-launch links go (default ~/.local/bin)
#
# 1. Publish src/Meta/Aw.Meta.Host with NativeAOT for the host RID, on a copy of the files it needs (a RID publish
#    adds the host's ILCompiler packs to the lock files, so it never runs on the checkout), under the heavy-job lock
#    when it exists. The binary and its bundled e_sqlite3 land in $AW_STATE_DIR/bin.
# 2. Hooks (Claude Code only: cross-session messages exist only there): Stop → meta-stop-gate.sh, PostToolUse with
#    matcher SendMessage → meta-outbox.sh. Both pass straight through for every session but the registered meta one.
# 3. Links aw-meta and aw-meta-launch (scripts/meta-launch.sh) into CLAUDE_LOCAL_BIN.
# The /meta skill itself is linked by setup.sh with the other skills.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
# shellcheck source=../config/hooks/adapters/install-lib.sh
source "$SCRIPT_DIR/config/hooks/adapters/install-lib.sh"
aw_parse_provider_args "$@" || exit 1
set -- ${AW_ARGS[@]+"${AW_ARGS[@]}"}
[ $# -eq 0 ] || { echo "usage: install-meta.sh [--provider claude|codex|cursor]" >&2; exit 1; }

STATE="${AW_STATE_DIR:-$HOME/.agentic-workflow}"
BIN="$STATE/bin"
LOCAL_BIN="${CLAUDE_LOCAL_BIN:-$HOME/.local/bin}"
HEAVY="$STATE/bin-r1b/heavy"
dry() { [ "${AW_DRY_RUN:-0}" = 1 ]; }

if [ "$AW_PROVIDER" != claude ]; then
  echo "  meta: skipped for $AW_PROVIDER (cross-session messaging is Claude Code only)"
  exit 0
fi

# aw-meta reads Linux /proc (memory, uptime, the process table), so it does not run on macOS yet.
if [ "$(uname)" = Darwin ]; then
  echo "install-meta: aw-meta is Linux-only for now (it reads /proc); not installing on macOS" >&2
  exit 1
fi

# 1. Build.
if [ "${AW_SKIP_BUILD:-0}" = 1 ]; then
  [ -x "$BIN/aw-meta" ] || { echo "install-meta: AW_SKIP_BUILD=1 but $BIN/aw-meta is missing" >&2; exit 1; }
elif dry; then
  echo "  [dry-run] would publish src/Meta/Aw.Meta.Host (NativeAOT) to $BIN/aw-meta"
else
  export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
  # The SDK global.json pins (rollForward disable): installed into ~/.dotnet with Microsoft's dotnet-install.sh when missing
  sdk="$(jq -r '.sdk.version' "$SCRIPT_DIR/global.json")"
  if ! dotnet --list-sdks 2>/dev/null | grep -q "^$sdk "; then
    echo "  meta: installing the .NET SDK $sdk into ~/.dotnet (global.json)"
    curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --version "$sdk" --install-dir "$HOME/.dotnet" --no-path \
      || { echo "install-meta: installing the .NET SDK $sdk failed" >&2; exit 1; }
  fi
  work="$STATE/cache/meta-build"
  mkdir -p "$work"
  tree="$(mktemp -d "$work/tree.XXXXXX")"
  git -C "$SCRIPT_DIR" ls-files -z --cached --others --exclude-standard -- global.json nuget.config .editorconfig 'Directory.*' \
    BannedSymbols.txt CodeMetricsConfig.txt src/Aw.Kernel src/Aw.Kernel.Data src/Aw.Kernel.Telemetry src/Redaction src/Aw.Analyzers src/Meta \
    | (cd "$SCRIPT_DIR" && tar --null -T - -cf -) | tar -xf - -C "$tree"
  rid="$(dotnet --info | awk '/^ *RID:/ {print $2; exit}')"
  run=(dotnet publish "$tree/src/Meta/Aw.Meta.Host/Aw.Meta.Host.csproj" -c Release -r "$rid" -p:RestoreLockedMode=false -o "$tree/out")
  [ -x "$HEAVY" ] && run=("$HEAVY" "${run[@]}")
  echo "  meta: publishing aw-meta ($rid) — takes a box-wide build slot"
  "${run[@]}" < /dev/null > "$work/publish.log" 2>&1 || { tail -20 "$work/publish.log" >&2; exit 1; }
  mkdir -p "$BIN"
  for f in aw-meta libe_sqlite3.so libe_sqlite3.dylib; do
    [ -e "$tree/out/$f" ] || continue
    cp "$tree/out/$f" "$BIN/.$f.new" && mv "$BIN/.$f.new" "$BIN/$f"
  done
  find "$tree" -delete
  echo "  meta: installed $BIN/aw-meta"
fi

# 2. Hooks.
aw_hooks_init claude
if dry; then
  echo "  [dry-run] would install meta-stop-gate (Stop) and meta-outbox (PostToolUse SendMessage) in $AW_HOOKS_CONFIG"
else
  mkdir -p "$AW_HOOKS_INSTALL_DIR/lib"
  for h in meta-stop-gate.sh meta-outbox.sh; do
    cp "$SCRIPT_DIR/config/hooks/$h" "$AW_HOOKS_INSTALL_DIR/$h"
    chmod +x "$AW_HOOKS_INSTALL_DIR/$h"
  done
  cp "$SCRIPT_DIR/config/lib/failopen.sh" "$AW_HOOKS_INSTALL_DIR/lib/failopen.sh"
  stop=$(jq -nc --arg c "$AW_HOOKS_INSTALL_DIR/meta-stop-gate.sh # aw:meta-stop-gate" '{hooks:[{type:"command",command:$c,timeout:20}]}')
  out=$(jq -nc --arg c "$AW_HOOKS_INSTALL_DIR/meta-outbox.sh # aw:meta-outbox" '{matcher:"SendMessage",hooks:[{type:"command",command:$c,timeout:10}]}')
  merge_hook "$AW_HOOKS_CONFIG" Stop aw:meta-stop-gate "$stop"
  merge_hook "$AW_HOOKS_CONFIG" PostToolUse aw:meta-outbox "$out"
  echo "  meta: hooks installed in $AW_HOOKS_CONFIG"
fi

# 3. Links.
if dry; then
  echo "  [dry-run] would link aw-meta and aw-meta-launch into $LOCAL_BIN"
else
  mkdir -p "$LOCAL_BIN"
  ln -sfn "$BIN/aw-meta" "$LOCAL_BIN/aw-meta"
  ln -sfn "$SCRIPT_DIR/scripts/meta-launch.sh" "$LOCAL_BIN/aw-meta-launch"
  echo "  meta: aw-meta and aw-meta-launch in $LOCAL_BIN"
  [ -f "$STATE/meta/team.json" ] || echo "  meta: next, write a team config: aw-meta team-init --team <t> --repo <dir> --lanes a,b,c (example: skills/meta/examples/agentic-workflow.team.json)"
fi
