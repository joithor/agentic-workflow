import { describe, expect, it } from "vitest";
import { record } from "../src/audit/audit-log";
import { auditReport } from "../src/audit/report";

describe("audit report", () => {
  it("counts entries per action", () => {
    record("ana", "note.save", "n9");
    expect(auditReport("2000-01-01", "2999-01-01")["note.save"]).toBeGreaterThan(0);
  });
});
