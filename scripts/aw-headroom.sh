#!/usr/bin/env bash
# aw-headroom — run Claude Code or Codex through the headroom proxy, chained in front of whatever the tool
# already talks to (an API gateway such as EasyCLIProxyAPI, or the official API):
#
#   client -> headroom proxy (loopback) -> the upstream the client would have used -> the provider
#
# Usage: aw-headroom claude|codex [tool args...]   start (or reuse) the proxy, run the tool through it
#        aw-headroom upstream claude|codex          print the upstream that would be chained (no side effects)
#        aw-headroom status                         the proxies this wrapper started
#        aw-headroom stop [claude|codex]            stop them
#
# Nothing on disk is edited: the tool gets its base URL for this one run, as a --settings overlay (Claude
# Code) or a -c override (Codex). The auth token is never read, passed or printed here; it flows from the
# tool's own configuration to the upstream.
#
# Fail-open: when headroom is missing, the port is taken, or the proxy does not come up, the tool runs
# unwrapped with a one-line warning.
#
# Upstream order (first hit wins; headroom's own URL never counts, so a nested run cannot loop):
#   claude: $ANTHROPIC_BASE_URL, settings.json env.ANTHROPIC_BASE_URL, https://api.anthropic.com
#   codex:  $OPENAI_BASE_URL, the active model provider's base_url in config.toml (or top-level
#           openai_base_url when no provider is set), https://api.openai.com/v1
#
# Environment (all optional):
#   AW_HEADROOM_PORT_CLAUDE / AW_HEADROOM_PORT_CODEX  loopback ports (defaults 8791 / 8792)
#   AW_HEADROOM_BIN        headroom executable (default: headroom on PATH, then ~/.local/bin/headroom)
#   AW_HEADROOM_KEEP=1     leave the proxy running after the last wrapped session exits
#   AW_HEADROOM_START_WAIT seconds to wait for the proxy's /health (default 120)
#   AW_HEADROOM_ARGS       extra arguments for `headroom proxy`, word-split
#   CLAUDE_SETTINGS_FILE   Claude settings (default ~/.claude/settings.json)
#   CODEX_HOME             Codex home (default ~/.codex); its config.toml is read
#   AW_STATE_DIR           state root (default ~/.agentic-workflow); proxies live under headroom/<provider>/
set -uo pipefail

STATE_ROOT="${AW_STATE_DIR:-$HOME/.agentic-workflow}/headroom"
PORT_CLAUDE="${AW_HEADROOM_PORT_CLAUDE:-8791}"
PORT_CODEX="${AW_HEADROOM_PORT_CODEX:-8792}"
DEFAULT_ANTHROPIC="https://api.anthropic.com"
DEFAULT_OPENAI="https://api.openai.com/v1"

warn() { echo "aw-headroom: $*" >&2; }

headroom_bin() {
  if [ -n "${AW_HEADROOM_BIN:-}" ]; then printf '%s\n' "$AW_HEADROOM_BIN"; return; fi
  if command -v headroom >/dev/null 2>&1; then command -v headroom; return; fi
  [ -x "$HOME/.local/bin/headroom" ] && printf '%s\n' "$HOME/.local/bin/headroom"
}

port_of() { case "$1" in claude) echo "$PORT_CLAUDE" ;; codex) echo "$PORT_CODEX" ;; esac; }

norm_url() { local u="${1%/}"; printf '%s\n' "$u"; }

# Is the URL one of headroom's own (either wrapper port, any loopback spelling)?
is_headroom_url() {
  local u rest
  u="$(norm_url "$1")"
  rest="${u#*://}"; rest="${rest%%/*}"
  case "$rest" in
    "127.0.0.1:$PORT_CLAUDE"|"localhost:$PORT_CLAUDE"|"[::1]:$PORT_CLAUDE"|"0.0.0.0:$PORT_CLAUDE") return 0 ;;
    "127.0.0.1:$PORT_CODEX"|"localhost:$PORT_CODEX"|"[::1]:$PORT_CODEX"|"0.0.0.0:$PORT_CODEX") return 0 ;;
  esac
  return 1
}

claude_upstream() {
  local u settings="${CLAUDE_SETTINGS_FILE:-$HOME/.claude/settings.json}"
  u="${ANTHROPIC_BASE_URL:-}"
  if [ -n "$u" ] && ! is_headroom_url "$u"; then norm_url "$u"; return; fi
  if [ -f "$settings" ] && command -v jq >/dev/null 2>&1; then
    u="$(jq -r '.env.ANTHROPIC_BASE_URL // empty' "$settings" 2>/dev/null || true)"
    if [ -n "$u" ] && ! is_headroom_url "$u"; then norm_url "$u"; return; fi
  fi
  printf '%s\n' "$DEFAULT_ANTHROPIC"
}

# Prints "<provider-name-or-empty><TAB><base_url-or-empty>" for the active Codex provider block. Only
# a base_url is read: never a token key.
codex_config_url() {
  local cfg="${CODEX_HOME:-$HOME/.codex}/config.toml"
  [ -f "$cfg" ] || return 0
  awk '
    function val(s,   v) { v = s; sub(/^[^=]*=[ \t]*/, "", v); sub(/[ \t]*(#.*)?$/, "", v); gsub(/^["\047]|["\047]$/, "", v); return v }
    /^[ \t]*\[/ { top = 0; sec = $0; gsub(/^[ \t]*\[|\][ \t]*(#.*)?$/, "", sec); gsub(/"/, "", sec); next }
    BEGIN { top = 1 }
    top && /^[ \t]*model_provider[ \t]*=/ { prov = val($0) }
    top && /^[ \t]*openai_base_url[ \t]*=/ { top_url = val($0) }
    !top && /^[ \t]*base_url[ \t]*=/ { urls[sec] = val($0) }
    END {
      if (prov != "") print prov "\t" urls["model_providers." prov]
      else print "\t" top_url
    }' "$cfg"
}

codex_upstream() {
  local u line
  u="${OPENAI_BASE_URL:-}"
  if [ -n "$u" ] && ! is_headroom_url "$u"; then norm_url "$u"; return; fi
  line="$(codex_config_url)"
  u="${line#*$'\t'}"
  if [ -n "$u" ] && ! is_headroom_url "$u"; then norm_url "$u"; return; fi
  printf '%s\n' "$DEFAULT_OPENAI"
}

upstream_of() { case "$1" in claude) claude_upstream ;; codex) codex_upstream ;; esac; }

# headroom appends the request path (/v1/messages, /v1/responses) to its target, so the target is the origin
# plus any prefix before a trailing /v1.
proxy_target() { local u; u="$(norm_url "$1")"; printf '%s\n' "${u%/v1}"; }

alive() { [ -n "${1:-}" ] && kill -0 "$1" 2>/dev/null; }
healthy() { curl -fsS -m 2 "http://127.0.0.1:$1/health" >/dev/null 2>&1; }
port_busy() { (exec 3<>"/dev/tcp/127.0.0.1/$1") 2>/dev/null; }

pdir() { printf '%s/%s\n' "$STATE_ROOT" "$1"; }

stop_proxy() {
  local d pid; d="$(pdir "$1")"
  pid="$(cat "$d/proxy.pid" 2>/dev/null || true)"
  if alive "$pid"; then kill "$pid" 2>/dev/null || true; fi
  rm -f "$d/proxy.pid" "$d/upstream"
  rm -rf "$d/users"
}

live_users() {
  local d="$1/users" f n=0
  [ -d "$d" ] || { echo 0; return; }
  for f in "$d"/*; do
    [ -e "$f" ] || continue
    if alive "$(basename "$f")"; then n=$((n + 1)); else rm -f "$f"; fi
  done
  echo "$n"
}

# Start or reuse the proxy for $1 with upstream $2. Returns 0 when it is healthy on its port.
ensure_proxy() {
  local provider="$1" upstream="$2" port bin d pid target wait i
  port="$(port_of "$provider")"; d="$(pdir "$provider")"
  target="$(proxy_target "$upstream")"
  mkdir -p "$d/users" || return 1
  pid="$(cat "$d/proxy.pid" 2>/dev/null || true)"
  if alive "$pid" && healthy "$port"; then
    if [ "$(cat "$d/upstream" 2>/dev/null)" = "$target" ]; then return 0; fi
    if [ "$(live_users "$d")" -gt 0 ]; then
      warn "the proxy on :$port chains to a different upstream and another session uses it; running unwrapped"
      return 1
    fi
    stop_proxy "$provider"; mkdir -p "$d/users"
  fi
  if port_busy "$port"; then warn "port $port is taken by something that is not this wrapper's proxy; running unwrapped"; return 1; fi
  bin="$(headroom_bin)"
  if [ -z "$bin" ]; then warn "headroom is not installed (run scripts/install-headroom.sh); running unwrapped"; return 1; fi
  local -a args=(proxy --host 127.0.0.1 --port "$port" --no-telemetry)
  case "$provider" in
    claude) args+=(--anthropic-api-url "$target") ;;
    codex) args+=(--openai-api-url "$target") ;;
  esac
  # shellcheck disable=SC2206
  [ -n "${AW_HEADROOM_ARGS:-}" ] && args+=(${AW_HEADROOM_ARGS})
  ( umask 077; setsid nohup "$bin" "${args[@]}" >"$d/proxy.log" 2>&1 < /dev/null & echo $! >"$d/proxy.pid" )
  printf '%s\n' "$target" >"$d/upstream"
  pid="$(cat "$d/proxy.pid")"
  wait="${AW_HEADROOM_START_WAIT:-120}"
  for ((i = 0; i < wait * 2; i++)); do
    healthy "$port" && return 0
    alive "$pid" || break
    sleep 0.5
  done
  warn "the proxy did not come up (see $d/proxy.log); running unwrapped"
  stop_proxy "$provider"
  return 1
}

# EXIT trap of a wrapped run: leave the session list and stop the proxy when it was the last one.
cleanup() {
  rm -f "$RUN_DIR/users/$$"
  if [ "${AW_HEADROOM_KEEP:-0}" != "1" ] && [ "$(live_users "$RUN_DIR")" -eq 0 ]; then stop_proxy "$RUN_PROVIDER"; fi
}

run_wrapped() {
  local provider="$1"; shift
  local tool="$provider" upstream port url rc d
  if ! command -v "$tool" >/dev/null 2>&1; then warn "$tool is not on PATH"; return 127; fi
  upstream="$(upstream_of "$provider")"
  port="$(port_of "$provider")"; d="$(pdir "$provider")"
  if ! ensure_proxy "$provider" "$upstream"; then exec "$tool" "$@"; fi
  url="http://127.0.0.1:$port"
  : >"$d/users/$$"
  RUN_PROVIDER="$provider"; RUN_DIR="$d"
  trap cleanup EXIT
  trap 'exit 130' INT
  trap 'exit 143' TERM
  warn "$provider -> headroom :$port -> $upstream"
  case "$provider" in
    claude)
      # --settings is a higher-precedence layer than the user's settings.json env, so the overlay wins
      # without touching that file; the token in settings.json still applies.
      ANTHROPIC_BASE_URL="$url" "$tool" --settings "$(jq -nc --arg u "$url" '{env: {ANTHROPIC_BASE_URL: $u}}')" "$@"
      rc=$? ;;
    codex)
      local line name
      line="$(codex_config_url)"; name="${line%%$'\t'*}"
      if [ -n "$name" ]; then
        OPENAI_BASE_URL="$url/v1" "$tool" -c "model_providers.$name.base_url=\"$url/v1\"" "$@"
      else
        OPENAI_BASE_URL="$url/v1" "$tool" -c "openai_base_url=\"$url/v1\"" "$@"
      fi
      rc=$? ;;
  esac
  return "$rc"
}

cmd_status() {
  local p d pid
  for p in claude codex; do
    d="$(pdir "$p")"; pid="$(cat "$d/proxy.pid" 2>/dev/null || true)"
    if alive "$pid"; then
      echo "$p: running pid $pid port $(port_of "$p") upstream $(cat "$d/upstream" 2>/dev/null) sessions $(live_users "$d")"
    else
      echo "$p: not running"
    fi
  done
}

case "${1:-}" in
  claude|codex)
    p="$1"; shift
    if ! command -v jq >/dev/null 2>&1 && [ "$p" = claude ]; then warn "jq is required"; exec claude "$@"; fi
    run_wrapped "$p" "$@"; exit $? ;;
  upstream)
    case "${2:-}" in claude|codex) upstream_of "$2" ;; *) echo "usage: aw-headroom upstream claude|codex" >&2; exit 2 ;; esac ;;
  status) cmd_status ;;
  stop)
    if [ -n "${2:-}" ]; then stop_proxy "$2"; else stop_proxy claude; stop_proxy codex; fi ;;
  *) echo "usage: aw-headroom claude|codex [args...] | upstream claude|codex | status | stop [claude|codex]" >&2; exit 2 ;;
esac
