#!/usr/bin/env python3
"""Capture the TS bridge's stdio MCP surface (the parity target for the C# Bridge): the initialize result,
one tool's inputSchema from tools/list, and the replies to a call with bad arguments and to an unknown tool.
Runs against a temp DB (DB_PATH), so it writes nothing real.
Usage: capture-ts-bridge-mcp.py <repo-root>"""
import json, os, subprocess, sys, tempfile

root = sys.argv[1]
env = dict(os.environ, DB_PATH=os.path.join(tempfile.mkdtemp(), "bridge.db"))
p = subprocess.Popen(["node", os.path.join(root, "mcp-bridge/dist/mcp.js")], stdin=subprocess.PIPE,
                     stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True, env=env)

def rpc(i, method, params=None):
    msg = {"jsonrpc": "2.0", "id": i, "method": method}
    if params is not None:
        msg["params"] = params
    p.stdin.write(json.dumps(msg) + "\n"); p.stdin.flush()
    while True:
        m = json.loads(p.stdout.readline())
        if m.get("id") == i:
            return m

init = rpc(1, "initialize", {"protocolVersion": "2025-06-18", "capabilities": {}, "clientInfo": {"name": "capture", "version": "0"}})
p.stdin.write(json.dumps({"jsonrpc": "2.0", "method": "notifications/initialized"}) + "\n"); p.stdin.flush()
tools = rpc(2, "tools/list")["result"]["tools"]
first = tools[0]
print("initialize.serverInfo:", json.dumps(init["result"]["serverInfo"]))
print("tools:", ", ".join(t["name"] for t in tools))
print(f"tools[0] = {first['name']} inputSchema:")
print(json.dumps(first["inputSchema"], indent=1))
bad = rpc(3, "tools/call", {"name": first["name"], "arguments": {"__bogus": 7}})
print("bad args reply:", json.dumps(bad)[:600])
unknown = rpc(4, "tools/call", {"name": "nope", "arguments": {}})
print("unknown tool reply:", json.dumps(unknown)[:600])
p.stdin.close(); p.wait(timeout=10)
