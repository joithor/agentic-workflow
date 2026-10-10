#!/usr/bin/env python3
"""Print the nuget.org publish date of package versions. Usage: nuget-dates.py id@version ... | --lock FILE..."""
import gzip, json, sys, urllib.request
cache = {}
def versions(pid):
    pid = pid.lower()
    if pid in cache: return cache[pid]
    def get(u):
        b = urllib.request.urlopen(u, timeout=30).read()
        try: b = gzip.decompress(b)
        except OSError: pass
        return json.loads(b)
    d = get(f"https://api.nuget.org/v3/registration5-gz-semver2/{pid}/index.json")
    out = {}
    for pg in d["items"]:
        items = pg.get("items") or get(pg["@id"])["items"]
        for it in items:
            ce = it["catalogEntry"]; out[ce["version"].lower()] = ce["published"][:10]
    cache[pid] = out; return out
pairs = []
args = sys.argv[1:]
if args and args[0] == "--lock":
    for f in args[1:]:
        for tfm in json.load(open(f))["dependencies"].values():
            for pid, info in tfm.items():
                if info.get("type") != "Project": pairs.append((pid, info["resolved"]))
else:
    pairs = [tuple(a.split("@")) for a in args]
for pid, v in sorted(set(pairs), key=lambda p: p[0].lower()):
    print(f"{pid} {v} {versions(pid).get(v.lower(), 'NOT-FOUND')}")
