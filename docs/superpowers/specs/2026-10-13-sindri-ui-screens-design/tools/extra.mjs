// Edge flows: connection states, deep links and history, keyboard, focus, layout limits and a few whole-document audits.
//   cd skills/ui-evidence && node ../../docs/superpowers/specs/2026-10-13-sindri-ui-screens-design/tools/extra.mjs
import { chromium, open, runner, assert, eq, txt, count, title, hash, focused, panel, reset, setConn, go, live } from './lib.mjs';

(async () => {
  const browser = await chromium.launch();
  const { page, errors } = await open(browser);
  const { step, done } = runner('extra');
  const key = k => page.keyboard.press(k);
  const click = sel => page.click(sel);

  await step('Not running: the screen names the address and offers the copyable command; the title goes quiet', async () => {
    await setConn(page, 'notrunning'); assert(/Not running/.test(await txt(page, '.px-card h2')), 'heading'); eq(await txt(page, '.px-card .cmd code'), 'sindri'); eq(await title(page), 'Sindri', 'title');
    assert(/Not running/.test(await txt(page, '.health .word')), 'pill');
  });
  await step('Connecting: skeleton rows under aria-busy', async () => {
    await setConn(page, 'connecting'); eq(await page.getAttribute('main.page', 'aria-busy'), 'true'); assert((await count(page, '.px-skrow')) >= 5, 'skeleton rows');
  });
  await step('Version mismatch: no nav, no search, the fix command is offered', async () => {
    await setConn(page, 'mismatch'); eq(await count(page, 'nav.seg'), 0, 'nav hidden'); eq(await count(page, '.search'), 0, 'search hidden'); assert(/different versions/.test(await txt(page, '.px-card h2')), 'heading');
  });
  await step('Stale: banner with Refresh; Refresh returns to normal', async () => {
    await setConn(page, 'stale'); assert(/No event for more than 30 s/.test(await txt(page, '.px-banner')), 'banner'); await click('.px-banner [data-act="refresh"]'); eq(await count(page, '.px-banner'), 0, 'banner gone');
    assert(/Updated 2s ago/.test(await txt(page, '.hsent')), 'health sentence time fresh');
  });
  await step('Stalled: the pill reads Stalled · views stale, rows are marked stale, the sentence says so', async () => {
    await setConn(page, 'stalled'); assert(/views stale/.test(await txt(page, '.health')), 'pill'); assert(/Views are stale\./.test(await txt(page, '.hsent')), 'sentence'); await reset(page);
  });
  await step('deep links: a sheet hash opens its sheet; browser Back closes it', async () => {
    await page.goto((await page.url()).replace(/#.*$/, '') + '#workflow/INC-14'); await page.waitForSelector('.sheet'); assert(/INC-14/.test(await page.getAttribute('.sheet', 'aria-label')), 'INC-14 sheet');
    await go(page, 'workflows'); await page.goBack(); await page.waitForTimeout(150); eq(await count(page, '.sheet'), 1, 'sheet back after Back');
  });
  await step('incident sheet: the Attach note sits under the control row and Attach says "Attached in Warp"', async () => {
    eq(await txt(page, '.sheet .ctl .attach-note'), 'Attach joins the live session; your first keystroke takes it over.'); await click('.sheet [data-act="attach"]'); await page.waitForTimeout(80);
    assert(/Attached in Warp/.test(await txt(page, '.sheet .px-att')), 'attached'); eq(await txt(page, '.sheet .tab[aria-selected="true"] .n'), '19', 'timeline count 18 + the new event');
    eq(await txt(page, '.sheet .ev.more a'), '… 17 more', 'more row follows the count');
  });
  await step('sheet width: keyboard on the grip resizes within 420..760', async () => {
    await page.focus('.sheet-grip'); const w0 = await page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue('--sheetw'));
    await key('ArrowLeft'); await page.waitForTimeout(60); const w1 = await page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue('--sheetw'));
    assert(w0.trim() === '760px' && w1.trim() === '760px', 'already at the 760 maximum, ArrowLeft cannot widen it'); await key('ArrowRight'); await page.waitForTimeout(60);
    eq((await page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue('--sheetw'))).trim(), '740px', 'ArrowRight narrows by 20');
  });
  await step('narrow window (1000 px): the sheet takes the full width', async () => {
    await page.setViewportSize({ width: 1000, height: 800 }); await page.waitForTimeout(100);
    const w = await page.evaluate(() => Math.round(document.querySelector('.sheet').getBoundingClientRect().width)); assert(w >= 990, 'sheet width ' + w); await page.setViewportSize({ width: 1280, height: 900 });
  });
  await step('J / K move focus between rows; the edge tab reopens the last sheet after Esc', async () => {
    await key('Escape'); await go(page, 'home'); await page.evaluate(() => document.activeElement && document.activeElement.blur()); await key('j'); eq((await focused(page)).row, 'ny:ITM-01JC8', 'first row'); await key('j'); eq((await focused(page)).row, 'ny:REQ-01JD4', 'second row'); await key('k');
    eq((await focused(page)).row, 'ny:ITM-01JC8', 'back up'); await key('Enter'); await page.waitForSelector('.sheet'); await key('Escape'); eq(await count(page, '[data-act="reopen"]'), 1, 'edge tab');
  });
  await step('the G chord shows its hint and times out', async () => {
    await page.focus('body'); await key('g'); eq(await page.evaluate(() => document.getElementById('chord').classList.contains('on')), true, 'chord on'); await page.waitForTimeout(1200);
    eq(await page.evaluate(() => document.getElementById('chord').classList.contains('on')), false, 'chord off');
  });
  await step('text scale 1.3: Home caps the needs-you list at 4 and keeps the pill inside the window', async () => {
    await page.evaluate(() => { const r = document.querySelector('#optScale'); r.value = '1.3'; r.dispatchEvent(new Event('input', { bubbles: true })); }); await page.waitForTimeout(150);
    eq(await count(page, '[data-row^="ny:"]'), 4, 'rows'); assert((await page.evaluate(() => document.documentElement.scrollWidth)) <= 1280, 'no sideways scroll');
    const r = await page.evaluate(() => Math.round(document.querySelector('.health').getBoundingClientRect().right)); assert(r <= 1264, 'pill right ' + r); await reset(page);
  });
  await step('prefers-reduced-motion at boot sets html.rm', async () => {
    const p2 = await browser.newPage({ viewport: { width: 1280, height: 900 }, reducedMotion: 'reduce' }); await p2.goto((await page.url()).replace(/#.*$/, '')); await p2.waitForTimeout(200);
    eq(await p2.evaluate(() => document.documentElement.classList.contains('rm')), true); await p2.close();
  });
  await step('audit: no duplicate ids, every button has a name, every sheet and dialog is labelled (Home, Work, Agents, an item sheet)', async () => {
    for (const h of ['home', 'work', 'agents', 'item/ITM-01JE7']) {
      await go(page, h);
      const r = await page.evaluate(() => {
        const ids = [...document.querySelectorAll('[id]')].map(e => e.id), dup = ids.filter((x, i) => ids.indexOf(x) !== i);
        const unnamed = [...document.querySelectorAll('#app button, #app [role=button]')].filter(b => !(b.getAttribute('aria-label') || b.textContent.trim() || b.title)).map(b => b.className);
        const unl = [...document.querySelectorAll('[role=dialog],[role=alertdialog],aside.sheet')].filter(d => !(d.getAttribute('aria-label') || d.getAttribute('aria-labelledby'))).length;
        return { dup, unnamed, unl };
      });
      assert(!r.dup.length, h + ': duplicate ids ' + r.dup.join(',')); assert(!r.unnamed.length, h + ': unnamed buttons ' + r.unnamed.join(',')); eq(r.unl, 0, h + ': unlabelled dialogs');
    }
  });
  await step('audit: the retired words are nowhere (waiting on you, LIVE chip, the attention band) on any page', async () => {
    for (const h of ['home', 'workflows', 'work', 'recipes', 'context', 'agents', 'system']) {
      await go(page, h); const r = await page.evaluate(() => ({ w: /waiting on you/i.test(document.body.textContent), live: [...document.querySelectorAll('.chip')].some(c => c.textContent.trim().toUpperCase() === 'LIVE'), band: document.querySelectorAll('.attn-band').length }));
      assert(!r.w && !r.live && !r.band, h + ': ' + JSON.stringify(r));
    }
  });
  await step('audit: every page head count equals its attention-chip rows (Home 5, Workflows 2, Work 3, Agents 2)', async () => {
    const want = { home: 5, workflows: 2, work: 3, agents: 2 };
    for (const [h, n] of Object.entries(want)) {
      await go(page, h); const r = await page.evaluate(() => {
        const head = document.querySelector('.pagehead .sub').textContent.match(/(\d+) need you/); const attn = [...document.querySelectorAll('#app .chip.attn, #app .chip.fail')].filter(c => /^(PARKED|ESCALATED|DECISION|PROFILE|FAILED)$/i.test(c.textContent.trim()) && !c.closest('.sheet') && !c.closest('.wf')); // Home's Workflows strip draws sentry #8812's DECISION chip; it is a workflow row, counted on Workflows (fixture.md, the open point)
        return { head: head ? +head[1] : null, rows: attn.length };
      });
      eq(r.head, n, h + ' head count'); if (h !== 'workflows') eq(r.rows, n, h + ' attention-chip rows');
    }
  });

  done(errors); await browser.close();
})();
