#!/usr/bin/env python3
"""Drive an MCP stdio server: initialize, tools/list, tools/call (ok, bad args, unknown tool), then close stdin.

Usage: drive-stdio.py <server-binary> [args...]
Prints a transcript and the time to the initialize response; exits 0 only when every expectation holds.
"""
import json
import subprocess
import sys
import time


def main(cmd):
    t0 = time.monotonic()
    p = subprocess.Popen(cmd, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True)
    fails = []

    def send(msg):
        line = json.dumps(msg)
        print(">>", line)
        p.stdin.write(line + "\n")
        p.stdin.flush()

    def recv(want_id):
        while True:
            line = p.stdout.readline()
            if not line:
                raise SystemExit("server closed stdout")
            print("<<", line.strip()[:400])
            msg = json.loads(line)
            if msg.get("id") == want_id:
                return msg

    send({"jsonrpc": "2.0", "id": 1, "method": "initialize",
          "params": {"protocolVersion": "2025-06-18", "capabilities": {},
                     "clientInfo": {"name": "drive-stdio", "version": "0"}}})
    init = recv(1)
    first = time.monotonic() - t0
    if "result" not in init:
        fails.append("initialize")
    send({"jsonrpc": "2.0", "method": "notifications/initialized"})
    send({"jsonrpc": "2.0", "id": 2, "method": "tools/list"})
    tools = recv(2).get("result", {}).get("tools", [])
    if [t["name"] for t in tools] != ["upper"]:
        fails.append(f"tools/list {tools}")
    send({"jsonrpc": "2.0", "id": 3, "method": "tools/call",
          "params": {"name": "upper", "arguments": {"text": "hello aot"}}})
    r3 = recv(3).get("result", {})
    if r3.get("isError") or r3.get("content", [{}])[0].get("text") != "HELLO AOT":
        fails.append(f"call ok {r3}")
    send({"jsonrpc": "2.0", "id": 4, "method": "tools/call",
          "params": {"name": "upper", "arguments": {"text": 7}}})
    r4 = recv(4)
    if not (r4.get("result", {}).get("isError") or "error" in r4):
        fails.append(f"bad args {r4}")
    send({"jsonrpc": "2.0", "id": 5, "method": "tools/call", "params": {"name": "nope", "arguments": {}}})
    r5 = recv(5)
    if not (r5.get("result", {}).get("isError") or "error" in r5):
        fails.append(f"unknown tool {r5}")
    p.stdin.close()
    try:
        p.wait(timeout=10)
    except subprocess.TimeoutExpired:
        p.kill()
        fails.append("did not exit on stdin close")
    print(f"time-to-initialize-response: {first * 1000:.0f} ms; exit code {p.returncode}")
    for f in fails:
        print("FAIL", f)
    print("OK" if not fails else f"FAILED {len(fails)}")
    return 0 if not fails else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
