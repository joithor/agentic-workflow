import { describe, expect, it } from "vitest";

import { readManifestDeps, tagsFor } from "../src/index/deps-layer.js";

describe("dependency layer", () => {
  it("reads prod, dev and peer deps with purpose tags", () => {
    const text = JSON.stringify({ dependencies: { dayjs: "^1", zod: "^3" }, devDependencies: { vitest: "^2" }, peerDependencies: { react: "^18" } });
    expect(readManifestDeps("package.json", text)).toEqual([
      { manifest: "package.json", name: "dayjs", version: "^1", kind: "prod", tags: ["date"] },
      { manifest: "package.json", name: "zod", version: "^3", kind: "prod", tags: ["validation"] },
      { manifest: "package.json", name: "vitest", version: "^2", kind: "dev", tags: ["test"] },
      { manifest: "package.json", name: "react", version: "^18", kind: "peer", tags: [] },
    ]);
  });

  it("returns nothing for invalid or empty manifests", () => {
    expect(readManifestDeps("p/package.json", "{not json")).toEqual([]);
    expect(readManifestDeps("p/package.json", "[]")).toEqual([]);
    expect(readManifestDeps("p/package.json", JSON.stringify({ dependencies: { a: 1 } }))).toEqual([]);
  });

  it("tags known packages, including scoped ones by bare name", () => {
    expect(tagsFor("moment")).toEqual(["date"]);
    expect(tagsFor("@types/node")).toEqual([]);
    expect(tagsFor("left-pad")).toEqual([]);
  });
});
