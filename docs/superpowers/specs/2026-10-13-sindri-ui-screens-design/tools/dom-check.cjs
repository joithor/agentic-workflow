// DOM checks for the rev-8 mockups at 1280 wide. Run from skills/ui-evidence (playwright is installed there):
//   node ../../docs/superpowers/specs/2026-10-13-sindri-ui-screens-design/tools/dom-check.cjs [names,comma] [--shots DIR]
// Serves nothing: expects http://127.0.0.1:8765/ to serve the mockups directory.
const { chromium } = require(require.resolve('playwright', { paths: [process.cwd(), __dirname] }));
const ALL = ['home','workflows','workflow-project','workflow-incident','item-sheet','recipes','recipe-inspector','agents','context','context-filemap','system'];
const args = process.argv.slice(2);
const names = args[0] && !args[0].startsWith('--') ? args[0].split(',') : ALL;
const shotsAt = args.indexOf('--shots'); const shotsDir = shotsAt >= 0 ? args[shotsAt + 1] : null;
const CHIPS = new Set(['PARKED','ESCALATED','DECISION','PROFILE','CHANGED','ORPHANED','DEGRADED','FAILED','RUNNING','OK','WITH YOU','QUEUED','DONE','DRAFT','APPROVED','DETERMINISTIC','MODEL','CHILD','POLLING','ABSENT']);
(async () => {
  const browser = await chromium.launch(); let fails = 0;
  for (const name of names) {
    const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
    const errors = []; page.on('pageerror', e => errors.push(String(e)));
    await page.goto(`http://127.0.0.1:8765/${name}.html`, { waitUntil: 'networkidle' });
    await page.evaluate(() => document.fonts && document.fonts.ready);
    const r = await page.evaluate((chips) => {
      const out = {};
      out.scrollWidth = document.documentElement.scrollWidth;
      const pill = document.querySelector('.health'); out.pillRight = pill ? Math.round(pill.getBoundingClientRect().right) : -1;
      out.band = document.querySelectorAll('.attn-band').length;
      out.fontsBelow11 = 0; out.leftBorders = 0; out.dashes = 0; out.badChips = []; out.lowerChips = 0;
      const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
      while (walker.nextNode()) { const t = walker.currentNode; const txt = t.textContent; if (txt.includes('—') && txt.trim() !== '—') out.dashes++; }
      for (const el of document.querySelectorAll('body *')) {
        const cs = getComputedStyle(el); if (cs.display === 'none' || cs.visibility === 'hidden') continue;
        if (el.childNodes.length && [...el.childNodes].some(n => n.nodeType === 3 && n.textContent.trim())) { if (parseFloat(cs.fontSize) < 11) out.fontsBelow11++; }
        if (parseFloat(cs.borderLeftWidth) > 1 && cs.borderLeftStyle !== 'none') out.leftBorders++;
      }
      for (const c of document.querySelectorAll('.chip')) { const w = c.textContent.trim(); if (!chips.includes(w.toUpperCase())) out.badChips.push(w); if (w !== w.toUpperCase() && !/^[A-Z][a-z]/.test(w)) out.lowerChips++; }
      out.waiting = /waiting on you/i.test(document.body.textContent);
      out.height = Math.ceil(Math.max(document.documentElement.scrollHeight, document.body.scrollHeight));
      return out;
    }, [...CHIPS]);
    const ok = r.scrollWidth <= 1280 && r.pillRight <= 1264 && r.band === 0 && r.fontsBelow11 === 0 && r.leftBorders === 0 && r.dashes === 0 && r.badChips.length === 0 && !r.waiting && errors.length === 0;
    if (!ok) fails++;
    console.log(`${ok ? 'PASS' : 'FAIL'} ${name}: scrollWidth=${r.scrollWidth} pillRight=${r.pillRight} band=${r.band} fonts<11=${r.fontsBelow11} leftBorders>1px=${r.leftBorders} dashes=${r.dashes} badChips=${JSON.stringify(r.badChips)} waiting=${r.waiting} errors=${errors.length} height=${r.height}`);
    if (shotsDir) { await page.setViewportSize({ width: 1280, height: Math.max(900, r.height) }); await page.screenshot({ path: `${shotsDir}/${name}.png`, fullPage: false }); }
    await page.close();
  }
  await browser.close(); process.exit(fails ? 1 : 0);
})();
