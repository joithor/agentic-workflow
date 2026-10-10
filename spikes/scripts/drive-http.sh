#!/usr/bin/env bash
# Drive the HttpSlim MCP spike: start it with ASPNETCORE_URLS pointing at 0.0.0.0:<other port> (which the
# code must ignore), list its listening sockets, then initialize / tools/list / tools/call (ok + bad args)
# over Streamable HTTP, and check the env-configured port is refused. Exit 0 only when every check holds.
# Usage: drive-http.sh <HttpSlim-binary> [port]
set -u
bin="$1"; port="${2:-5199}"; other=$((port + 1))
ASPNETCORE_URLS="http://0.0.0.0:${other}" "$bin" "$port" </dev/null >/dev/null 2>&1 &
pid=$!
trap 'kill "$pid" 2>/dev/null; wait "$pid" 2>/dev/null' EXIT
now_ms() { perl -MTime::HiRes=time -e 'printf "%d", time()*1000'; }
t0=$(now_ms)
for _ in $(seq 1 100); do curl -s -o /dev/null "http://127.0.0.1:${port}/mcp" && break; sleep 0.05; done
t1=$(now_ms)
echo "time-to-listening: $((t1 - t0)) ms (includes curl poll granularity, 50 ms)"
fails=0
echo "## listening sockets (ASPNETCORE_URLS=http://0.0.0.0:${other} in env)"
lsof -nP -a -p "$pid" -iTCP -sTCP:LISTEN | awk 'NR>1{print $9}'
lsof -nP -a -p "$pid" -iTCP -sTCP:LISTEN | awk 'NR>1{print $9}' | grep -qvx "127.0.0.1:${port}" && { echo "FAIL non-loopback listener"; fails=$((fails+1)); }
call() { # id json-body expect-regex
  local out
  out=$(curl -s -m 5 -H 'Content-Type: application/json' -H 'Accept: application/json, text/event-stream' \
    "http://127.0.0.1:${port}/mcp" -d "$2" | head -c 2000)
  echo "## $1"; echo "$out" | grep '^data:' | cut -c1-400
  echo "$out" | grep -qE "$3" || { echo "FAIL $1"; fails=$((fails+1)); }
}
call initialize '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"curl","version":"0"}}}' '"serverInfo"'
call tools/list '{"jsonrpc":"2.0","id":2,"method":"tools/list"}' '"name":"upper"'
call 'tools/call upper' '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"upper","arguments":{"text":"hello aot"}}}' '"text":"HELLO AOT"'
call 'tools/call bad args' '{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"upper","arguments":{"text":7}}}' '"isError":true|"error"'
echo "## env-configured port ${other}"
if curl -s -m 2 -o /dev/null "http://127.0.0.1:${other}/mcp"; then echo "FAIL port ${other} answered"; fails=$((fails+1)); else echo "refused (curl exit $?)"; fi
[ "$fails" -eq 0 ] && echo OK || echo "FAILED $fails"
[ "$fails" -eq 0 ]
