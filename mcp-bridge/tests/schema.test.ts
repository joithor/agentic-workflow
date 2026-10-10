import { describe, it, expect, vi, afterEach } from "vitest";
import { createDatabase, MIGRATIONS } from "../src/db/schema.js";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { randomUUID } from "node:crypto";
import { existsSync, mkdtempSync, rmSync, unlinkSync } from "node:fs";

describe("createDatabase", () => {
  it("creates a database with WAL mode and tables", () => {
    const path = join(tmpdir(), `test-${randomUUID()}.db`);
    try {
      const db = createDatabase(path);
      // Verify WAL mode
      const mode = db.pragma("journal_mode", { simple: true });
      expect(mode).toBe("wal");
      // Verify tables exist
      const tables = db.prepare("SELECT name FROM sqlite_master WHERE type='table'").all() as Array<{ name: string }>;
      const tableNames = tables.map((t) => t.name);
      expect(tableNames).toContain("messages");
      expect(tableNames).toContain("tasks");
      db.close();
    } finally {
      try { unlinkSync(path); } catch {}
      try { unlinkSync(path + "-wal"); } catch {}
      try { unlinkSync(path + "-shm"); } catch {}
    }
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("creates bridge.db in the working directory when no path is given", () => {
    const dir = mkdtempSync(join(tmpdir(), "bridge-default-"));
    vi.spyOn(process, "cwd").mockReturnValue(dir);
    try {
      const db = createDatabase();
      expect(db.name).toBe(join(dir, "bridge.db"));
      expect(existsSync(join(dir, "bridge.db"))).toBe(true);
      db.close();
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  });

  it("MIGRATIONS creates both tables", () => {
    expect(MIGRATIONS).toContain("CREATE TABLE IF NOT EXISTS messages");
    expect(MIGRATIONS).toContain("CREATE TABLE IF NOT EXISTS tasks");
  });
});
