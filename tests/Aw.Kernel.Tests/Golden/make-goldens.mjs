// Regenerates the Node goldens the C# byte-compat tests compare against (agent-readiness spec \u00a74c, A1).
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
    "",
  ],
  numbers: [
    0, -0, 1, -1, 0.1 + 0.2, 1 / 3, 1e21, 1e-7, 1e-6, 123456789012345680000, 5e-324,
    1.7976931348623157e308, 2 ** 53, 100, 1.5, -2.5e-10, 0.000001234, 1e300, 4.35, 0.5e-6,
  ],
  nested: { zeta: 1, alpha: 2 },
  labels: { "caf\u00e9 </x>": "v", plain: "w" },
  emptyList: [],
  emptyObject: {},
};

writeFileSync(join(here, "fixture.compact.json"), JSON.stringify(fixture));
writeFileSync(join(here, "fixture.indented.json"), JSON.stringify(fixture, null, 2));
