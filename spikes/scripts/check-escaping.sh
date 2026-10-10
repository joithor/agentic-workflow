#!/usr/bin/env bash
# §4c escaping probe: compare STJ's default encoder and UnsafeRelaxedJsonEscaping with Node's JSON.stringify,
# byte for byte, on json-escaping/corpus.jsonl. Prints one row per string: node / default / relaxed (= or the bytes).
set -u
corpus=json-escaping/corpus.jsonl
dotnet build json-escaping/JsonEscaping.csproj -c Release -o json-escaping/bin/out </dev/null >/dev/null 2>&1 || { echo "build failed"; exit 2; }
node -e 'const fs=require("fs");for(const l of fs.readFileSync(process.argv[1],"utf8").split("\n")){if(l)process.stdout.write(JSON.stringify(JSON.parse(l))+"\n")}' "$corpus" > json-escaping/bin/node.txt
dotnet json-escaping/bin/out/JsonEscaping.dll default "$corpus" > json-escaping/bin/default.txt 2> json-escaping/bin/reader.txt
dotnet json-escaping/bin/out/JsonEscaping.dll relaxed "$corpus" > json-escaping/bin/relaxed.txt 2>/dev/null
sed 's/^/STJ /' json-escaping/bin/reader.txt
python3 - <<'PY'
import json
c=[l.rstrip("\n") for l in open("json-escaping/corpus.jsonl")]
n=open("json-escaping/bin/node.txt","rb").read().split(b"\n")
d=open("json-escaping/bin/default.txt","rb").read().split(b"\n")
r=open("json-escaping/bin/relaxed.txt","rb").read().split(b"\n")
diff_d=diff_r=0
print(f"{'input (JSON literal)':28} {'node JSON.stringify':30} {'STJ default':34} {'STJ relaxed'}")
for i,lit in enumerate(c):
    nd, dd, rd = n[i], d[i], r[i]
    show=lambda b: b.decode('utf-8','backslashreplace')
    dcol = "=" if dd==nd else show(dd); rcol = "=" if rd==nd else show(rd)
    diff_d += dd!=nd; diff_r += rd!=nd
    print(f"{lit[:28]:28} {show(nd)[:30]:30} {dcol[:34]:34} {rcol}")
print(f"differences from node: default {diff_d}/{len(c)}, relaxed {diff_r}/{len(c)}")
PY
