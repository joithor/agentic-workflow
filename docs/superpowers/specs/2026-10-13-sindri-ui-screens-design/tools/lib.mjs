// Shared by walk.mjs, more.mjs, extra.mjs, proto-dom.mjs, css-equiv.mjs.
// Playwright is resolved from the current directory (run these from skills/ui-evidence), not from this file's folder.
import { createRequire } from 'node:module';
import path from 'node:path';
import fs from 'node:fs';
import { fileURLToPath } from 'node:url';

export const here = path.dirname(fileURLToPath(import.meta.url));
export const specDir = path.resolve(here, '..');
export const { chromium } = createRequire(path.join(process.cwd(), 'noop.js'))('playwright');
export const PROTO = process.env.PROTO_URL || 'http://127.0.0.1:8766/prototype.html';
export const MOCK = process.env.MOCK_URL || 'http://127.0.0.1:8766/mockups/';

/** A tiny step runner: step(name, fn) prints PASS/FAIL, collects failures; done() prints the tally and sets the exit code. */
export function runner(label) {
  const results = [];
  return {
    results,
    async step(name, fn) {
      try { await fn(); results.push([name, true]); console.log(`PASS ${label}: ${name}`); }
      catch (e) { results.push([name, false]); console.log(`FAIL ${label}: ${name}\n     ${String(e.message || e).split('\n').slice(0, 4).join('\n     ')}`); }
    },
    done(errors = []) {
      const failed = results.filter(r => !r[1]).length;
      console.log(`${label}: ${results.length - failed}/${results.length} steps passed, ${errors.length} page errors`);
      for (const e of errors) console.log('  page error: ' + e);
      process.exitCode = failed || errors.length ? 1 : 0;
    },
  };
}

export function assert(cond, msg) { if (!cond) throw new Error(msg || 'assertion failed'); }
export function eq(a, b, msg) { if (a !== b) throw new Error(`${msg || 'expected equal'}: got ${JSON.stringify(a)}, want ${JSON.stringify(b)}`); }

/** The fixture's health sentence, read from fixture.md so the walk checks against the spec, not against itself. */
export function fixtureHealthSentence() {
  const md = fs.readFileSync(path.join(specDir, 'fixture.md'), 'utf8');
  const m = md.match(/\*\*The health sentence\*\*[^`]*`([^`]+)`/);
  if (!m) throw new Error('fixture.md: health sentence not found');
  return m[1].replace(/\s+/g, ' ').trim();
}

export async function open(browser, url = PROTO, viewport = { width: 1280, height: 900 }) {
  const page = await browser.newPage({ viewport });
  const errors = [];
  page.on('pageerror', e => errors.push(String(e)));
  page.on('console', m => { if (m.type() === 'error' && !/fonts\.(googleapis|gstatic)|ERR_(INTERNET|NAME|CONNECTION)/.test(m.text())) errors.push('console: ' + m.text()); });
  await page.goto(url, { waitUntil: 'load' });
  await page.evaluate(() => document.fonts && document.fonts.ready).catch(() => {});
  await page.waitForTimeout(450); // the prototype announces "N now need you" 400 ms after boot; let that settle first
  return { page, errors };
}

// ---- small page helpers shared by the click-through scripts
export const txt = (page, sel) => page.evaluate(s => { const e = document.querySelector(s); return e ? e.innerText.replace(/\s+/g, ' ').trim() : null; }, sel);
export const count = (page, sel) => page.evaluate(s => document.querySelectorAll(s).length, sel);
export const title = page => page.title();
export const hash = page => page.evaluate(() => location.hash);
/** what has focus: its data-row (or the row it sits in), data-act / data-id and text, for assertions */
export const focused = page => page.evaluate(() => {
  const a = document.activeElement; if (!a || a === document.body) return { none: true };
  const row = a.closest && a.closest('[data-row]');
  return { tag: a.tagName, id: a.id || '', row: a.dataset && a.dataset.row || (row ? row.dataset.row : ''), act: a.dataset && a.dataset.act || '', did: a.dataset && a.dataset.id || '', text: (a.innerText || '').replace(/\s+/g, ' ').trim().slice(0, 80), label: a.getAttribute('aria-label') || '' };
});
/** the prototype controls are a collapsed panel; the scripts drive its buttons directly */
export const panel = (page, sel) => page.evaluate(s => document.querySelector(s).click(), sel);
export const reset = async page => { await panel(page, '#optReset'); await page.waitForTimeout(80); };
export const setConn = async (page, conn) => { await panel(page, `#connStates [data-conn="${conn}"]`); await page.waitForTimeout(80); };
export const go = async (page, h) => { await page.evaluate(h => { location.hash = '#' + h; }, h); await page.waitForTimeout(120); };
export const live = page => page.evaluate(() => document.getElementById('live').textContent);
export const sleep = (page, ms) => page.waitForTimeout(ms);
