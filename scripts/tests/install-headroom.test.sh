#!/usr/bin/env bash
# Tests for scripts/aw-headroom.sh (the launcher) and scripts/install-headroom.sh (headroom as a proxy wrapper, never an MCP
# server). Run: bash scripts/tests/install-headroom.test.sh
# Everything runs under a temp HOME with a fake headroom (a tiny loopback HTTP server), fake claude and codex binaries on
# PATH and fake provider configs; no real settings file, proxy or network is touched.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
WRAP="$ROOT/scripts/aw-headroom.sh"
INSTALL="$ROOT/scripts/install-headroom.sh"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
contains() { case "$2" in *"$3"*) echo "ok - $1" ;; *) echo "not ok - $1"; echo "  missing: $3"; echo "  in:      $2"; fail=1 ;; esac; }
lacks() { case "$2" in *"$3"*) echo "not ok - $1"; echo "  unexpected: $3"; fail=1 ;; *) echo "ok - $1" ;; esac; }

WORK="$(mktemp -d)"
cleanup() { AW_STATE_DIR="$WORK/state" bash "$WRAP" stop >/dev/null 2>&1; rm -rf "$WORK"; }
trap cleanup EXIT
export HOME="$WORK/home"; mkdir -p "$HOME/.claude" "$HOME/.codex" "$WORK/bin"
export AW_STATE_DIR="$WORK/state"
export CLAUDE_SETTINGS_FILE="$HOME/.claude/settings.json"
export CODEX_HOME="$HOME/.codex"
export AW_HEADROOM_BIN_DIR="$WORK/localbin"
PC=$((20000 + $$ % 9000)); PX=$((PC + 1))
export AW_HEADROOM_PORT_CLAUDE="$PC" AW_HEADROOM_PORT_CODEX="$PX"
export AW_HEADROOM_START_WAIT=20
unset ANTHROPIC_BASE_URL OPENAI_BASE_URL
SECRET="FAKE-TOKEN-ZZ-0123456789-DO-NOT-PRINT"
CALLS="$WORK/calls.log"; : > "$CALLS"

# Fake headroom: --version; `proxy` logs its arguments and serves 200 on every GET on --port, loopback only.
cat > "$WORK/bin/headroom" <<'SH'
#!/usr/bin/env bash
if [ "${1:-}" = "--version" ]; then echo "headroom, version 0.0.0-fake"; exit 0; fi
if [ "${1:-}" = "mcp" ]; then echo "MCP $*" >> "$FAKE_CALLS"; exit 0; fi
echo "headroom $*" >> "$FAKE_CALLS"
port=""; prev=""
for a in "$@"; do [ "$prev" = "--port" ] && port="$a"; prev="$a"; done
exec python3 -c '
import http.server, sys
class H(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        self.send_response(200); self.end_headers(); self.wfile.write(b"{}")
    def log_message(self, *a): pass
http.server.HTTPServer(("127.0.0.1", int(sys.argv[1])), H).serve_forever()' "$port"
SH
# Fake claude / codex: log argv, the base URL env, and any mcp call; never print the environment.
for t in claude codex; do
cat > "$WORK/bin/$t" <<SH
#!/usr/bin/env bash
echo "$t \$*" >> "\$FAKE_CALLS"
echo "$t env ANTHROPIC_BASE_URL=\${ANTHROPIC_BASE_URL:-} OPENAI_BASE_URL=\${OPENAI_BASE_URL:-}" >> "\$FAKE_CALLS"
case "\${1:-}" in mcp) echo "MCP \$*" >> "\$FAKE_CALLS" ;; esac
exit 0
SH
done
chmod +x "$WORK"/bin/*
export FAKE_CALLS="$CALLS"
export PATH="$WORK/bin:$PATH"

# Provider configs. The auth values are secrets: no output of the wrapper may contain them.
cat > "$CLAUDE_SETTINGS_FILE" <<JSON
{"env": {"ANTHROPIC_BASE_URL": "http://127.0.0.1:8317", "ANTHROPIC_AUTH_TOKEN": "$SECRET"}}
JSON
cat > "$CODEX_HOME/config.toml" <<TOML
model_provider = "gw"
model = "m"
[projects."/x"]
trust_level = "trusted"
[model_providers.other]
base_url = "http://other.example/v1"
[model_providers.gw]
name = "Gateway"
base_url = "http://127.0.0.1:8317/v1"
wire_api = "responses"
experimental_bearer_token = "$SECRET"
TOML
sum_before="$(cat "$CLAUDE_SETTINGS_FILE" "$CODEX_HOME/config.toml" | sha256sum)"

# --- upstream detection: claude ---
check "claude upstream: settings.json env" "$(bash "$WRAP" upstream claude)" "http://127.0.0.1:8317"
check "claude upstream: env beats settings.json" "$(ANTHROPIC_BASE_URL=http://env.example/ bash "$WRAP" upstream claude)" "http://env.example"
check "claude upstream: headroom's own env URL is skipped" "$(ANTHROPIC_BASE_URL=http://127.0.0.1:$PC bash "$WRAP" upstream claude)" "http://127.0.0.1:8317"
check "claude upstream: localhost spelling of headroom's URL is skipped" "$(ANTHROPIC_BASE_URL=http://localhost:$PC/ bash "$WRAP" upstream claude)" "http://127.0.0.1:8317"
check "claude upstream: the codex proxy's URL is skipped too" "$(ANTHROPIC_BASE_URL=http://127.0.0.1:$PX bash "$WRAP" upstream claude)" "http://127.0.0.1:8317"
echo "{\"env\": {\"ANTHROPIC_BASE_URL\": \"http://127.0.0.1:$PC\"}}" > "$WORK/loop.json"
check "claude upstream: headroom's URL in settings.json falls to the default (no loop)" "$(CLAUDE_SETTINGS_FILE="$WORK/loop.json" bash "$WRAP" upstream claude)" "https://api.anthropic.com"
check "claude upstream: no env, no settings file: the official default" "$(CLAUDE_SETTINGS_FILE="$WORK/none.json" bash "$WRAP" upstream claude)" "https://api.anthropic.com"
echo '{"env":{}}' > "$WORK/e.json"
check "claude upstream: settings without a base URL: the default" "$(CLAUDE_SETTINGS_FILE="$WORK/e.json" bash "$WRAP" upstream claude)" "https://api.anthropic.com"

# --- upstream detection: codex ---
check "codex upstream: the active provider's base_url" "$(bash "$WRAP" upstream codex)" "http://127.0.0.1:8317/v1"
check "codex upstream: env beats config.toml" "$(OPENAI_BASE_URL=http://env.example/v1 bash "$WRAP" upstream codex)" "http://env.example/v1"
check "codex upstream: headroom's own env URL is skipped" "$(OPENAI_BASE_URL=http://127.0.0.1:$PX/v1 bash "$WRAP" upstream codex)" "http://127.0.0.1:8317/v1"
mkdir -p "$WORK/ch-loop" "$WORK/ch-top"
printf 'model_provider = "gw"\n[model_providers.gw]\nbase_url = "http://127.0.0.1:%s/v1"\n' "$PX" > "$WORK/ch-loop/config.toml"
check "codex upstream: headroom's URL in config.toml falls to the default (no loop)" "$(CODEX_HOME="$WORK/ch-loop" bash "$WRAP" upstream codex)" "https://api.openai.com/v1"
printf 'openai_base_url = "http://top.example/v1"\n[model_providers.gw]\nbase_url = "http://not-active/v1"\n' > "$WORK/ch-top/config.toml"
check "codex upstream: no provider set: top-level openai_base_url" "$(CODEX_HOME="$WORK/ch-top" bash "$WRAP" upstream codex)" "http://top.example/v1"
check "codex upstream: no config file: the official default" "$(CODEX_HOME="$WORK/ch-none" bash "$WRAP" upstream codex)" "https://api.openai.com/v1"
OUT="$(bash "$WRAP" upstream codex 2>&1; bash "$WRAP" upstream claude 2>&1)"
lacks "upstream detection never prints the token" "$OUT" "$SECRET"

# --- a wrapped Claude run ---
: > "$CALLS"
OUT="$(bash "$WRAP" claude -p "say ok" 2>&1)"; RC=$?
LOG="$(cat "$CALLS")"
check "claude run: exit 0" "$RC" "0"
contains "claude run: headroom proxy chains to the detected upstream" "$LOG" "headroom proxy --host 127.0.0.1 --port $PC --no-telemetry --anthropic-api-url http://127.0.0.1:8317"
contains "claude run: the tool gets headroom's URL by --settings overlay" "$LOG" "claude --settings {\"env\":{\"ANTHROPIC_BASE_URL\":\"http://127.0.0.1:$PC\"}} -p say ok"
contains "claude run: and in its environment" "$LOG" "claude env ANTHROPIC_BASE_URL=http://127.0.0.1:$PC"
lacks "claude run: the token is never printed or passed" "$OUT$LOG" "$SECRET"
check "claude run: the proxy is stopped when the last session ends" "$(bash "$WRAP" status | head -1)" "claude: not running"
check "claude run: no session files left" "$(ls "$AW_STATE_DIR/headroom/claude/users" 2>/dev/null | wc -l | tr -d ' ')" "0"
check "provider configs untouched" "$(cat "$CLAUDE_SETTINGS_FILE" "$CODEX_HOME/config.toml" | sha256sum)" "$sum_before"

# --- a wrapped Codex run: the /v1 prefix is stripped for headroom's target, the tool keeps /v1 ---
: > "$CALLS"
OUT="$(bash "$WRAP" codex exec "say ok" 2>&1)"; RC=$?
LOG="$(cat "$CALLS")"
check "codex run: exit 0" "$RC" "0"
contains "codex run: headroom proxy chains to the upstream origin" "$LOG" "--port $PX --no-telemetry --openai-api-url http://127.0.0.1:8317"
lacks "codex run: headroom's target has no /v1 (it appends the request path)" "$LOG" "--openai-api-url http://127.0.0.1:8317/v1"
contains "codex run: the active provider block's base_url is overridden for this run" "$LOG" "codex -c model_providers.gw.base_url=\"http://127.0.0.1:$PX/v1\" exec say ok"
lacks "codex run: the token is never printed or passed" "$OUT$LOG" "$SECRET"
check "codex run: the proxy is stopped afterwards" "$(bash "$WRAP" status | tail -1)" "codex: not running"

# --- reuse and keep ---
: > "$CALLS"
AW_HEADROOM_KEEP=1 bash "$WRAP" claude one >/dev/null 2>&1
AW_HEADROOM_KEEP=1 bash "$WRAP" claude two >/dev/null 2>&1
check "keep: one proxy start for two runs (the second reuses it)" "$(grep -c '^headroom proxy' "$CALLS")" "1"
contains "keep: status shows it running" "$(bash "$WRAP" status)" "claude: running"
bash "$WRAP" stop
check "stop: stops it" "$(bash "$WRAP" status | head -1)" "claude: not running"

# --- fail-open ---
: > "$CALLS"
mkdir -p "$WORK/nohr"
for t in claude codex jq; do ln -s "$(command -v $t)" "$WORK/nohr/$t"; done
OUT="$(PATH="$WORK/nohr:/usr/bin:/bin" HOME="$WORK/emptyhome" bash "$WRAP" claude -p hi 2>&1)"; RC=$?
LOG="$(cat "$CALLS")"
check "fail-open: no headroom, exit 0" "$RC" "0"
contains "fail-open: warns" "$OUT" "headroom is not installed"
contains "fail-open: the tool still runs, unwrapped" "$LOG" "claude -p hi"
lacks "fail-open: and with no overlay" "$LOG" "--settings"
: > "$CALLS"
python3 -c "import socket,time;s=socket.socket();s.setsockopt(socket.SOL_SOCKET,socket.SO_REUSEADDR,1);s.bind(('127.0.0.1',$PC));s.listen(1);time.sleep(6)" &
BUSY=$!; sleep 0.5
OUT="$(bash "$WRAP" claude -p hi 2>&1)"
contains "fail-open: a busy port is not taken over" "$OUT" "port $PC is taken"
contains "fail-open: the tool still runs" "$(cat "$CALLS")" "claude -p hi"
kill "$BUSY" 2>/dev/null; wait "$BUSY" 2>/dev/null

# --- the installer ---
find "$HOME" "$WORK/localbin" "$AW_STATE_DIR" 2>/dev/null | sort > "$WORK/tree-before"
: > "$CALLS"
OUT="$(bash "$INSTALL" --provider claude --dry-run 2>&1)"; RC=$?
check "dry run: exit 0" "$RC" "0"
contains "dry run: prints the launcher step" "$OUT" "[dry-run] would install"
find "$HOME" "$WORK/localbin" "$AW_STATE_DIR" 2>/dev/null | sort > "$WORK/tree-after"
check "dry run: writes nothing" "$(diff "$WORK/tree-before" "$WORK/tree-after" >/dev/null && echo same || echo changed)" "same"
OUT="$(AW_DRY_RUN=1 bash "$INSTALL" --provider codex 2>&1)"
contains "AW_DRY_RUN=1 is a dry run" "$OUT" "[dry-run] would install"
check "AW_DRY_RUN=1 writes nothing" "$([ -e "$AW_HEADROOM_BIN_DIR/aw-headroom" ] && echo wrote || echo none)" "none"
OUT="$(bash "$INSTALL" --provider cursor 2>&1)"; RC=$?
check "cursor: exit 0" "$RC" "0"
contains "cursor: says it gets no wrapper" "$OUT" "skipped for cursor"
check "cursor: writes nothing" "$([ -e "$AW_HEADROOM_BIN_DIR/aw-headroom" ] && echo wrote || echo none)" "none"
OUT="$(bash "$INSTALL" --provider claude 2>&1)"; RC=$?
check "install: exit 0" "$RC" "0"
check "install: the launcher is a copy of the repo's, executable" "$(cmp -s "$ROOT/scripts/aw-headroom.sh" "$AW_HEADROOM_BIN_DIR/aw-headroom" && [ -x "$AW_HEADROOM_BIN_DIR/aw-headroom" ] && echo yes || echo no)" "yes"
check "install again: idempotent" "$(bash "$INSTALL" --provider codex >/dev/null 2>&1; echo $?)" "0"
check "no mcp command was ever run by any step" "$(grep -c -i 'mcp' "$CALLS")" "0"
bash "$INSTALL" --uninstall >/dev/null 2>&1
check "uninstall: removes the launcher" "$([ -e "$AW_HEADROOM_BIN_DIR/aw-headroom" ] && echo left || echo gone)" "gone"

# headroom missing: the installer tries uv, and a failing install only warns.
mkdir -p "$WORK/nohr2"
cat > "$WORK/nohr2/uv" <<'SH'
#!/usr/bin/env bash
echo "uv $*" >> "$FAKE_CALLS"
exit 1
SH
chmod +x "$WORK/nohr2/uv"
for t in bash cp mkdir chmod dirname cat; do ln -sf "$(command -v $t)" "$WORK/nohr2/$t"; done
: > "$CALLS"
OUT="$(PATH="$WORK/nohr2" HOME="$WORK/emptyhome" "$WORK/nohr2/bash" "$INSTALL" --provider claude 2>&1)"; RC=$?
check "no headroom, failing install: still exit 0 (fail-open)" "$RC" "0"
contains "no headroom: tries uv tool install with the proxy extra" "$(cat "$CALLS")" "uv tool install headroom-ai[proxy]"
contains "no headroom: warns and goes on" "$OUT" "could not install"
OUT="$(PATH="$WORK/nohr2" HOME="$WORK/emptyhome" "$WORK/nohr2/bash" "$INSTALL" --provider claude --dry-run 2>&1)"
contains "no headroom, dry run: prints the install step" "$OUT" "[dry-run] would install headroom-ai[proxy]"

# --- headroom is never an MCP server in setup ---
check "setup.sh registers no headroom MCP server" "$(grep -c 'name: "headroom"' "$ROOT/setup.sh")" "0"
contains "setup.sh keeps headroom in AW_MCP_RETIRED" "$(grep '^AW_MCP_RETIRED=' "$ROOT/setup.sh")" "headroom"
check "the scripts never call mcp add or serve" "$(cat "$WRAP" "$INSTALL" | grep -c -E 'mcp (add|serve)')" "0"

exit "$fail"
