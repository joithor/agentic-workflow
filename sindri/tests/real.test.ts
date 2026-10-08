import os from "node:os";
import { describe, expect, it } from "vitest";

import { realSystemProbe } from "../src/system-real.js";

describe("realSystemProbe (smoke)", () => {
  it("answers for this process on this host", () => {
    const sys = realSystemProbe();
    expect(sys.hostname()).toBe(os.hostname());
    expect(sys.pid).toBe(process.pid);
    expect(sys.pidAlive(process.pid)).toBe(true);
    expect(sys.pidStartTime(process.pid)).not.toBeNull();
    if (process.platform === "darwin" || process.platform === "linux") expect(sys.bootId()).not.toBeNull();
    expect(sys.isLocalDisk(os.tmpdir())).not.toBe(false);
  });
});
