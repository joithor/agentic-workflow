// More flows: decisions, escalations, answers, seen/undo, desk answers, popovers, Work, Recipes, the inspector, System, Context.
//   cd skills/ui-evidence && node ../../docs/superpowers/specs/2026-10-13-sindri-ui-screens-design/tools/more.mjs
import { chromium, open, runner, assert, eq, txt, count, title, hash, focused, panel, reset, go, live } from './lib.mjs';

(async () => {
  const browser = await chromium.launch();
  const { page, errors } = await open(browser);
  const { step, done } = runner('more');
  const key = k => page.keyboard.press(k);
  const click = sel => page.click(sel);
  const body = () => page.evaluate(() => document.body.textContent);
  const subHead = () => txt(page, '.pagehead .sub');

  await step('Mark all seen (⇧A) clears the title count; Undo restores it', async () => {
    await page.focus('body'); await key('Shift+A'); await page.waitForTimeout(80);
    eq(await title(page), 'Sindri', 'title quiet when everything is seen');
    await click('[data-act="undo-seen"]'); eq(await title(page), '(5) Needs you · Sindri', 'title back');
  });
  await step('decision: Accept records now (live line says so), the row leaves, 4 need you', async () => {
    await click('[data-row="ny:REQ-01JD4"] [data-act="decide"][data-x="accept"]'); await page.waitForTimeout(100);
    assert(/Recorded now; the engine acts on it when the workflow runs\./.test(await live(page)), 'announcement');
    eq(await count(page, '[data-row="ny:REQ-01JD4"]'), 0, 'row gone'); eq(await title(page), '(4) Needs you · Sindri'); assert(/^4 need you/.test(await subHead()), 'head says 4');
  });
  await step('escalated: Accept · terminal reveals the command; Reject (menu) re-runs produce', async () => {
    await click('[data-row="ny:ITM-01JB4"] [data-act="lock"]'); eq(await txt(page, '[data-row="ny:ITM-01JB4"] .cmd code'), 'sindri workflow answer ITM-01JB4 --accept');
    await click('[data-row="ny:ITM-01JB4"] [data-act="menu"]'); await click('.menu [data-act="reject"]'); await page.waitForTimeout(100);
    eq(await count(page, '[data-row="ny:ITM-01JB4"]'), 0, 'row gone'); assert(/^3 need you/.test(await subHead()), '3 need you');
  });
  await step('a row menu closes on Esc and focus returns to its ▾', async () => {
    await click('[data-row="ny:ITM-01JC8"] [data-act="menu"]'); await page.waitForSelector('.menu');
    await key('Escape'); eq(await count(page, '.menu'), 0, 'menu closed'); const f = await focused(page); eq(f.act, 'menu', 'focus back on the menu button');
  });
  await step('parked needs-answer path: write an answer, ⌘↵ sends it, the item runs again', async () => {
    await go(page, 'item/ITM-01JC8'); await page.waitForSelector('.sheet #ans');
    await page.fill('#ans', 'Retry with jitter'); await key('Control+Enter'); await page.waitForTimeout(100);
    assert(/answered by you: Retry with jitter/.test(await txt(page, '.sheet .tabpanel')), 'timeline carries the answer');
    await key('Escape'); await go(page, 'home'); assert(/^2 need you/.test(await subHead()), 'only the service and the profile are left');
  });
  await step('Workflows: sentry #8812 desk answer routes it (2 → 1 need you)', async () => {
    await go(page, 'workflows'); await click('[data-row="wf:sentry #8812"] [data-act="wf-answer"]'); await page.waitForSelector('.sheet [data-act="wf-sev"]');
    await click('.sheet [data-act="wf-sev"][data-x="sev2"]'); await page.waitForTimeout(80); await key('Escape');
    assert(/1 need you/.test(await subHead()), 'head 1 need you'); assert(/routed as sev2/.test(await txt(page, '[data-row="wf:sentry #8812"]')), 'row now says routed');
  });
  await step('Workflows: PRJ-exports "Complete step" opens its build and the with-you count drops to 0', async () => {
    await click('[data-row="wf:PRJ-exports"]'); await page.waitForSelector('.sheet #imp'); await click('.sheet [data-act="wf-complete"]'); await page.waitForTimeout(80); await key('Escape');
    assert(/0 with you/.test(await subHead()), '0 with you'); assert(/build/.test(await txt(page, '[data-row="wf:PRJ-exports"]')), 'row in build');
  });
  await step('Start… popover: choose triage.alert, start it, 13 open', async () => {
    await click('[data-act="start"]'); await page.waitForSelector('.px-pop'); await click('.px-rec[data-x="triage.alert"]'); await page.fill('#startin', 'sentry #9000'); await click('[data-act="start-go"]');
    await page.waitForTimeout(80); assert(/^13 open/.test(await subHead()), '13 open');
  });
  await step('Work: Needs you is one group of 3 (parked, decision, escalated order is the mockup\'s)', async () => {
    await page.evaluate(() => document.getElementById('optReset').click()); await go(page, 'work');
    eq(await txt(page, '.band.attn .sh h2'), 'Needs you'); eq(await txt(page, '.band.attn .sh .meta'), '3');
    eq(await page.evaluate(() => [...document.querySelectorAll('.band.attn .tr .chip')].map(c => c.textContent).join(',')), 'Parked,Decision,Escalated');
    eq(await subHead(), '12 items · 3 need you · 1 with you · 4 running · 4 queued', 'summary line');
    assert(!/waiting on you/i.test(await body()), 'the retired phrase is nowhere');
  });
  await step('Work: "/" filters, Create item adds a queued row', async () => {
    await page.focus('body'); await key('/'); await page.keyboard.type('tray'); await page.waitForTimeout(80);
    eq(await count(page, '.qt .tr'), 1, 'one row matches'); await page.fill('#wkfilter', '');
    await click('[data-act="create"]'); await page.fill('#newtitle', 'Spark label audit'); await click('[data-act="create-go"]'); await page.waitForTimeout(80);
    assert(/13 items/.test(await subHead()), '13 items');
  });
  await step('Recipes: Failed, Done without you %, the weekly spark; 68% total; "132 of 193" in the footnote', async () => {
    await go(page, 'recipes');
    const row = await page.evaluate(() => [...document.querySelector('[data-row="rc:workflow.item"]').querySelectorAll('[role=cell]')].map(c => c.textContent.trim()).join('|'));
    eq(row, 'Approved|workflow.item|112|6|72%|1h 40m|310k||10-02v7|', 'workflow.item row');
    eq(await page.getAttribute('[data-row="rc:workflow.item"] .spark', 'aria-label'), 'runs per week, four weeks, rising: 22 27 30 33', 'spark label');
    eq(await page.evaluate(() => [...document.querySelectorAll('[data-row="rc:workflow.item"] .spark i')].map(i => i.style.height).join(',')), '67%,82%,91%,100%', 'spark heights');
    assert(/132 of 193/.test(await txt(page, '.footnote')), 'footnote'); assert(/<b>68%<\/b> done without you/.test(await page.innerHTML('.pagehead .sub')), '68%');
    eq(await page.evaluate(() => [...document.querySelectorAll('.rrow .row-title a')].map(a => a.textContent).join(',')), 'workflow.project,workflow.item,workflow.step,builder.feature,triage.alert,incident.response,scoping.project,intake.linear');
  });
  await step('Recipes: window 7 days scales the counts, the percentages hold', async () => {
    await click('[data-act="win"]'); await click('[data-act="win-set"][data-x="7 days"]');
    const n = +(await txt(page, '[data-row="rc:workflow.item"] .num.n')); assert(n > 0 && n < 112, 'scaled runs ' + n);
    assert(/68%/.test(await subHead()), 'page total still 68%'); await click('[data-act="win"]'); await click('[data-act="win-set"][data-x="30 days"]');
  });
  await step('Recipes: Validate file… rejects a non-yaml file and accepts a yaml one', async () => {
    await click('[data-act="validate"]'); await page.fill('#valin', 'notes.txt'); await click('[data-act="val-go"]'); assert(/SND-RECIPE-001/.test(await txt(page, '.px-res.bad')), 'invalid message');
    await page.fill('#valin', 'recipes/x.yaml'); await click('[data-act="val-go"]'); assert(/5 states/.test(await txt(page, '.px-res.ok')), 'valid message'); await click('[data-act="pop-close"]');
  });
  await step('Recipes: scoping.project Re-approve · terminal shows the command and waits', async () => {
    await click('[data-row="rc:scoping.project"] [data-act="lock"]'); eq(await txt(page, '[data-row="rc:scoping.project"] .cmd code'), 'sindri recipe approve scoping.project');
  });
  await step('inspector: Run evals counts to 12 of 12; Versions v6 scopes the numbers', async () => {
    await click('a[data-id="workflow.item"]'); await page.waitForSelector('.sheet'); await click('.sheet [data-act="evals"]');
    await page.waitForFunction(() => /12 of 12 pass/.test(document.querySelector('.sheet .ev .sh').textContent), null, { timeout: 8000 });
    await click('.sheet [data-act="vers"]'); await click('.menu [data-act="ver-set"][data-x="v6"]'); assert(/scoped to runs under v6/.test(await txt(page, '.sheet .px-res')), 'scoped note');
    eq(await count(page, '.sheet .op'), 6, 'open list holds the six workflow.item instances'); await key('Escape');
  });
  await step('System: Sync now (Onyx) streams, then the health sentence loses its Onyx clause', async () => {
    await go(page, 'system'); await click('[data-act="sync-onyx"]'); await page.waitForSelector('.px-job');
    await page.waitForFunction(() => !/Onyx is 2h behind/.test(document.querySelector('.pagesum').textContent), null, { timeout: 15000 });
    assert(/^Sindri is degraded: the code index failed at the 03:00 build and the profile changed since approval\. Updated 2s ago\./.test(await txt(page, '.pagesum')), 'two-clause sentence');
  });
  await step('System: a graph node opens its doctor check; an engine row selects its history', async () => {
    await click('a.gn[data-id="code index"]'); await page.waitForSelector('.sheet'); assert(/code index/i.test(await page.getAttribute('.sheet', 'aria-label')), 'doctor sheet'); await key('Escape');
    await click('[data-row="en:ITM-01JD9"]'); assert(/ITM-01JD9\s*history/.test(await txt(page, '#hist-h')), 'history of JD9');
  });
  await step('Context: a graph node click selects its card; lens off hides its band; depth cycles; conflict popover opens', async () => {
    await go(page, 'context'); await click('[data-node="prod-session"]'); assert(/feature:session\.restore/.test(await txt(page, '.v-context .duo')), 'card follows');
    await click('[data-act="lens"][data-x="product"]'); await page.waitForTimeout(80); eq(await count(page, '.lens-band:not(.collapsed)'), 2, 'two bands left'); await click('[data-act="lens"][data-x="product"]');
    const d0 = await txt(page, '[data-act="depth"]'); await click('[data-act="depth"]'); assert((await txt(page, '[data-act="depth"]')) !== d0, 'depth changed');
    await click('[data-act="conflict"]'); await page.waitForSelector('.px-pop'); await key('Escape');
  });
  await step('Context: the Build draft helper reads the pending count (46, then 15 after Accept all)', async () => {
    eq(await txt(page, '#build-what'), 'keeps the 46 pending proposals'); await click('[data-act="accept-det"]'); eq(await txt(page, '#build-what'), 'keeps the 15 pending proposals');
    assert(/draft v14 \(46 proposals, 31 accepted\)/.test(await subHead()), 'header counts'); assert(await page.isDisabled('[data-act="accept-det"]'), 'Accept all is disabled once nothing is left');
    eq(await page.getAttribute('[data-act="accept-det"]', 'title'), 'Nothing deterministic left to accept', 'its reason');
  });
  await step('item sheet: Request form files a request into the Requests tab', async () => {
    await go(page, 'item/ITM-01JE7'); await page.waitForSelector('.sheet'); await click('.sheet [data-act="req-form"]'); await page.fill('#reqin', 'Use the shared fixture'); await click('.sheet [data-act="req-send"]');
    await click('.sheet [data-act="stab"][data-x="requests"]'); assert(/Use the shared fixture/.test(await txt(page, '.sheet .tabpanel')), 'request listed');
    await click('.sheet [data-act="stab"][data-x="timeline"]'); eq(await txt(page, '.sheet .tab[aria-selected="true"] .n'), '42', 'the timeline count grew by the new event');
  });

  done(errors); await browser.close();
})();
