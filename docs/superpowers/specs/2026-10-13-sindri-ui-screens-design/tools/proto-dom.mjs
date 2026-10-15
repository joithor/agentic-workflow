// DOM state checks over the prototype: a list of states (pages, sheets, inspector, connection states, large fixture, text scale)
// each measured at 1280 wide. Per state: scrollWidth <= 1280, health pill right <= 1264, no text under 11 px, no em dash except
// a lone "—" (tabular null), no left border wider than 1 px, no attention band, no "waiting on you", no page errors, and the
// chip audit: every .chip's word and colour class against the table in fixture.md "State chips".
//   cd skills/ui-evidence && node ../../docs/superpowers/specs/2026-10-13-sindri-ui-screens-design/tools/proto-dom.mjs [--json]
import fs from 'node:fs';
import path from 'node:path';
import { chromium, open, specDir, setConn, panel, reset, go } from './lib.mjs';

/** fixture.md "State chips" table -> { WORD: 'attn'|'fail'|'ok'|'' } (the class after `chip`) */
function chipTable() {
  const md = fs.readFileSync(path.join(specDir, 'fixture.md'), 'utf8');
  const sec = md.slice(md.indexOf('## State chips'));
  const table = {};
  for (const line of sec.split('\n')) {
    const m = line.match(/^\|\s*((?:`[^`]+`(?:,\s*)?)+)\s*\|\s*`chip(?: (attn|fail|ok))?`\s*\|/);
    if (!m) continue;
    for (const w of m[1].match(/`([^`]+)`/g)) table[w.replace(/`/g, '')] = m[2] || '';
  }
  if (Object.keys(table).length < 19) throw new Error('fixture.md chip table: found only ' + Object.keys(table).length + ' words');
  return table;
}

const STATES = [
  // pages
  ['home', async () => {}], ['workflows', async p => go(p, 'workflows')], ['work', async p => go(p, 'work')], ['recipes', async p => go(p, 'recipes')],
  ['context (graph)', async p => go(p, 'context')], ['context (file map)', async p => go(p, 'context/files')], ['agents', async p => go(p, 'agents')], ['system', async p => go(p, 'system')],
  // sheets
  ['item sheet: running, terminal (JE7)', async p => go(p, 'item/ITM-01JE7')], ['item sheet: parked (JC8)', async p => go(p, 'item/ITM-01JC8')],
  ['item sheet: escalated (JB4)', async p => go(p, 'item/ITM-01JB4')], ['item sheet: decision (JD2)', async p => go(p, 'item/ITM-01JD2')],
  ['item sheet: with you (JA1)', async p => go(p, 'item/ITM-01JA1')], ['item sheet: tool step (JD6)', async p => go(p, 'item/ITM-01JD6')], ['item sheet: queued (JF0)', async p => go(p, 'item/ITM-01JF0')],
  ['workflow sheet: INC-14', async p => go(p, 'workflow/INC-14')], ['workflow sheet: PRJ-sync', async p => go(p, 'workflow/PRJ-sync')],
  ['workflow sheet: sentry #8812 (desk)', async p => go(p, 'workflow/sentry%20%238812')], ['workflow sheet: PRJ-exports (with you)', async p => go(p, 'workflow/PRJ-exports')], ['workflow sheet: P-2211 (escalated)', async p => go(p, 'workflow/P-2211')],
  // inspector
  ['recipe inspector: workflow.item', async p => go(p, 'recipe/workflow.item')], ['recipe inspector: scoping.project (changed)', async p => go(p, 'recipe/scoping.project')], ['recipe inspector: intake.linear (draft)', async p => go(p, 'recipe/intake.linear')],
  ['context node sheet', async p => go(p, 'node/feature%3Atabs.tab-manager')], ['system doctor sheet: code index', async p => go(p, 'system/code%20index')],
  // connection states (on Home)
  ...['notrunning', 'connecting', 'mismatch', 'stale', 'rebuilding', 'stalled'].map(c => [`connection: ${c}`, async p => setConn(p, c)]),
  // large fixture and text scale
  ['large fixture: home', async p => { await p.evaluate(() => { const c = document.querySelector('#optLarge'); c.checked = true; c.dispatchEvent(new Event('change', { bubbles: true })); }); }],
  ['large fixture: workflows', async p => { await p.evaluate(() => { const c = document.querySelector('#optLarge'); c.checked = true; c.dispatchEvent(new Event('change', { bubbles: true })); }); await go(p, 'workflows'); }],
  ['text scale 1.3: home', async p => { await p.evaluate(() => { const r = document.querySelector('#optScale'); r.value = '1.3'; r.dispatchEvent(new Event('input', { bubbles: true })); }); }],
  // Recipes and Agents keep a 1264 px floor below 1100 px of viewport (the mockups' own media rule), so at text scale 1.3 they scroll sideways by design and are not measured here.
  ['text scale 1.3: work', async p => { await p.evaluate(() => { const r = document.querySelector('#optScale'); r.value = '1.3'; r.dispatchEvent(new Event('input', { bubbles: true })); }); await go(p, 'work'); }],
  ['agents: ▾ menu open', async p => { await go(p, 'agents'); await p.click('[data-act="menu"][data-id="agm:ITM-01JE7"]'); }],
  ['home: palette open', async p => { await p.keyboard.press('Control+k'); }],
];

const measure = (table) => {
  const out = { scrollWidth: document.documentElement.scrollWidth, pillRight: -1, band: document.querySelectorAll('.attn-band').length, small: [], dashes: [], leftBorders: 0, waiting: /waiting on you/i.test(document.body.textContent), chips: 0, badChips: [] };
  const pill = document.querySelector('.health'); if (pill) out.pillRight = Math.round(pill.getBoundingClientRect().right);
  const roots = [document.querySelector('#app'), document.querySelector('#layer')].filter(Boolean);
  for (const root of roots) {
    const w = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
    while (w.nextNode()) {
      const t = w.currentNode, s = t.textContent; if (!s.trim()) continue;
      const el = t.parentElement, cs = getComputedStyle(el); if (cs.display === 'none' || cs.visibility === 'hidden' || el.closest('.sr')) continue;
      if (parseFloat(cs.fontSize) < 11) out.small.push(el.className + ':' + s.trim().slice(0, 20));
      if (s.includes('—') && s.trim() !== '—') out.dashes.push(s.trim().slice(0, 30));
    }
    for (const el of root.querySelectorAll('*')) {
      const cs = getComputedStyle(el); if (cs.display === 'none') continue;
      if (parseFloat(cs.borderLeftWidth) > 1 && cs.borderLeftStyle !== 'none') out.leftBorders++;
    }
    for (const c of root.querySelectorAll('.chip')) {
      if (getComputedStyle(c).display === 'none') continue;
      out.chips++;
      const word = c.textContent.trim().toUpperCase(), cls = ['attn', 'fail', 'ok'].find(k => c.classList.contains(k)) || '';
      if (!(word in table)) out.badChips.push(`${c.textContent.trim()} (not in the table)`);
      else if (table[word] !== cls) out.badChips.push(`${c.textContent.trim()} (class "${cls}", table says "${table[word]}")`);
    }
  }
  return out;
};

(async () => {
  const table = chipTable();
  const browser = await chromium.launch();
  let fails = 0, chips = 0, bad = 0; const rows = [];
  for (const [name, setup] of STATES) {
    const { page, errors } = await open(browser);
    await setup(page); await page.waitForTimeout(250);
    const r = await page.evaluate(measure, table);
    const probs = [];
    if (r.scrollWidth > 1280) probs.push(`scrollWidth ${r.scrollWidth}`);
    if (r.pillRight > 1264) probs.push(`pill right ${r.pillRight}`);
    if (r.band) probs.push('attn-band present');
    if (r.small.length) probs.push('font<11: ' + r.small.slice(0, 3).join(' | '));
    if (r.dashes.length) probs.push('em dash: ' + r.dashes.slice(0, 3).join(' | '));
    if (r.leftBorders) probs.push(`${r.leftBorders} left borders > 1px`);
    if (r.waiting) probs.push('"waiting on you" present');
    if (r.badChips.length) probs.push('chips: ' + r.badChips.slice(0, 3).join(' | '));
    if (errors.length) probs.push('page errors: ' + errors.join(' | '));
    chips += r.chips; bad += r.badChips.length; if (probs.length) fails++;
    rows.push({ name, ok: !probs.length, scrollWidth: r.scrollWidth, pillRight: r.pillRight, chips: r.chips, probs });
    console.log(`${probs.length ? 'FAIL' : 'PASS'} ${name}: scrollWidth=${r.scrollWidth} pillRight=${r.pillRight} chips=${r.chips}${probs.length ? '  ' + probs.join('; ') : ''}`);
    await page.close();
  }
  console.log(`proto-dom: ${STATES.length - fails}/${STATES.length} states clean; chip audit: ${chips} chips checked against fixture.md's ${Object.keys(table).length}-word table, ${bad} violations`);
  await browser.close(); process.exit(fails ? 1 : 0);
})();
