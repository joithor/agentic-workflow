// Checks the CSS merge: for each mockup, every element's computed style in the mockup itself must equal the same element
// inside <div class="v-PAGE"> styled only by the merged CSS (build-prototype.py --css-only). Catches a rule that lost
// its scope, a reordered cascade, a dropped rule.
//   python3 ../../docs/.../tools/build-prototype.py --css-only /tmp/merged.css
//   cd skills/ui-evidence && node ../../docs/.../tools/css-equiv.mjs /tmp/merged.css [pages,comma]
import fs from 'node:fs';
import { chromium, MOCK } from './lib.mjs';
const ALL = ['home','workflows','workflow-project','workflow-incident','item-sheet','recipes','recipe-inspector','context','context-filemap','agents','system'];
const cssPath = process.argv[2]; if (!cssPath) { console.error('usage: css-equiv.mjs merged.css [pages]'); process.exit(2); }
const css = fs.readFileSync(cssPath, 'utf8');
const names = process.argv[3] ? process.argv[3].split(',') : ALL;
(async () => {
  const browser = await chromium.launch(); let bad = 0;
  for (const name of names) {
    const a = await browser.newPage({ viewport: { width: 1280, height: 900 } });
    await a.goto(MOCK + name + '.html', { waitUntil: 'load' });
    const body = await a.evaluate(() => document.body.innerHTML);
    const collect = () => {
      const root = document.querySelector('.v-root') || document.body;
      const out = [];
      for (const el of root.querySelectorAll('*')) {
        if (el.tagName === 'SCRIPT') continue;
        const cs = getComputedStyle(el); const o = {};
        for (const p of cs) if (!/^(transition|animation|-webkit-|inline-size|block-size|perspective-origin|transform-origin)/.test(p)) o[p] = cs.getPropertyValue(p);
        out.push([el.tagName + '.' + (el.getAttribute('class') || ''), o]);
      }
      return out;
    };
    await a.evaluate(() => document.fonts.ready);
    const A = await a.evaluate(collect);
    const b = await browser.newPage({ viewport: { width: 1280, height: 900 } });
    await b.setContent(`<!doctype html><html lang="en"><head><meta charset="utf-8"><link rel="preconnect" href="https://fonts.googleapis.com"><link rel="preconnect" href="https://fonts.gstatic.com" crossorigin><link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600&family=JetBrains+Mono:wght@400;500&display=swap" rel="stylesheet"><style>${css}</style></head><body><div class="v-root v-${name}">${body}</div></body></html>`, { waitUntil: 'load' });
    await b.evaluate(() => document.fonts.ready); await a.evaluate(() => document.fonts.ready);
    const B = await b.evaluate(collect);
    let diffs = 0; const sample = [];
    if (A.length !== B.length) { diffs++; sample.push(`element count ${A.length} vs ${B.length}`); }
    for (let i = 0; i < Math.min(A.length, B.length); i++) {
      for (const k of Object.keys(A[i][1])) if (A[i][1][k] !== B[i][1][k]) { diffs++; if (sample.length < 6) sample.push(`${A[i][0]} ${k}: mockup ${A[i][1][k]} | merged ${B[i][1][k]}`); }
    }
    if (diffs) bad++;
    console.log(`${diffs ? 'FAIL' : 'PASS'} ${name}: ${A.length} elements, ${diffs} property differences`);
    for (const s of sample) console.log('     ' + s);
    await a.close(); await b.close();
  }
  await browser.close(); process.exit(bad ? 1 : 0);
})();
