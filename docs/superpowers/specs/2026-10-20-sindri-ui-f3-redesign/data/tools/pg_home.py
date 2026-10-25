from gap_common import page

BAR = """    <div class="bar">
      <span class="muted" id="sumline" style="font-size:13px"></span>
      <span class="sp"></span>
      <label class="stsw" data-gap="demo of an outline">View as
        <select id="stsel" aria-label="Show this state (prototype only)">
          <option value="populated">Busy day</option>
          <option value="firstrun">First run: setup unfinished</option>
          <option value="quiet">Nothing needs you</option>
          <option value="loading">Loading</option>
          <option value="stale">Out of date</option>
          <option value="down">Sindri not running</option>
        </select></label>
      <button class="btn primary" id="startwf">__PLAY__Start workflow</button>
    </div>"""
BODY = """        <div id="setupline"></div>
        <div id="home"></div>"""
CSS = """.hgrid{display:grid;grid-template-columns:minmax(0,1.3fr) minmax(0,1fr);gap:16px;margin:16px}
@media (max-width:1100px){.hgrid{grid-template-columns:1fr}}
.pan{border:1px solid var(--border);border-radius:8px;background:var(--card-warm);overflow:hidden}
.pan .ph{display:flex;align-items:center;gap:8px;height:40px;padding:0 16px;border-bottom:1px solid var(--border);font:600 12px/16px var(--font);letter-spacing:.06em;text-transform:uppercase;color:var(--faint)}
.pan .ph .n{margin-left:auto;text-transform:none;letter-spacing:0;font-weight:500}
.pan .ph a.n{text-decoration:underline}
.hrow{display:grid;grid-template-columns:28px minmax(0,1fr) auto;gap:12px;align-items:center;min-height:52px;padding:8px 16px;border-bottom:1px solid color-mix(in oklab,var(--border) 60%,transparent);width:100%;text-align:left}
.hrow:hover{background:var(--hover)}
.hrow:last-child{border-bottom:0}
.hrow .kt{width:24px;height:24px;border-radius:6px;display:flex;align-items:center;justify-content:center;background:var(--wait-bg);color:var(--wait-fg);border:1px solid var(--wait-bd)}
.hrow .kt .ico{width:14px;height:14px}
.hrow .t{font-weight:500;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.hrow .s{color:var(--muted-fg);font-size:12px;line-height:16px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.hrow .w{color:var(--faint);font-size:12px;font-variant-numeric:tabular-nums;white-space:nowrap}
.hrow .new{display:inline-block;margin-left:6px;font-size:11px;color:var(--wait-fg)}
.more{display:block;width:100%;text-align:left;padding:10px 16px;color:var(--muted-fg);font-size:12px;border-top:1px solid var(--border)}
.more:hover{background:var(--hover);color:var(--fg)}
.strip{display:grid;grid-template-columns:repeat(5,minmax(0,1fr));gap:1px;background:var(--border);margin:16px;border:1px solid var(--border);border-radius:8px;overflow:hidden}
@media (max-width:1100px){.strip{grid-template-columns:repeat(2,minmax(0,1fr))}}
.strip div{background:var(--card-warm);padding:10px 14px}
.strip small{display:block;color:var(--faint);font:500 11px/16px var(--font);text-transform:uppercase;letter-spacing:.06em}
.strip b{font-weight:500;font-size:13px}
.setupln{display:flex;align-items:center;gap:12px;margin:16px 16px 0;padding:10px 16px;border:1px solid var(--border);border-radius:8px;background:var(--card-warm)}
.setupln .meter{width:120px}"""

SCRIPT = r"""
/* Data: GET /sindri/needs-you (goldens needs-you/*), /queue, /workflow/instances, /status, /setup. Same sample world as Workflows and Needs you. */
const NEED = [
  { t: 'Approve the scope', s: 'Link expiry', w: '5h 51m', href: 'needs-you.html?state=pending', ic: P.clock },
  { t: 'Approve the merge', s: 'Bulk archive', w: '38m', href: 'needs-you.html#stop-ITM-01K5F', ic: P.clock },
  { t: 'Design review used all tries', s: 'Team invite links · paused run', w: '1h 04m', href: 'needs-you.html#stop-ITM-01K2C', ic: P.pause },
  { t: 'Accept this re-scope request?', s: 'Link expiry · agent decision', w: '52m', href: 'needs-you.html', ic: P.ask, isNew: true },
  { t: 'Approve a changed workflow', s: 'Review loop, version 3', w: '3h 20m', href: 'workflows.html', ic: I('<path d="M14 3H7a2 2 0 00-2 2v14a2 2 0 002 2h10a2 2 0 002-2V8z"/><path d="M14 3v5h5"/>') },
  { t: 'Approve settings changes', s: 'Settings for aw-notes', w: '1d 2h', href: 'setup.html', ic: I('<path d="M14 3H7a2 2 0 00-2 2v14a2 2 0 002 2h10a2 2 0 002-2V8z"/><path d="M14 3v5h5"/>') },
  { t: 'Approve all new workflows', s: '3 workflows', w: '20m', href: 'setup.html#approveall', ic: I('<path d="M14 3H7a2 2 0 00-2 2v14a2 2 0 002 2h10a2 2 0 002-2V8z"/><path d="M14 3v5h5"/>') },
];
const RUN = [
  ['running', 'Saved search sharing', 'Design · Review pass 2 of 4', '7h 21m', 'run-canvas.html?run=ITM-01JM8/line/1'],
  ['running', 'Email digest settings', 'Scoping · looking at product, code and history', '48m', 'run-canvas.html?run=ITM-01K1A/line/1'],
  ['running', 'Audit log filters', 'Plan · dry-run each phase', '1h 35m', 'run-canvas.html?run=ITM-01K3D/line/1'],
  ['running', 'Rate limit headers', 'Build and prove · phase 2 of 3', '2h 52m', 'run-canvas.html?run=ITM-01K4E/line/1'],
  ['took', 'Bulk tag rename', 'Plan · with you since 14:05', '2h 35m', 'needs-you.html?state=takeover'],
];
const QUEUED = [['Mobile layout for the notes list', 'aw-notes', '20m'], ['Keyboard shortcuts help', 'aw-notes', '38m'], ['Export endpoint pagination', 'aw-notes-api', '30h 40m']];
const st = { state: Q.get('state') || 'populated' };
const row = (n) => `<a class="hrow" href="${n.href}"><span class="kt">${n.ic}</span><span style="min-width:0"><div class="t">${esc(n.t)}${n.isNew ? '<span class="new">new</span>' : ''}</div><div class="s">${esc(n.s)}</div></span><span class="w">${n.w}</span></a>`;
function msg(t, p, acts) { return `<div class="empty-st" style="padding:72px 16px"><div class="box"><div class="tile">${P.warn}</div><h2>${t}</h2><p>${p}</p>${acts ? '<div class="acts">' + acts + '</div>' : ''}</div></div>`; }
function render() {
  document.getElementById('stsel').value = st.state;
  const s = st.state, sl = document.getElementById('setupline'), h = document.getElementById('home'), sum = document.getElementById('sumline');
  sl.innerHTML = s === 'firstrun' ? `<div class="setupln">${P.info}<b style="font-weight:500">Finish setting up Sindri</b><span class="muted">2 of 6 done</span><span class="meter"><i style="width:33%"></i></span><span class="muted" style="flex:1">Next: approve your settings</span><a class="btn primary" href="setup.html">Continue</a></div>` : '';
  const need = s === 'quiet' ? [] : NEED, run = s === 'quiet' ? [] : RUN, q = s === 'quiet' ? [] : QUEUED;
  sum.textContent = s === 'loading' || s === 'down' ? '' : need.length + ' need you · ' + run.filter((r) => r[0] === 'running').length + ' running · ' + run.filter((r) => r[0] === 'took').length + ' with you · ' + q.length + ' queued';
  if (s === 'down') { h.innerHTML = msg('Sindri is not running', 'Start it, then Home shows what needs you.', `<span class="copy" data-copy="sindri serve">${P.copy}<span>sindri serve</span></span>`); return; }
  if (s === 'loading') { h.innerHTML = `<div class="hgrid"><div class="pan"><div class="ph">Needs you</div>${Array.from({ length: 4 }, () => '<div class="skel" style="height:52px"><i style="width:50%"></i></div>').join('')}</div><div class="pan"><div class="ph">Workflows</div>${Array.from({ length: 4 }, () => '<div class="skel" style="height:52px"><i style="width:60%"></i></div>').join('')}</div></div>`; return; }
  const needHtml = need.length ? need.slice(0, 5).map(row).join('') + (need.length > 5 ? `<a class="more" href="needs-you.html">All ${need.length} →</a>` : '') : `<p class="faint" style="margin:0;padding:16px;font-size:13px">Nothing needs you.</p>`;
  const runHtml = run.length ? run.map((r) => `<a class="hrow" href="${r[4]}" style="grid-template-columns:auto minmax(0,1fr) auto"><span>${badge(r[0])}</span><span style="min-width:0"><div class="t">${esc(r[1])}</div><div class="s">${esc(r[2])}</div></span><span class="w">${r[3]}</span></a>`).join('') : `<p class="faint" style="margin:0;padding:16px;font-size:13px">Nothing running. Start a workflow to see it here.</p>`;
  const qHtml = q.length ? q.map((x) => `<a class="hrow" href="work.html" style="grid-template-columns:auto minmax(0,1fr) auto"><span>${badge('queued', '', 'New')}</span><span style="min-width:0"><div class="t">${esc(x[0])}</div><div class="s">${x[1]}</div></span><span class="w">${x[2]}</span></a>`).join('') + `<a class="more" href="work.html">Show all ${q.length} in Work</a>` : `<p class="faint" style="margin:0;padding:16px;font-size:13px">Nothing queued.</p>`;
  h.innerHTML = (s === 'stale' ? `<div class="note-line warn">${P.warn}<span>Showing the last answer. Sindri has not answered for 30 seconds.</span></div>` : '') + `
  <div class="hgrid">
    <section class="pan" aria-label="Needs you"><div class="ph">Needs you <span class="n">${need.length ? '<button style="color:inherit;text-decoration:underline" id="seen">Mark all seen</button>' : ''}</span></div>${needHtml}</section>
    <div style="display:flex;flex-direction:column;gap:16px">
      <section class="pan" aria-label="Workflows"><div class="ph">Running <span class="n">${run.length} open</span></div>${runHtml}<a class="more" href="workflows.html">Open Workflows</a></section>
      <section class="pan" aria-label="Queued"><div class="ph">Queued <span class="n">${q.length}</span></div>${qHtml}</section>
    </div></div>
  <div class="strip" aria-label="Health"><div><small>Process</small><b>Running · pid 4242</b></div><div><small>Epoch · lease</small><b>3 · held</b></div><div><small>API</small><b>v1</b></div><div><small>Services</small><b>6 scheduled</b></div><div><small>Profile</small><b>${s === 'firstrun' ? 'not approved' : 'approved'}</b></div></div>`;
  const seen = document.getElementById('seen'); if (seen) seen.onclick = () => toast('7 marked seen (not saved: this is a prototype)');
}
document.getElementById('stsel').onchange = (e) => { st.state = e.target.value; render(); };
document.getElementById('startwf').onclick = () => { window.location.href = 'workflows.html'; };
render();
"""

def build():
    play = "<svg class='ico' viewBox='0 0 24 24'><path d='M7 4l13 8-13 8z'/></svg>"
    return page("Home", "Home", '', BAR.replace("__PLAY__", play), BODY, SCRIPT, css=CSS)
