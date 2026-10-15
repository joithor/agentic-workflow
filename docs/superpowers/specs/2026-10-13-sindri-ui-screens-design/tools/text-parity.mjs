// Text parity: the visible text of each mockup against the same view in the prototype (a mockup draws one state; the
// prototype is driven to it first). Prints text lines only the mockup has (M) and only the prototype has (P).
//   cd skills/ui-evidence && node ../../docs/.../tools/text-parity.mjs [pages,comma]
import { chromium, PROTO, MOCK, open } from './lib.mjs';
const MAP = {
  'home': { hash: 'home' },
  'workflows': { hash: 'workflows' },
  'workflow-project': { hash: 'workflow/PRJ-sync' },
  'workflow-incident': { hash: 'workflow/INC-14' },
  'item-sheet': { hash: 'item/ITM-01JE7' },
  'recipes': { hash: 'recipes' },
  'recipe-inspector': { hash: 'recipe/workflow.item' },
  'agents': { hash: 'agents' },
  'context': { hash: 'context' },
  'context-filemap': { hash: 'context/files' },
  'system': { hash: 'system' },
};
const names = process.argv[2] ? process.argv[2].split(',') : Object.keys(MAP);
const norm = t => t.split('\n').map(l => l.replace(/\s+/g, ' ').trim()).filter(Boolean);
const text = () => document.body.innerText;
(async () => {
  const browser = await chromium.launch();
  for (const name of names) {
    const m = await browser.newPage({ viewport: { width: 1280, height: 900 } });
    await m.goto(MOCK + name + '.html', { waitUntil: 'load' });
    const mt = norm(await m.evaluate(text));
    const { page } = await open(browser, PROTO + '#' + MAP[name].hash);
    await page.waitForTimeout(200);
    if (name === 'context') await page.click('[data-act="accept-det"]');
    if (name === 'context-filemap') { await page.evaluate(() => { location.hash = '#context'; }); await page.waitForTimeout(100); await page.click('[data-act="accept-det"]'); await page.evaluate(() => { location.hash = '#context/files'; }); await page.waitForTimeout(100); }
    await page.waitForTimeout(150);
    const pt = norm(await page.evaluate(text));
    const ms = new Set(mt), ps = new Set(pt);
    console.log(`== ${name}`);
    for (const l of mt) if (!ps.has(l)) console.log('  M ' + l.slice(0, 200));
    for (const l of pt) if (!ms.has(l)) console.log('  P ' + l.slice(0, 200));
    await m.close(); await page.close();
  }
  await browser.close();
})();
