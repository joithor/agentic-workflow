// What the AOT smoke (Program.cs) must print, byte for byte: Node's own JSON.stringify of the same values.
const sample = {
  id: "smoke",
  at: new Date(Date.UTC(2026, 9, 10, 6, 0, 0, 123)).toISOString(),
  texts: ["</script>&'", 'tab\tquote"', "lone \ud800", "emoji \u{1F600}", "line\u2028end", "plus + backtick ` caf\u00e9 \u4e2d"],
  numbers: [0.1 + 0.2, 1e21, 1e-7, -0, 123.456],
  error: { code: "AW-KERNEL-002", kind: "notFound", message: "gone" },
  keys: { "caf\u00e9 + ` \u{1F600}": 1 },
};
const scalars = { when: "2026-10-10T06:00:00.000Z", money: 1.1, big: 9007199254740993, single: Math.fround(0.1), nan: NaN };
const untyped = String.raw`{"b":1.0,"1":2,"s":"caf\u00e9 \ud800","e":1E5,"dup":1,"dup":2}`;
process.stdout.write(
  JSON.stringify(sample) + "\n" + JSON.stringify(sample, null, 2) + "\nroundtrip:True\nbad:AW-KERNEL-001\n" +
    JSON.stringify(scalars) + "\n" + JSON.stringify(JSON.parse(untyped)) + "\n",
);
