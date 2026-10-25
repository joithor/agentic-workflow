from gap_common import page

TOP = '<label class="search"><svg class="ico" viewBox="0 0 24 24"><circle cx="11" cy="11" r="7"/><path d="M21 21l-4.3-4.3"/></svg><input id="q" type="search" placeholder="Filter by id, title, repo" aria-label="Filter by id, title, repo" autocomplete="off"><kbd>/</kbd></label><span class="muted" id="hd" style="font-size:13px"></span>'

BAR = """    <div class="bar">
      <div class="seg" role="group" aria-label="Show" id="filters">
        <button data-f="all" aria-pressed="true">All <span class="n" id="n-all"></span></button>
        <button data-f="running" aria-pressed="false">Running <span class="n" id="n-run"></span></button>
      </div>
      <span class="sel" role="group" aria-label="Repo"><span class="k">Repo</span><span>All repos</span></span>
      <span class="sp"></span>
      <span class="hint"><kbd>J</kbd><kbd>K</kbd> move <kbd>Enter</kbd> open <kbd>Esc</kbd> close <kbd>C</kbd> new item</span>
      <label class="stsw" data-gap="demo of an outline">View as
        <select id="stsel" aria-label="Show this state (prototype only)">
          <option value="populated">Items</option>
          <option value="loading">Loading</option>
          <option value="empty">Nothing in the queue</option>
          <option value="nomatch">Filter matches nothing</option>
          <option value="rebuilding">Rebuilding views</option>
          <option value="stalled">Views stalled</option>
          <option value="stale">Out of date</option>
          <option value="down">Sindri not running</option>
        </select></label>
      <button class="btn primary" id="add">__PLUS__New item</button>
    </div>"""

BODY = """        <div id="banner"></div>
        <div id="list"></div>
        <p class="foot hidden" id="foot">__INFO__<span id="asof"></span></p>
        <div class="dlgback hidden" id="dlg" role="dialog" aria-modal="true" aria-labelledby="dt"></div>"""
ASIDE = '        <aside class="insp" id="insp" aria-label="Item details"></aside>'

CSS = """.dlgback{position:fixed;inset:0;background:rgba(0,0,0,.55);display:flex;align-items:center;justify-content:center;z-index:100}
.dlg{width:480px;max-width:92vw;background:var(--panel);border:1px solid var(--panel-border);border-radius:12px;padding:20px;box-shadow:0 20px 50px rgba(0,0,0,.5)}
.dlg h2{font:600 16px/22px var(--font);margin:0 0 12px}
.dlg label{display:block;font:500 12px/16px var(--font);color:var(--muted-fg);margin:12px 0 4px}
.dlg input,.dlg textarea{width:100%;border-radius:6px;border:1px solid var(--border);background:var(--field);padding:8px 10px;font:13px/19px var(--font);color:var(--fg);outline:none}
.dlg input:focus,.dlg textarea:focus{border-color:var(--ring)}
.dlg .err{color:var(--fail-fg);font-size:12px;margin-top:4px}
.dlg .acts{justify-content:flex-end;margin-top:16px}
.wcols{grid-template-columns:minmax(200px,2.4fr) 110px 200px 120px 56px}
@container (max-width:760px){.wcols{grid-template-columns:minmax(150px,1fr) 150px 56px}.wcols>:nth-child(2),.wcols>:nth-child(4){display:none}}
.pcq{container-type:inline-size}
.rowid{color:var(--faint);font-weight:400}"""

SCRIPT = r"""
/* Data: the shape of GET /sindri/queue (groups of items), GET /sindri/items/<id> and /items/<id>/timeline (goldens queue/*, items-id/*, items-id-timeline/*).
   Titles and ids are the sample world's runs; repos, the queued items and the history lines are mockup-authored. */
const ITEMS = [
  { id: 'ITM-01JN2', title: 'Link expiry', repo: 'aw-notes', state: 'needs-decision', step: 'scoping', since: '2026-10-19T10:49:00Z', group: 'waitingOnYou', waitingOn: 'DEC-01JN2A', instance: 'ITM-01JN2/line/1', createdBy: 'human:app', createdAt: '2026-10-19T09:30:00Z', version: 4, body: 'Shared links should stop working after a date the owner picks. Today they live forever.', link: null,
    decision: { question: 'Approve the scope for Link expiry?', options: ['Approve', 'Send back', 'Drop'] } },
  { id: 'ITM-01K5F', title: 'Bulk archive', repo: 'aw-notes', state: 'needs-decision', step: 'merge', since: '2026-10-19T14:55:00Z', group: 'waitingOnYou', waitingOn: 'DEC-01K5FB', instance: 'ITM-01K5F/line/1', createdBy: 'human:cli', createdAt: '2026-10-16T09:00:00Z', version: 9, body: 'Select many notes and archive them in one go.', link: 'github:pr/412',
    decision: { question: 'Approve the merge?', options: ['Approve the merge', 'Send back'] } },
  { id: 'ITM-01K2C', title: 'Team invite links', repo: 'aw-notes', state: 'parked', step: 'design', since: '2026-10-19T12:10:00Z', group: 'waitingOnYou', waitingOn: 'DEC-01K2CC', instance: 'ITM-01K2C/line/1', createdBy: 'human:app', createdAt: '2026-10-18T16:30:00Z', version: 7, body: null, link: null,
    decision: { question: 'The design review used all 3 tries. Allow more tries, or stop?', options: ['Allow 3 more', 'Stop the run'] } },
  { id: 'ITM-01JQ7', title: 'CSV import limits', repo: 'aw-notes-api', state: 'needs-decision', step: 'build', since: '2026-10-19T13:32:00Z', group: 'waitingOnYou', waitingOn: 'DEC-01JQ7D', instance: 'ITM-01JQ7/line/1', createdBy: 'human:cli', createdAt: '2026-10-19T08:00:00Z', version: 6, body: 'Reject imports over 10 MB with a clear message.', link: null,
    decision: { question: 'The code review found the behaviour wrong. Try the phase again, or stop?', options: ['Try again', 'Stop the run'] } },
  { id: 'ITM-01JP4', title: 'Bulk tag rename', repo: 'aw-notes', state: 'taken-over', step: 'plan', since: '2026-10-19T14:05:00Z', group: 'withYou', waitingOn: null, instance: 'ITM-01JP4/line/1', createdBy: 'human:app', createdAt: '2026-10-19T11:02:00Z', version: 5, body: null, link: null },
  { id: 'ITM-01JM8', title: 'Saved search sharing', repo: 'aw-notes', state: 'running', step: 'design', since: '2026-10-19T09:19:00Z', group: 'running', waitingOn: null, instance: 'ITM-01JM8/line/1', createdBy: 'human:cli', createdAt: '2026-10-19T08:10:00Z', version: 6, body: 'Let people share a saved search with a teammate by link.', link: 'linear:issue/AWN-142' },
  { id: 'ITM-01K1A', title: 'Email digest settings', repo: 'aw-notes', state: 'running', step: 'scoping', since: '2026-10-19T15:52:00Z', group: 'running', waitingOn: null, instance: 'ITM-01K1A/line/1', createdBy: 'human:app', createdAt: '2026-10-19T15:50:00Z', version: 2, body: null, link: null },
  { id: 'ITM-01K3D', title: 'Audit log filters', repo: 'aw-notes', state: 'running', step: 'plan', since: '2026-10-19T15:05:00Z', group: 'running', waitingOn: null, instance: 'ITM-01K3D/line/1', createdBy: 'human:cli', createdAt: '2026-10-18T09:40:00Z', version: 8, body: null, link: 'linear:issue/AWN-151' },
  { id: 'ITM-01K4E', title: 'Rate limit headers', repo: 'aw-notes-api', state: 'running', step: 'build', since: '2026-10-19T13:48:00Z', group: 'running', waitingOn: null, instance: 'ITM-01K4E/line/1', createdBy: 'human:cli', createdAt: '2026-10-17T10:00:00Z', version: 11, body: null, link: null },
  { id: 'ITM-01K9L', title: 'Mobile layout for the notes list', repo: 'aw-notes', state: 'new', step: null, since: '2026-10-19T16:20:00Z', group: 'queued', waitingOn: null, instance: null, createdBy: 'human:app', createdAt: '2026-10-19T16:20:00Z', version: 1, body: 'The list overflows on phones.', link: null },
  { id: 'ITM-01KAM', title: 'Keyboard shortcuts help', repo: 'aw-notes', state: 'new', step: null, since: '2026-10-19T16:02:00Z', group: 'queued', waitingOn: null, instance: null, createdBy: 'human:cli', createdAt: '2026-10-19T16:02:00Z', version: 1, body: null, link: null },
  { id: 'ITM-01KBN', title: 'Export endpoint pagination', repo: 'aw-notes-api', state: 'new', step: null, since: '2026-10-18T10:00:00Z', group: 'queued', waitingOn: null, instance: null, createdBy: 'human:cli', createdAt: '2026-10-18T10:00:00Z', version: 1, body: null, link: null },
];
const CLOSED = [
  { id: 'ITM-01K6G', title: 'Webhook retry backoff', repo: 'aw-notes-api', state: 'done', step: null, since: '2026-10-19T11:20:00Z', group: 'closed', waitingOn: null, instance: 'ITM-01K6G/line/1', createdBy: 'human:cli', createdAt: '2026-10-15T09:00:00Z', version: 14, body: null, link: 'github:pr/398' },
  { id: 'ITM-01K7H', title: 'Export to PDF', repo: 'aw-notes', state: 'cancelled', step: null, since: '2026-10-18T17:30:00Z', group: 'closed', waitingOn: null, instance: 'ITM-01K7H/line/1', createdBy: 'human:app', createdAt: '2026-10-16T14:00:00Z', version: 8, body: null, link: null },
  { id: 'ITM-01K8J', title: 'Dark mode toggle', repo: 'aw-notes', state: 'cancelled', step: null, since: '2026-10-17T10:05:00Z', group: 'closed', waitingOn: null, instance: 'ITM-01K8J/line/1', createdBy: 'human:app', createdAt: '2026-10-17T08:00:00Z', version: 3, body: null, link: null },
];
const GROUPS = [['waitingOnYou', 'Waiting on you'], ['withYou', 'With you'], ['running', 'Running'], ['queued', 'Queued']];
const WORD = { new: 'New', running: 'Running', 'needs-decision': 'Needs decision', parked: 'Paused: needs a decision', 'taken-over': 'With you', done: 'Done', cancelled: 'Cancelled' };
const KIND = { new: 'queued', running: 'running', 'needs-decision': 'need', parked: 'paused', 'taken-over': 'took', done: 'done', cancelled: 'cancel' };
const STAGE = { scoping: 'Scoping', design: 'Design', plan: 'Plan', build: 'Build and prove', merge: 'Merge' };
const EMPTY = { waitingOnYou: 'Nothing is waiting on you.', withYou: 'Nothing is with you.', running: 'Nothing running.', queued: 'Nothing queued.' };
const st = { state: Q.get('state') || 'populated', filter: Q.get('filter') || 'all', q: '', sel: Q.get('open'), closed: false, confirm: null, dialog: Q.get('dialog') };
const HIST = (it) => [
  ['item.created', it.createdBy, it.createdAt, 'Created: ' + it.title],
  ...(it.link ? [['item.linked', 'service:host', it.createdAt, 'Linked to ' + it.link]] : []),
  ...(it.instance ? [['workflow.started', 'service:host', it.createdAt, 'Started Idea to merged pull request']] : []),
  ...(it.waitingOn ? [['decision.filed', 'service:host', it.since, 'Waiting for your answer: ' + it.decision.question]] : []),
  ...(it.state === 'taken-over' ? [['workflow.taken-over', 'human:app', it.since, 'You took over']] : []),
  ...(it.state === 'done' ? [['item.closed', 'human:app', it.since, 'Closed as done']] : []),
  ...(it.state === 'cancelled' ? [['item.closed', 'human:app', it.since, 'Closed as cancelled']] : []),
].reverse();

function match(it) { return !st.q || (it.id + ' ' + it.title + ' ' + it.repo).toLowerCase().includes(st.q); }
function row(it) {
  const k = KIND[it.state];
  return `<button class="crow cols wcols qrow" data-id="${it.id}" aria-selected="${st.sel === it.id}">
    <span style="font-weight:500;display:flex;gap:8px;align-items:center;min-width:0"><span style="overflow:hidden;text-overflow:ellipsis">${esc(it.title)}</span><span class="mono rowid">${it.id}</span></span>
    <span class="muted">${esc(it.repo)}</span>
    <span>${badge(k, '', WORD[it.state])}</span>
    <span class="muted">${it.step ? STAGE[it.step] : '<span class="faint">Not started</span>'}</span>
    <span class="faint tnum tm" style="text-align:right">${ago(it.since)}</span></button>`;
}
function skeleton() { return Array.from({ length: 7 }, () => '<div class="skel"><i style="width:' + (30 + Math.random() * 40 | 0) + '%"></i></div>').join(''); }
const HEADROW = '<div class="cols chead wcols"><span>Title</span><span>Repo</span><span>Status</span><span data-gap="plain stage name: item.step is a raw step id">Stage</span><span style="text-align:right">Age</span></div>';
function msg(title, text, acts) { return `<div class="empty-st" style="padding:48px 16px"><div class="box"><div class="tile">${I('<path d="M9 6h12M9 12h12M9 18h12"/><path d="M3 6l1 1 2-2M3 12l1 1 2-2M3 18l1 1 2-2"/>')}</div><h2>${title}</h2><p>${text}</p>${acts ? '<div class="acts">' + acts + '</div>' : ''}</div></div>`; }

function render() {
  document.getElementById('stsel').value = st.state;
  const list = document.getElementById('list'), banner = document.getElementById('banner');
  const vis = ITEMS.filter(match).filter((i) => st.filter === 'all' || i.state === 'running');
  document.querySelectorAll('#filters button').forEach((b) => b.setAttribute('aria-pressed', b.dataset.f === st.filter));
  document.getElementById('n-all').textContent = ITEMS.length; document.getElementById('n-run').textContent = ITEMS.filter((i) => i.state === 'running').length;
  const wait = ITEMS.filter((i) => i.group === 'waitingOnYou').length;
  document.getElementById('hd').textContent = st.state === 'empty' ? '0 open' : ITEMS.length + ' open · ' + wait + ' waiting on you';
  banner.innerHTML = ''; document.getElementById('foot').classList.add('hidden');
  let html = '';
  if (st.state === 'stale') banner.innerHTML = `<div class="note-line warn">${P.warn}<span>Showing the last answer. Sindri has not answered for 30 seconds.</span></div>`;
  if (st.state === 'rebuilding') banner.innerHTML = `<div class="note-line">${P.spin}<span>Rebuilding views (cursor 8 of 8)…</span></div>`;
  if (st.state === 'stalled') banner.innerHTML = `<div class="note-line bad">${P.fail}<span>Views are stalled at 9: run <span class="copy" data-copy="sindri views rebuild">${P.copy}<span>sindri views rebuild</span></span></span></div>`;
  if (st.state === 'loading') html = `<div class="panel"><div class="ph">Reading the queue…</div>${skeleton()}</div>`;
  else if (st.state === 'down') html = msg('Sindri is not running', 'The queue comes from the Sindri control plane, and it is not answering.', '<span class="copy" data-copy="sindri serve">' + P.copy + '<span>sindri serve</span></span>');
  else if (st.state === 'empty') html = msg('Nothing in the queue', 'Add the first item, or run it from the terminal.', `<button class="btn primary" id="add2">${P.plus}New item</button><span class="copy" data-copy="sindri item add">${P.copy}<span>sindri item add</span></span>`);
  else if (st.state === 'nomatch' || (!vis.length)) html = msg('No items match', 'Nothing matches your filter. Clear it to see the whole queue.', '<a class="btn" href="work.html">Clear filter</a>');
  else {
    const reb = st.state === 'rebuilding' || st.state === 'stalled';
    html = '<div class="panel pcq">' + HEADROW + GROUPS.map(([g, name]) => {
      const rows = (reb && g !== 'queued') ? [] : vis.filter((i) => i.group === g);
      const emptyText = st.state === 'rebuilding' ? 'Rebuilding views (cursor 8 of 8)…' : st.state === 'stalled' ? 'Views are stalled at 9. Run sindri views rebuild.' : EMPTY[g];
      return `<div class="grp" style="position:static"><b>${name}</b> <span class="n">${rows.length}</span></div>` + (rows.length ? rows.map(row).join('') : `<div class="faint" style="padding:8px 16px;font-size:12px;border-bottom:1px solid var(--border)">${emptyText}</div>`);
    }).join('') + `<button class="fold" id="fold" aria-expanded="${st.closed}">${P.chev}<span>Closed (${CLOSED.length})</span></button>` + (st.closed ? CLOSED.map(row).join('') : '') + '</div>';
  }
  list.innerHTML = html;
  if (!['loading', 'down', 'empty'].includes(st.state)) { document.getElementById('foot').classList.remove('hidden'); document.getElementById('asof').textContent = st.state === 'stalled' ? 'Updated up to event 8 of 9.' : 'Up to date with the ledger (event 214).'; }
  list.querySelectorAll('.qrow').forEach((b) => b.onclick = () => { st.sel = b.dataset.id; st.confirm = null; render(); });
  const f = document.getElementById('fold'); if (f) f.onclick = () => { st.closed = !st.closed; render(); };
  const a2 = document.getElementById('add2'); if (a2) a2.onclick = () => { st.dialog = 'create'; render(); };
  sheet(); dialog();
}
function sheet() {
  const el = document.getElementById('insp'), it = [...ITEMS, ...CLOSED].find((x) => x.id === st.sel);
  if (!it || ['loading', 'down', 'empty'].includes(st.state)) { el.classList.remove('open'); el.innerHTML = ''; return; }
  const open = !['done', 'cancelled'].includes(it.state);
  el.classList.add('open');
  const hist = HIST(it);
  el.innerHTML = `<div class="chrome"><span class="sp"></span><button class="ib" id="x" aria-label="Close (Esc)">${P.close}</button></div>
  <div class="scroll"><div class="hd"><div class="row1">${badge(KIND[it.state], '', WORD[it.state])}<span class="muted">${esc(it.repo)}</span></div>
    <h2>${esc(it.title)}</h2>
    <div class="meta"><span>${WORD[it.state]} for ${ago(it.since)}</span><span class="dotsep">·</span><span class="copy" data-copy="${it.id}">${P.copy}<span>${it.id}</span></span></div>
    ${it.body ? `<p style="margin:12px 0 0;font-size:13px;line-height:20px">${esc(it.body)}</p>` : '<p class="faint" style="margin:12px 0 0;font-size:12px">No description.</p>'}
    <div class="acts">${it.instance ? `<a class="btn primary" href="run-canvas.html?run=${it.instance}">${P.open}Open the run</a>` : `<button class="btn primary" data-gap="start from an item: served by Start workflow">${P.play}Start a workflow</button>`}
      ${open ? (st.confirm ? `<span class="muted" style="align-self:center;font-size:12px">Close "${esc(it.title)}" as ${st.confirm}?</span><button class="btn" id="yes">Confirm</button><button class="btn" id="no">Keep</button>` : `<button class="btn" id="cd">Mark done</button><button class="btn" id="cc">Cancel item</button>`) : ''}</div></div>
  ${it.waitingOn ? `<div class="callout need"><h3>${P.clock}Waiting for you</h3><p>${esc(it.decision.question)}</p><div class="acts">${it.decision.options.map((o, i) => `<a class="btn ${i === 0 ? 'primary' : ''}" href="needs-you.html#stop-${it.id}">${esc(o)}</a>`).join('')}</div><p style="margin-top:8px">Answer it in <a href="needs-you.html#stop-${it.id}" style="text-decoration:underline">Needs you</a>, where the whole request is shown.</p></div>` : ''}
  <div class="cap">Details</div><dl class="kv">
    <dt data-gap="waitingOn is a decision id; the question is read from the decision">Waiting for</dt><dd class="mono">${it.waitingOn || '<span class="faint">Nobody</span>'}</dd>
    <dt>Run</dt><dd class="mono">${it.instance ? `<a href="run-canvas.html?run=${it.instance}" style="text-decoration:underline">${it.instance}</a>` : '<span class="faint">Not started</span>'}</dd>
    <dt>Link</dt><dd class="mono">${it.link || '<span class="faint">None</span>'}</dd>
    <dt>Item id</dt><dd class="mono">${it.id} <span class="copy" data-copy="${it.id}" style="margin-left:6px">${P.copy}<span>Copy</span></span></dd>
    <dt>Created by</dt><dd class="mono">${it.createdBy}</dd>
    <dt>Created</dt><dd>${utc(it.createdAt)}</dd><dt>Version</dt><dd>${it.version}</dd></dl>
  <div class="cap">History <span class="n">${hist.length}</span></div>
  ${hist.map((h, i) => `<div class="stg ${i === 0 ? 'cur' : ''}"><div class="sh"><div class="sn">${esc(h[3])}<small>${esc(h[1])} · ${utc(h[2])}</small></div><span class="sd">${ago(h[2])} ago</span></div></div>`).join('')}
  <p class="foot">${P.info}<span>Mark done and Cancel item close the item; they do not stop a run in progress. Version ${it.version}; a stale version is refused.</span></p></div>`;
  document.getElementById('x').onclick = () => { st.sel = null; st.confirm = null; render(); };
  const b = (id, v) => { const e = document.getElementById(id); if (e) e.onclick = () => { st.confirm = v; render(); }; };
  b('cd', 'done'); b('cc', 'cancelled'); b('no', null);
  const y = document.getElementById('yes'); if (y) y.onclick = () => { toast('Closed (not saved: this is a prototype)'); st.confirm = null; render(); };
}
function dialog() {
  const d = document.getElementById('dlg');
  if (st.dialog !== 'create') { d.classList.add('hidden'); d.innerHTML = ''; return; }
  const bad = Q.get('error') === 'title';
  d.classList.remove('hidden');
  d.innerHTML = `<div class="dlg"><h2 id="dt">New item</h2>
    <label for="t">Title (required)</label><input id="t" value="${bad ? '' : 'Pin notes to the top'}" aria-invalid="${bad}" ${bad ? 'aria-describedby="te"' : ''}>${bad ? '<div class="err" id="te">Title is required</div>' : ''}
    <label for="r">Repo</label><input id="r" value="aw-notes">
    <label for="b">Body</label><textarea id="b" rows="3" placeholder="What should change, and why"></textarea>
    <div class="acts"><button class="btn" id="dc">Cancel</button><button class="btn primary" id="dok">Create item</button></div></div>`;
  document.getElementById('dc').onclick = () => { st.dialog = null; render(); };
  document.getElementById('dok').onclick = () => { st.dialog = null; toast('Created (not saved: this is a prototype)'); render(); };
}
document.getElementById('stsel').onchange = (e) => { st.state = e.target.value; render(); };
document.getElementById('q').oninput = (e) => { st.q = e.target.value.toLowerCase(); render(); };
document.getElementById('add').onclick = () => { st.dialog = 'create'; render(); };
document.getElementById('filters').onclick = (e) => { const b = e.target.closest('button'); if (b) { st.filter = b.dataset.f; render(); } };
document.addEventListener('keydown', (e) => {
  if (e.metaKey || e.ctrlKey || e.altKey || ['INPUT', 'SELECT', 'TEXTAREA'].includes(e.target.tagName)) return;
  const rows = [...document.querySelectorAll('.qrow')]; const i = rows.findIndex((r) => r.dataset.id === st.sel);
  if (e.key === 'j' || e.key === 'ArrowDown') { st.sel = (rows[Math.min(i + 1, rows.length - 1)] || {}).dataset?.id; render(); }
  else if (e.key === 'k' || e.key === 'ArrowUp') { st.sel = (rows[Math.max(i - 1, 0)] || {}).dataset?.id; render(); }
  else if (e.key === 'Escape') { if (st.dialog) st.dialog = null; else st.sel = null; render(); }
  else if (e.key === '/') { e.preventDefault(); document.getElementById('q').focus(); }
  else if (e.key === 'c') { st.dialog = 'create'; render(); }
});
if (Q.get('closed')) st.closed = true;
render();
"""

def build():
    plus = "<svg class='ico' viewBox='0 0 24 24'><path d='M12 5v14M5 12h14'/></svg>"
    info = "<svg class='ico' viewBox='0 0 24 24'><circle cx='12' cy='12' r='9'/><path d='M12 8v5M12 16h.01'/></svg>"
    return page("Work", "Work", TOP, BAR.replace("__PLUS__", plus), BODY.replace("__INFO__", info), SCRIPT, aside=ASIDE, css=CSS)
