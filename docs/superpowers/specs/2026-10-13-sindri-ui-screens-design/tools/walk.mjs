// The main click-through of the rev-8 prototype: the flows the owner named, end to end, against a fresh page.
//   cd skills/ui-evidence && node ../../docs/superpowers/specs/2026-10-13-sindri-ui-screens-design/tools/walk.mjs
// Serve the spec dir first: cd <spec dir> && python3 -m http.server 8766 --bind 127.0.0.1
import { chromium, open, runner, assert, eq, txt, count, title, hash, focused, panel, reset, setConn, go, live, fixtureHealthSentence } from './lib.mjs';

(async () => {
  const browser = await chromium.launch();
  const { page, errors } = await open(browser);
  const { step, done } = runner('walk');
  const key = k => page.keyboard.press(k);
  const click = sel => page.click(sel);

  await step('boot: no .attn-band; the window title reads "(5) Needs you · Sindri"', async () => {
    eq(await count(page, '.attn-band'), 0, 'attention band elements');
    eq(await title(page), '(5) Needs you · Sindri', 'window title');
    eq(await count(page, '[data-row^="ny:"]'), 5, 'needs-you rows');
  });
  await step('Home health sentence equals the fixture sentence', async () => {
    eq(await txt(page, '.hsent'), fixtureHealthSentence(), 'health sentence');
  });
  await step('Home needs-you ids at rest (.id.at-rest) and the Attach note under the Running group', async () => {
    eq(await count(page, '[data-row^="ny:"] .id.at-rest'), 4, 'at-rest ids on the four item/decision/service rows');
    eq(await txt(page, '.v-home .attach-note'), 'Attach joins the live session; your first keystroke takes it over.', 'attach note');
  });

  await step('park row ↵ opens the item sheet', async () => {
    await page.focus('[data-row="ny:ITM-01JC8"]'); await key('Enter'); await page.waitForSelector('.sheet');
    assert((await page.getAttribute('.sheet', 'aria-label')).startsWith('Item ITM-01JC8'), 'sheet is ITM-01JC8');
    eq(await hash(page), '#item/ITM-01JC8', 'hash');
  });
  await step('Grant rounds reveals the command (nothing else changes)', async () => {
    await click('.sheet [data-act="lock"]'); await page.waitForSelector('.sheet .cmd');
    eq(await txt(page, '.sheet .cmd code'), 'sindri workflow answer ITM-01JC8 --grant 1', 'revealed command');
    assert(/Waits for the terminal/.test(await txt(page, '.sheet .px-note')), 'waits-for-terminal note');
  });
  await step('Esc closes the sheet and returns focus to the parked row', async () => {
    await page.focus('.sheet .btn.sheetback, .sheet .icon-btn'); await key('Escape'); await page.waitForTimeout(80);
    eq(await count(page, '.sheet'), 0, 'sheet closed'); eq((await focused(page)).row, 'ny:ITM-01JC8', 'focused row');
  });

  await step('G W goes to Workflows (12 open · 2 need you · 1 with you)', async () => {
    await key('g'); await key('w'); await page.waitForSelector('.v-workflows');
    eq(await txt(page, '.pagehead .sub'), '12 open · 2 need you · 1 with you', 'page head');
    eq(await hash(page), '#workflows');
  });
  await step('PRJ-sync frame: states, 9 children, Timeline "… 39 more"', async () => {
    await click('[data-row="wf:PRJ-sync"]'); await page.waitForSelector('.sheet');
    assert(/Workflow instance PRJ-sync/.test(await page.getAttribute('.sheet', 'aria-label')), 'PRJ-sync sheet');
    eq(await count(page, '.sheet .child'), 3, 'drawn children (of 9)');
    assert(/9 ·/.test(await txt(page, '.sheet #ch-h + .meta')), 'children meta says 9');
    eq(await txt(page, '.sheet .ev.more a'), '… 39 more', 'timeline more row');
    eq(await count(page, '.sheet .ev:not(.more)'), 2, 'two events drawn');
  });
  await step('child → item sheet (ITM-01JD6) with a back button to the project', async () => {
    await click('.sheet [data-act="open-sheet"][data-id="ITM-01JD6"]'); await page.waitForTimeout(100);
    assert((await page.getAttribute('.sheet', 'aria-label')).startsWith('Item ITM-01JD6'), 'item sheet');
    assert(/PRJ-sync/.test(await txt(page, '.sheet .sheetback.show')), 'back names PRJ-sync');
  });
  await step('tool-step Take over → With you, then Hand back → Running', async () => {
    await click('.sheet [data-act="takeover"]'); await page.waitForTimeout(80);
    assert(/taken over/.test(await txt(page, '.sheet .decl')), 'the state line says taken over');
    eq(await count(page, '.sheet [data-act="handback"]'), 1, 'hand back offered');
    await click('.sheet [data-act="handback"]'); await page.waitForTimeout(80);
    eq(await count(page, '.sheet [data-act="takeover"]'), 1, 'take over offered again');
    await key('Escape'); await key('Escape');
  });
  await step('JE7 Attach → "Attached in Warp" and the Attach note sits under the control row', async () => {
    await go(page, 'item/ITM-01JE7'); await page.waitForSelector('.sheet');
    eq(await txt(page, '.sheet .ctl .attach-note'), 'Attach joins the live session; your first keystroke takes it over.', 'attach note');
    await click('.sheet [data-act="attach"]'); await page.waitForTimeout(80);
    eq(await txt(page, '.sheet .px-att'), '● Attached in Warp', 'attached line');
    eq(await txt(page, '.sheet .ctl .attach-note') !== null, true, 'note stays');
  });
  await step('G R → inspector: workflow.item shows 30% human rate, 68% on the page', async () => {
    await key('Escape'); await page.focus('body'); await key('g'); await key('r'); await page.waitForSelector('.v-recipes');
    assert(/68%/.test(await txt(page, '.pagehead .sub')), 'page total 68%');
    await click('a[data-id="workflow.item"]'); await page.waitForSelector('.sheet');
    assert(/30%/.test(await txt(page, '.sheet .kvl')), 'human rate 30%');
    assert(/34 ÷ 112 runs/.test(await txt(page, '.sheet .kvl')), 'formula text');
    eq(await txt(page, '.sheet .win span'), '112 runs · 97 done · 6 failed · 9 cancelled', 'window line');
    await key('Escape');
  });
  await step('G C → graph; the Files lens toggled to tree shows the file map', async () => {
    await page.focus('body'); await key('g'); await key('c'); await page.waitForSelector('.v-context .stack');
    await click('[data-act="fform"][data-x="tree"]'); await page.waitForSelector('#fm-h');
    eq(await hash(page), '#context/files');
    await click('[data-act="fform"][data-x="graph"]'); await page.waitForSelector('.v-context .stack');
  });
  await step('Accept the suggested link: 46 → 45 pending', async () => {
    assert(/^46 pending/.test(await txt(page, '#mr-h + .meta')), 'starts at 46');
    await click('.mr[data-row="mr:p1"] [data-act="mr-do"][data-x="accept"]'); await page.waitForTimeout(80);
    assert(/^45 pending/.test(await txt(page, '#mr-h + .meta')), 'now 45');
  });
  await step('Accept all deterministic → toast → Undo restores the 31', async () => {
    await click('[data-act="accept-det"]'); await page.waitForSelector('.toast');
    assert(/^14 pending/.test(await txt(page, '#mr-h + .meta')), '14 pending after');
    assert(/31 accepted/.test(await txt(page, '.pagehead .sub')), 'header says 31 accepted');
    assert(/0 proposals · 31 accepted 12:26/.test(await txt(page, '.mr .keyt')), 'row text');
    await click('.toast [data-act="undo-det"]'); await page.waitForTimeout(80);
    eq(await count(page, '.toast'), 0, 'toast gone');
    assert(/^45 pending/.test(await txt(page, '#mr-h + .meta')), 'back to 45');
  });

  await step('Agents ▾ → Cancel… → Keep leaves 4 live; Cancel item leaves 3 live', async () => {
    await page.focus('body'); await key('g'); await key('a'); await page.waitForSelector('.v-agents');
    assert(/^4 live/.test(await txt(page, '.pagehead .sub')), '4 live to start');
    await click('[data-act="menu"][data-id="agm:ITM-01JE9"]'); await page.waitForSelector('.menu');
    await click('.menu [data-act="ask-cancel"]'); await page.waitForSelector('.confirm');
    assert(/Cancel ITM-01JE9\? Its checks stop\./.test(await txt(page, '.confirm')), 'confirm text');
    eq((await focused(page)).text, 'Keep', 'Keep holds the focus');
    await click('.confirm [data-act="confirm-no"]'); await page.waitForTimeout(80);
    eq(await count(page, '.confirm'), 0, 'confirm gone'); assert(/^4 live/.test(await txt(page, '.pagehead .sub')), 'unchanged: 4 live');
    await click('[data-act="menu"][data-id="agm:ITM-01JE9"]'); await click('.menu [data-act="ask-cancel"]');
    await click('.confirm [data-act="confirm-yes"]'); await page.waitForTimeout(100);
    assert(/^3 live/.test(await txt(page, '.pagehead .sub')), '3 live after Cancel item');
  });
  await step('Agents: the Needs you band says 2 (parked + escalated) and carries the Attach note', async () => {
    eq(await txt(page, '.band.attn .sh h2'), 'Needs you', 'heading'); eq(await txt(page, '.band.attn .count-attn'), '2', 'count');
    eq(await txt(page, '.v-agents .attach-note'), 'Attach joins the live session; your first keystroke takes it over.');
    assert(!/waiting on you/i.test(await page.evaluate(() => document.body.textContent)), 'no "waiting on you" anywhere');
  });

  await step('System: Run again disabled with the pending chip; focus on Sync now; one health sentence', async () => {
    await page.focus('body'); await key('g'); await key('.'); await page.waitForSelector('.v-system');
    eq(await page.isDisabled('.v-system .imp .acts button:has-text("Run again")'), true, 'Run again disabled');
    eq(await txt(page, '.v-system .imp .pending'), 'Available with API P6', 'pending chip');
    eq((await focused(page)).act, 'sync-onyx', 'focus on Sync now');
    const s = await txt(page, '.pagesum'); assert(s.startsWith(fixtureHealthSentence()), 'pagesum starts with the fixture sentence: ' + s);
    assert(s.endsWith('12 instances open · 3 activities running.'), 'pagesum ends with counts (ITM-01JE9 was cancelled above): ' + s);
  });

  await step('⌘K "needs" goes to Needs you on Home', async () => {
    await key('Control+k'); await page.waitForSelector('#palIn'); await page.keyboard.type('needs');
    assert(/Needs you\s*home · G N/.test(await txt(page, '.px-palist [aria-selected="true"], .px-palist .sel')), 'top result is Needs you');
    await key('Enter'); await page.waitForTimeout(100);
    eq(await count(page, '.v-home'), 1, 'home'); eq((await focused(page)).row.startsWith('ny:'), true, 'focus on a needs-you row');
  });
  await step('palette "Run index job (dev)" streams the job, then the index is ok', async () => {
    await key('Control+k'); await page.keyboard.type('Run index'); await key('Enter');
    await page.waitForSelector('.px-job'); await page.waitForTimeout(300);
    assert((await count(page, '.px-job div')) >= 2, 'lines streaming');
    await page.waitForFunction(() => !document.querySelector('[data-row="ny:svc:index"]'), null, { timeout: 15000 });
    assert(/4 need you/.test(await txt(page, '.pagehead .sub')), 'needs drop to 4');
  });
  await step('? opens the shortcuts dialog; Esc closes it', async () => {
    await page.focus('body'); await key('?'); await page.waitForSelector('.px-dlg');
    assert(/Shortcuts · Home/.test(await txt(page, '.px-dlg h2')), 'title'); await key('Escape'); eq(await count(page, '.px-dlg'), 0, 'closed');
  });
  await step('Stalled → banner → Rebuild views → back to normal', async () => {
    await setConn(page, 'stalled'); await page.waitForSelector('.px-banner.err');
    assert(/✕\s*Stalled/.test(await txt(page, '.health .word')), 'pill word');
    await click('[data-act="rebuild"]'); await page.waitForSelector('.px-banner .px-prog');
    await page.waitForFunction(() => !document.querySelector('.px-banner'), null, { timeout: 8000 });
    assert(/◐\s*Degraded/.test(await txt(page, '.health .word')), 'pill back');
  });
  await step('Reduced motion toggles html.rm', async () => {
    await panel(page, '#optRm'); eq(await page.evaluate(() => document.documentElement.classList.contains('rm')), true, 'rm on');
    await panel(page, '#optRm'); eq(await page.evaluate(() => document.documentElement.classList.contains('rm')), false, 'rm off');
  });
  await step('Large fixture: 12 need you, the page caps at 5 rows ("all 12 →")', async () => {
    await page.evaluate(() => { const c = document.querySelector('#optLarge'); c.checked = true; c.dispatchEvent(new Event('change', { bubbles: true })); }); await page.waitForTimeout(150);
    assert(/^12 need you/.test(await txt(page, '.pagehead .sub')), '12 need you');
    eq(await title(page), '(12) Needs you · Sindri', 'title follows');
    assert(/all 12/.test(await txt(page, '.band.attn .shr, #ny-h ~ .shr') || ''), 'all 12 link');
  });
  await step('Reset returns to the 5-need fixture on Home', async () => {
    await reset(page); eq(await title(page), '(5) Needs you · Sindri'); eq(await hash(page), '#home'); eq(await count(page, '.attn-band'), 0);
  });

  done(errors); await browser.close();
})();
