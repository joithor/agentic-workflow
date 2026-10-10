// Regenerates the Node goldens the C# byte-compat tests compare against (agent-readiness spec §4c, A1).
// Run from the repo root: node tests/Aw.Kernel.Tests/Golden/make-goldens.mjs
// The values here and in NodeJsonFixture.cs must describe the same object.
import { writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));

const fixture = {
  id: "evt-0001",
  // 2026-10-10T06:37:59.007Z, written by Date#toISOString (UTC, exactly 3 fractional digits).
  createdAt: new Date(Date.UTC(2026, 9, 10, 6, 37, 59, 7)).toISOString(),
  // A local time with an offset in C#; Node always writes the UTC instant.
  updatedAt: new Date(Date.UTC(2026, 0, 2, 1, 4, 5, 600)).toISOString(),
  count: 3,
  ratio: 0.1 + 0.2,
  flags: [true, false],
  // `omitted` is undefined in TS, so JSON.stringify leaves the key out.
  explicitNull: null,
  strings: [
    "plain",
    'quote " backslash \\ slash /',
    "</script><b>&'",
    "emoji \u{1F600} \u{1F9EA}",
    "line\u2028para\u2029end",
    "lone \ud800 high",
    "lone \udc00 low",
    "ctrl \u0000\u0001\b\t\n\u000b\f\r\u001f\u007f",
    "caf\u00e9 \u00f1 \u4e2d\u6587",
    "plus + backtick ` tilde ~ at @ hash #",
    "",
  ],
  numbers: [
    0, -0, 1, -1, 0.1 + 0.2, 1 / 3, 1e21, 1e-7, 1e-6, 123456789012345680000, 5e-324,
    1.7976931348623157e308, 2 ** 53, 100, 1.5, -2.5e-10, 0.000001234, 1e300, 4.35, 0.5e-6,
  ],
  nested: { zeta: 1, alpha: 2 },
  labels: { "caf\u00e9 </x>": "v", "\u4e2d + ` \u{1F600}": "k", plain: "w" },
  emptyList: [],
  emptyObject: {},
};

writeFileSync(join(here, "fixture.compact.json"), JSON.stringify(fixture));
writeFileSync(join(here, "fixture.indented.json"), JSON.stringify(fixture, null, 2));

// Untyped JSON: what judge's input_digest hashes on its failure path, JSON.stringify(JSON.parse(raw)) (spec §4c).
// Number text is re-laid out, integer-like keys move first, duplicate keys keep the first position and the last
// value, escapes are re-written the Node way (a lone surrogate stays escaped).
const untypedInput =
  String.raw`{"b":1.0,"1":2,"s":"caf\u00e9 <&> \/ \ud800 lone ` +
  "caf\u00e9 \u4e2d \u{1F600}" +
  String.raw`","e":1E5,"10":"ten","2":"two","01":"lead","-1":"neg","4294967295":"max","4294967294":"idx",` +
  String.raw`"dup":1,"nested":{"z":[1.50,-0,1e400,-1e-7,0.1e1,123456789012345678901234,[],{}],"a":{"9":null,"x":true}},` +
  String.raw`"dup":2,"pair":"\ud83d\ude00","ctl":"\u0001\u001f\t","esc":"\"\\","\udc00 key":false}`;
const untyped = JSON.parse(untypedInput);
writeFileSync(join(here, "untyped.input.json"), untypedInput);
writeFileSync(join(here, "untyped.compact.json"), JSON.stringify(untyped));
writeFileSync(join(here, "untyped.indented.json"), JSON.stringify(untyped, null, 2));
// The same untyped value inside a typed record (a JsonElement property and a JsonNode property).
writeFileSync(join(here, "wrapped.compact.json"), JSON.stringify({ name: "w", data: untyped, node: untyped }));

// The event envelope as it is stored and sent. causationId and traceId are undefined, so they are left out.
const envelope = {
  id: "00000000-0000-0000-0000-000000000001",
  type: "item.started",
  schemaVersion: 1,
  occurredAt: new Date(Date.UTC(2026, 9, 10, 6, 0, 0, 0)).toISOString(),
  actor: "host:sindri",
  tenant: "personal",
  ring: 0,
  correlationId: "00000000-0000-0000-0000-0000000000c1",
  payload: { item: "item-7" },
};
writeFileSync(join(here, "envelope.compact.json"), JSON.stringify(envelope));

// Scalars a C# record may hold, as Node would hold and write them: every number is a double.
const scalars = {
  when: new Date(Date.UTC(2026, 9, 10, 6, 0, 0, 0)).toISOString(),
  money: 1.10,
  whole: 1.0,
  big: 9007199254740993,
  single: Math.fround(0.1),
  huge: 9223372036854775808,
  nan: NaN,
  maybe: null,
};
writeFileSync(join(here, "scalars.compact.json"), JSON.stringify(scalars));
