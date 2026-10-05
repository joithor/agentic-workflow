// End-to-end runParity against a local page: a design PNG captured from the
// page itself must diff at 0%, and a wrong design or wrong geometry must show.
import fs from "node:fs";
import http from "node:http";
import os from "node:os";
import path from "node:path";

import { chromium } from "playwright";
import { PNG } from "pngjs";
import { afterAll, beforeAll, describe, expect, it } from "vitest";

import { parseDesignManifest, type DesignManifest } from "../../src/parity-schema.js";
import { runParity } from "../../src/parity-run.js";

const PAGE = `<!doctype html><html><body style="margin:0;font:16px sans-serif">
<h1 style="margin:0;position:absolute;left:300px;top:5px;font-size:14px" id="h">Hours</h1>
<div data-testid="card" style="position:absolute;left:40px;top:30px;width:200px;height:100px;background:#def;border:1px solid #369">
  <span id="msg">Closed</span>
  <button data-testid="toggle" onclick="document.getElementById('msg').textContent='Open all day'">Toggle</button>
  <input data-testid="name" value="x" />
</div></body></html>`;
const VIEWPORT = { w: 600, h: 400 };
const REGION = { x: 40, y: 30, w: 202, h: 102 }; // 200x100 content + 1px border each side

let host: string;
let close: () => Promise<void>;
let tmp: string;

beforeAll(async () => {
  const server = http.createServer((_q, r) => { r.setHeader("content-type", "text/html"); r.end(PAGE); });
  await new Promise<void>((res) => server.listen(0, "127.0.0.1", res));
  host = `http://127.0.0.1:${(server.address() as { port: number }).port}`;
  close = () => new Promise((r) => server.close(() => r()));
  tmp = fs.mkdtempSync(path.join(os.tmpdir(), "parity-run-"));
});
afterAll(async () => {
  await close();
  fs.rmSync(tmp, { recursive: true, force: true });
});

/** Independent capture of the page, optionally after clicking the toggle, to stand in for a Figma export. */
async function designPng(file: string, clicked: boolean, formValue?: string): Promise<string> {
  const browser = await chromium.launch();
  try {
    const page = await (await browser.newContext({ viewport: { width: VIEWPORT.w, height: VIEWPORT.h }, deviceScaleFactor: 1 })).newPage();
    await page.goto(host);
    if (formValue !== undefined) await page.getByTestId("name").fill(formValue);
    if (clicked) await page.getByTestId("toggle").click();
    await page.evaluate(() => (document.activeElement as HTMLElement | null)?.blur());
    await page.mouse.click(VIEWPORT.w - 22, VIEWPORT.h - 22);
    await page.waitForTimeout(100);
    const out = path.join(tmp, file);
    await page.screenshot({ path: out });
    return out;
  } finally {
    await browser.close();
  }
}

const manifest = (over: Record<string, unknown>): DesignManifest => {
  const m = parseDesignManifest({ route: "/", ready: { text: "Hours" }, viewport: VIEWPORT, settleMs: 50, timeoutMs: 1500, ...over });
  if ("error" in m) throw new Error(m.error);
  return m;
};
const opts = () => ({ host, hostKind: "local" as const });

describe("runParity", () => {
  it("writes four PNGs per state plus parity.json, with 0% diff against its own capture and passing geometry", async () => {
    const a = await designPng("a.png", false);
    const b = await designPng("b.png", true);
    const dir = path.join(tmp, "run-ok");
    const s = await runParity(
      manifest({
        designNode: "1:2",
        frames: [
          { name: "A-closed", designPng: a, designRegion: REGION, anchor: { testId: "card" }, expectBoxes: [{ target: { testId: "card" }, expected: { w: 202, h: 102, x: 40, y: 30 } }], knownDifferences: ["none really"] },
          { name: "B-open", designPng: b, designRegion: REGION, anchor: { testId: "card" }, steps: [{ action: "click", target: "toggle", expectedState: { kind: "text-visible", text: "Open all day" } }] },
        ],
      }),
      dir,
      { ...opts(), appBuild: "abc1234" },
    );
    for (const n of ["A-closed", "B-open"]) {
      for (const f of ["design", "implementation", "diff", "side-by-side", "full"]) expect(fs.existsSync(path.join(dir, `${n}-${f}.png`))).toBe(true);
    }
    const sbs = PNG.sync.read(fs.readFileSync(path.join(dir, "A-closed-side-by-side.png")));
    expect(sbs.width).toBe(REGION.w * 3 + 24);
    expect(s.frames["A-closed"]).toMatchObject({ diffPercent: 0, sizeDelta: { w: 0, h: 0 }, knownDifferences: ["none really"] });
    expect(s.frames["A-closed"]?.boxChecks[0]).toMatchObject({ pass: true });
    expect(s.frames["B-open"]?.diffPercent).toBe(0);
    expect(s).toMatchObject({ provenance: "unknown", appBuild: "abc1234", designNode: "1:2" });
    expect(JSON.parse(fs.readFileSync(path.join(dir, "parity.json"), "utf8")).frames["B-open"].diffPixels).toBe(0);
  });

  it("shows a real difference and a failing geometry check instead of hiding them", async () => {
    const wrong = await designPng("wrong.png", true);
    const dir = path.join(tmp, "run-diff");
    const s = await runParity(
      manifest({ frames: [{ name: "A", designPng: wrong, designRegion: REGION, anchor: { testId: "card" }, expectBoxes: [{ target: { testId: "card" }, expected: { w: 199 } }] }] }),
      dir,
      opts(),
    );
    expect(s.frames.A?.diffPixels).toBeGreaterThan(0);
    expect(s.frames.A?.boxChecks[0]).toMatchObject({ pass: false, failure: "w 202 != 199" });
  });

  it("types form values to match the design without any submit", async () => {
    const typed = await designPng("typed.png", false, "Orange Coast");
    const s = await runParity(
      manifest({ frames: [{ name: "T", designPng: typed, designRegion: REGION, anchor: { testId: "card" }, formValues: [{ testId: "name", value: "Orange Coast" }] }] }),
      path.join(tmp, "run-typed"),
      opts(),
    );
    expect(s.frames.T?.diffPercent).toBe(0);
  });

  it("fails clearly when an anchor, a step or the ready element is missing", async () => {
    const a = await designPng("a2.png", false);
    const frame = { name: "X", designPng: a, designRegion: REGION, anchor: { testId: "card" } };
    await expect(runParity(manifest({ frames: [{ ...frame, anchor: { testId: "missing" } }] }), path.join(tmp, "r1"), opts())).rejects.toThrow(/anchor testId:missing not found/);
    const step = { action: "click", target: "toggle", expectedState: { kind: "text-visible", text: "never shown" } };
    await expect(runParity(manifest({ frames: [{ ...frame, steps: [step] }] }), path.join(tmp, "r2"), opts())).rejects.toThrow(/step 1 \(click toggle\) failed/);
    await expect(runParity(manifest({ ready: { text: "Nope" }, frames: [frame] }), path.join(tmp, "r3"), opts())).rejects.toThrow();
  });

  it("refuses to start without credentials in the environment and never echoes values", async () => {
    const a = await designPng("a3.png", false);
    const login = { emailEnv: "PARITY_TEST_EMAIL", passwordEnv: "PARITY_TEST_PASSWORD", emailSelector: "input", passwordSelector: "input", submitSelector: "button" };
    delete process.env.PARITY_TEST_EMAIL;
    delete process.env.PARITY_TEST_PASSWORD;
    await expect(runParity(manifest({ login, frames: [{ name: "L", designPng: a, designRegion: REGION, anchor: { testId: "card" } }] }), path.join(tmp, "r4"), opts())).rejects.toThrow("missing credentials in environment: PARITY_TEST_EMAIL, PARITY_TEST_PASSWORD");
  });

  it("signs in from env credentials and redacts them from any error message", async () => {
    const a = await designPng("a4.png", false);
    process.env.PARITY_TEST_EMAIL = "pat@example.com";
    process.env.PARITY_TEST_PASSWORD = "hunter2-secret";
    try {
      // The fixture page has no real login form: these selectors just exercise the sign-in path.
      const login = { emailEnv: "PARITY_TEST_EMAIL", passwordEnv: "PARITY_TEST_PASSWORD", emailSelector: "[data-testid=name]", passwordSelector: "[data-testid=name]", submitSelector: "[data-testid=toggle]", loginPath: "/never" };
      const frame = { name: "L", designPng: a, designRegion: REGION, anchor: { testId: "card" } };
      const ok = await runParity(manifest({ login, frames: [frame] }), path.join(tmp, "r5"), opts());
      expect(JSON.stringify(ok)).not.toContain("hunter2-secret");
      // A failure whose text echoes a secret (here via the step target) never reaches the caller unredacted.
      const step = { action: "click", target: "pat@example.com", expectedState: { kind: "text-visible", text: "x" } };
      const err = await runParity(manifest({ login, frames: [{ ...frame, steps: [step] }] }), path.join(tmp, "r6"), opts()).catch((e: Error) => e);
      expect((err as Error).message).toMatch(/step 1 \(click \[redacted\]\)/);
      expect((err as Error).message).not.toContain("pat@example.com");
    } finally {
      delete process.env.PARITY_TEST_EMAIL;
      delete process.env.PARITY_TEST_PASSWORD;
    }
  });
});
