#!/usr/bin/env bash
# Drive the HttpSlim MCP spike (spec §4d loopback-in-code, §4e B12 Host check, MCP Origin check).
#   1. Bind-override vectors: start the server with each vector pointing at 0.0.0.0:<port+1> and list its
#      listening sockets with lsof. Hardened mode must listen on 127.0.0.1:<port> only, for every vector.
#      Baseline mode (--baseline, the first spike's setup) is run too, to show which vectors it let through.
#   2. Host and Origin: a foreign Host must get 400 and a foreign Origin 403; loopback ones and no Origin pass.
#   3. MCP over Streamable HTTP: initialize, tools/list, tools/call (ok + bad args).
# Usage: drive-http.sh <HttpSlim-binary> [port]. Exit 0 only when every hardened-mode check holds.
set -u
bin="$1"; port="${2:-5199}"; other=$((port + 1))
fails=0
now_ms() { perl -MTime::HiRes=time -e 'printf "%d", time()*1000'; }
pid=""
start() { # mode env-assignments... -- extra-args...
  local mode="$1"; shift
  local envs=() extra=()
  while [ $# -gt 0 ] && [ "$1" != "--" ]; do envs+=("$1"); shift; done
  [ $# -gt 0 ] && shift
  extra=("$@")
  local flag=(); [ "$mode" = baseline ] && flag=(--baseline)
  env "${envs[@]+"${envs[@]}"}" "$bin" "$port" "${flag[@]+"${flag[@]}"}" "${extra[@]+"${extra[@]}"}" </dev/null >/dev/null 2>&1 &
  pid=$!
  for _ in $(seq 1 100); do
    lsof -nP -a -p "$pid" -iTCP -sTCP:LISTEN >/dev/null 2>&1 && break
    kill -0 "$pid" 2>/dev/null || break
    sleep 0.05
  done
  sleep 0.2   # let every configured endpoint finish binding
}
stop() { [ -n "$pid" ] && { kill "$pid" 2>/dev/null; wait "$pid" 2>/dev/null; }; pid=""; }
trap stop EXIT
listeners() { lsof -nP -a -p "$pid" -iTCP -sTCP:LISTEN 2>/dev/null | awk 'NR>1{print $9}' | sort -u | tr '\n' ' '; }

echo "## 1. bind-override vectors (target 0.0.0.0:${other}; expected listener 127.0.0.1:${port} only)"
vectors=(
  "ASPNETCORE_URLS|ASPNETCORE_URLS=http://0.0.0.0:${other} --"
  "DOTNET_URLS|DOTNET_URLS=http://0.0.0.0:${other} --"
  "ASPNETCORE_HTTP_PORTS|ASPNETCORE_HTTP_PORTS=${other} --"
  "ASPNETCORE_PREFERHOSTINGURLS+URLS|ASPNETCORE_PREFERHOSTINGURLS=true ASPNETCORE_URLS=http://0.0.0.0:${other} --"
  "env Kestrel__Endpoints|Kestrel__Endpoints__X__Url=http://0.0.0.0:${other} --"
  "arg --Kestrel:Endpoints|-- --Kestrel:Endpoints:X:Url=http://0.0.0.0:${other}"
  "arg --urls|-- --urls=http://0.0.0.0:${other}"
)
printf '%-36s %-34s %s\n' vector baseline hardened
for v in "${vectors[@]}"; do
  name=${v%%|*}; spec=${v#*|}
  # shellcheck disable=SC2086
  start baseline $spec; b=$(listeners); stop
  # shellcheck disable=SC2086
  start hardened $spec; h=$(listeners); stop
  printf '%-36s %-34s %s\n' "$name" "${b:-none}" "${h:-none}"
  [ "$h" = "127.0.0.1:${port} " ] || { echo "FAIL hardened listeners for $name: $h"; fails=$((fails + 1)); }
done

post() { # extra-curl-args... ; prints "code body"
  curl -s -m 5 -w '\n%{http_code}' -H 'Content-Type: application/json' -H 'Accept: application/json, text/event-stream' \
    "$@" "http://127.0.0.1:${port}/mcp" \
    -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"curl","version":"0"}}}'
}
code_of() { local out; out=$(post "$@"); echo "${out##*$'\n'}"; }

echo "## 2. Host and Origin (initialize request)"
printf '%-40s %-9s %s\n' case baseline hardened
cases=(
  "no Origin, Host 127.0.0.1|200|"
  "Host: localhost|200|-H Host:localhost:${port}"
  "Host: evil.example (DNS rebinding)|400|-H Host:evil.example"
  "Origin: http://127.0.0.1|200|-H Origin:http://127.0.0.1:${port}"
  "Origin: http://evil.example|403|-H Origin:http://evil.example"
  "Origin: null|403|-H Origin:null"
)
start baseline --; declare -a bcodes=(); for c in "${cases[@]}"; do IFS='|' read -r _ _ a <<<"$c"; bcodes+=("$(code_of $a)"); done; stop
t0=$(now_ms); start hardened --; t1=$(now_ms)
i=0
for c in "${cases[@]}"; do
  IFS='|' read -r label want a <<<"$c"
  # shellcheck disable=SC2086
  got=$(code_of $a)
  printf '%-40s %-9s %s\n' "$label" "${bcodes[$i]}" "$got"
  [ "$got" = "$want" ] || { echo "FAIL $label: expected $want"; fails=$((fails + 1)); }
  i=$((i + 1))
done

echo "## 3. MCP over Streamable HTTP (hardened)"
call() { # label body expect-regex
  local out
  out=$(curl -s -m 5 -H 'Content-Type: application/json' -H 'Accept: application/json, text/event-stream' \
    "http://127.0.0.1:${port}/mcp" -d "$2" | head -c 2000)
  echo "$1: $(echo "$out" | grep '^data:' | cut -c1-300)"
  echo "$out" | grep -qE "$3" || { echo "FAIL $1"; fails=$((fails + 1)); }
}
call initialize '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"curl","version":"0"}}}' '"serverInfo"'
call tools/list '{"jsonrpc":"2.0","id":2,"method":"tools/list"}' '"name":"upper"'
call 'tools/call upper' '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"upper","arguments":{"text":"hello aot"}}}' '"text":"HELLO AOT"'
call 'tools/call bad args' '{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"upper","arguments":{"text":7}}}' '"isError":true|"error"'
stop
echo "time-to-listening (hardened, includes lsof polling): $((t1 - t0)) ms"
[ "$fails" -eq 0 ] && echo OK || echo "FAILED $fails"
[ "$fails" -eq 0 ]
