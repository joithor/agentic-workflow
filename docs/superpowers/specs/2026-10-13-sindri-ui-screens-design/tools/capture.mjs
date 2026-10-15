// Re-captures the ten prototype-*.png next to prototype.html: 1280 wide, the controls panel hidden, each at its page height.
//   cd skills/ui-evidence && node ../../docs/superpowers/specs/2026-10-13-sindri-ui-screens-design/tools/capture.mjs [names,comma]
import path from 'node:path';
import { chromium, open, specDir, setConn, go } from './lib.mjs';

const SHOTS = {
  home: async () => {},
  workflows: async p => go(p, 'workflows'),
  'workflow-project': async p => go(p, 'workflow/PRJ-sync'),
  'item-sheet': async p => go(p, 'item/ITM-01JE7'),
  'recipe-inspector': async p => go(p, 'recipe/workflow.item'),
  agents: async p => go(p, 'agents'),
  context: async p => go(p, 'context'),
  system: async p => go(p, 'system'),
  palette: async p => { await p.keyboard.press('Control+k'); },
  stalled: async p => setConn(p, 'stalled'),
};
const names = process.argv[2] ? process.argv[2].split(',') : Object.keys(SHOTS);
(async () => {
  const browser = await chromium.launch();
  for (const name of names) {
    const { page, errors } = await open(browser);
    await page.addStyleTag({ content: '#proto{display:none !important}' });
    await SHOTS[name](page); await page.waitForTimeout(400);
    const h = await page.evaluate(() => Math.ceil(Math.max(document.documentElement.scrollHeight, document.body.scrollHeight)));
    await page.setViewportSize({ width: 1280, height: Math.max(900, h) }); await page.waitForTimeout(250);
    const file = path.join(specDir, `prototype-${name}.png`);
    await page.screenshot({ path: file, fullPage: false });
    console.log(`${name}: 1280 x ${Math.max(900, h)}${errors.length ? '  page errors: ' + errors.join(' | ') : ''}`);
    await page.close();
  }
  await browser.close();
})();
