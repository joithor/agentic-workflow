from gap_common import page

TOP = '<span class="muted" id="hd" style="font-size:13px"></span>'
BAR = """    <div class="bar">
      <a class="btn" href="system.html" style="height:28px"><svg class="ico" viewBox="0 0 24 24"><path d="M15 6l-6 6 6 6"/></svg>System</a>
      <span class="sel" role="group" aria-label="Status"><span class="k">Status</span><span id="fS">Any status</span></span>
      <span class="sel" role="group" aria-label="Kind"><span class="k">Kind</span><span id="fK">Any kind</span></span>
      <span class="sp"></span>
      <span class="hint"><kbd>J</kbd><kbd>K</kbd> move <kbd>Enter</kbd> open <kbd>Esc</kbd> close</span>
      <label class="stsw" data-gap="demo of an outline">View as
        <select id="stsel" aria-label="Show this state (prototype only)">
          <option value="populated">Jobs</option>
          <option value="reading">Reading jobs</option>
          <option value="empty">No jobs yet</option>
          <option value="norunning">No running jobs</option>
          <option value="unreadable">Can't read the list</option>
          <option value="failed">Couldn't read just now</option>
        </select></label>
    </div>"""
BODY = """        <p class="note-line" style="margin-top:12px">__INFO__<span>Work Sindri does in the background, like syncing sources or building the context map.</span></p>
        <div id="list"></div>"""
ASIDE = '        <aside class="insp" id="insp" aria-label="Job details"></aside>'
CSS = """.jcols{grid-template-columns:minmax(180px,1.6fr) 110px 150px minmax(120px,1.2fr) 130px 56px}
@container (max-width:760px){.jcols{grid-template-columns:minmax(140px,1fr) 110px 56px}.jcols>:nth-child(3),.jcols>:nth-child(4),.jcols>:nth-child(5){display:none}}
.pcq{container-type:inline-size}
.prog{padding:8px 16px;border-bottom:1px solid var(--panel-border);display:flex;gap:12px;font-size:12px;line-height:18px}
.prog .r{color:var(--faint);width:44px;flex:none;font-variant-numeric:tabular-nums}
pre.res{margin:0;padding:12px 16px;font:12px/18px var(--mono);color:var(--fg);white-space:pre-wrap;overflow-wrap:anywhere}"""

SCRIPT = r"""
/* Data: GET /sindri/jobs and /jobs/<id> (goldens jobs/*, jobs-id/*). Kinds are the real job kinds; plain names are proposals. */
const KIND = { SyncSources: 'Sync sources', RefreshContextMap: 'Refresh the context map', BuildContextMap: 'Build the context map', RunService: 'Run a service', ApproveAllRecipes: 'Approve all workflows', AddRepo: 'Add a repository', ProbeLaunchTargets: 'Check agent launch targets', RunContextBacktest: 'Test the context map' };
const J = [
  { id: 'JOB-01KCM3', kind: 'RefreshContextMap', key: 'ITM-01JN2', item: 'ITM-01JN2', repo: 'aw-notes', service: 'context-map-refresh', state: 'running', startedBy: 'service:context-map-refresh', startedAt: '2026-10-19T16:39:10Z', endedAt: null, rounds: 2, summary: null, code: null, result: null, cancelledBy: null,
    progress: [{ round: 1, line: 'Read 41 files for Link expiry', at: '2026-10-19T16:39:12Z' }, { round: 2, line: 'Asked the product view and the code view', at: '2026-10-19T16:39:40Z' }], cli: 'sindri jobs cancel JOB-01KCM3' },
  { id: 'JOB-01KCL9', kind: 'RunService', key: 'heartbeat', item: null, repo: null, service: 'heartbeat', state: 'failed', startedBy: 'service:host', startedAt: '2026-10-19T16:38:00Z', endedAt: '2026-10-19T16:38:02Z', rounds: 0, summary: 'heartbeat could not reach the lease file', code: 'SND-SCHED-001', result: null, cancelledBy: null, progress: [], cli: null },
  { id: 'JOB-01KCK2', kind: 'SyncSources', key: 'aw-notes', item: null, repo: 'aw-notes', service: 'context-sync-gaps', state: 'succeeded', startedBy: 'service:context-sync-gaps', startedAt: '2026-10-19T16:30:00Z', endedAt: '2026-10-19T16:30:41Z', rounds: 3, summary: 'synced 3 sources', code: null, result: { sources: 3, behind: 0 },
    cancelledBy: null, progress: [{ round: 1, line: 'Linear: up to date', at: '2026-10-19T16:30:10Z' }, { round: 2, line: 'GitHub: 4 new pull requests', at: '2026-10-19T16:30:25Z' }, { round: 3, line: 'Docs: up to date', at: '2026-10-19T16:30:40Z' }], cli: null },
  { id: 'JOB-01KCJ7', kind: 'BuildContextMap', key: 'ITM-01K1A', item: 'ITM-01K1A', repo: 'aw-notes', service: null, state: 'succeeded', startedBy: 'human', startedAt: '2026-10-19T15:52:00Z', endedAt: '2026-10-19T15:58:30Z', rounds: 4, summary: 'built the map: 38 nodes', code: null, result: { nodes: 38, edges: 61 }, cancelledBy: null, progress: [], cli: null },
  { id: 'JOB-01KCH4', kind: 'ApproveAllRecipes', key: 'recipes', item: null, repo: null, service: null, state: 'cancelled', startedBy: 'human', startedAt: '2026-10-19T14:10:00Z', endedAt: '2026-10-19T14:10:05Z', rounds: 0, summary: 'cancelled', code: null, result: null, cancelledBy: 'human:app', progress: [], cli: null },
  { id: 'JOB-01KCG1', kind: 'RunContextBacktest', key: 'aw-notes', item: null, repo: 'aw-notes', service: null, state: 'failed', startedBy: 'session:6f2a91c4', startedAt: '2026-10-18T22:00:00Z', endedAt: '2026-10-18T22:04:10Z', rounds: 1, summary: 'interrupted: the process stopped while it ran', code: 'SND-JOB-001', result: null, cancelledBy: null, progress: [], cli: null },
];
const WORD = { running: ['running', 'Running'], succeeded: ['done', 'Done'], failed: ['failed', 'Failed'], cancelled: ['cancel', 'Cancelled'] };
const BY = (b) => b === 'service:host' ? 'Sindri' : b === 'human' ? 'you' : b.startsWith('service:') ? "Sindri's " + b.slice(8) + ' service' : b.startsWith('session:') ? 'an agent session' : b;
const st = { state: Q.get('state') || 'populated', sel: Q.get('open'), confirm: false };
function row(j) {
  return `<button class="crow cols jcols jrow" data-id="${j.id}" aria-selected="${st.sel === j.id}">
    <span style="font-weight:500;overflow:hidden;text-overflow:ellipsis">${KIND[j.kind] || j.kind} <span class="faint" style="font-weight:400">${esc(j.key)}</span></span>
    <span>${badge(WORD[j.state][0], '', WORD[j.state][1])}</span><span class="muted">${j.endedAt ? 'Ended ' : 'Started '}${ago(j.endedAt || j.startedAt)} ago</span>
    <span class="muted">${BY(j.startedBy)}</span><span class="muted" style="overflow:hidden;text-overflow:ellipsis">${j.summary ? esc(j.summary) : '<span class="faint">In progress</span>'}</span>
    <span class="faint tnum tm" style="text-align:right">${j.rounds ? (j.rounds === 1 ? "1 round" : j.rounds + " rounds") : ''}</span></button>`;
}
function msg(t, p, acts) { return `<div class="empty-st" style="padding:56px 16px"><div class="box"><div class="tile">${P.term}</div><h2>${t}</h2><p>${p}</p>${acts ? '<div class="acts">' + acts + '</div>' : ''}</div></div>`; }
function render() {
  document.getElementById('stsel').value = st.state;
  const list = document.getElementById('list');
  const run = J.filter((j) => j.state === 'running').length;
  document.getElementById('hd').textContent = ['reading', 'unreadable'].includes(st.state) ? '' : st.state === 'empty' ? '0 running · 0 jobs' : run + ' running · ' + J.length + ' jobs';
  let html = '';
  if (st.state === 'reading') html = `<div class="panel"><div class="ph">Reading jobs…</div>${Array.from({ length: 6 }, () => '<div class="skel"><i style="width:' + (30 + Math.random() * 40 | 0) + '%"></i></div>').join('')}</div>`;
  else if (st.state === 'empty') html = msg('No jobs have run yet', 'Start one from System, or in the terminal.', `<a class="btn primary" href="system.html?tab=services">Go to Services</a><span class="copy" data-copy="sindri services run &lt;name&gt;">${P.copy}<span>sindri services run &lt;name&gt;</span></span>`);
  else if (st.state === 'norunning') html = `<div class="panel pcq"><div class="cols chead jcols"><span>Job</span><span>Status</span><span>When</span><span>Started by</span><span>What happened</span><span></span></div><div class="faint" style="padding:12px 16px;font-size:12px;border-bottom:1px solid var(--border)">No running jobs.</div>${J.filter((j) => j.state !== 'running').map(row).join('')}</div>`;
  else if (st.state === 'unreadable') html = msg("The job list can't be read from this Sindri", 'The answer is in a shape this version of the app does not know. Update the app.', '');
  else if (st.state === 'failed') html = `<div class="note-line bad">${P.fail}<span>Couldn't read the jobs just now. Sindri did not answer.</span><button class="btn" style="margin-left:auto;height:24px">Try again</button></div><div class="panel pcq" style="opacity:.6"><div class="cols chead jcols"><span>Job</span><span>Status</span><span>When</span><span>Started by</span><span>What happened</span><span></span></div>${J.map(row).join('')}</div>`;
  else html = `<div class="panel pcq"><div class="cols chead jcols"><span>Job</span><span>Status</span><span>When</span><span>Started by</span><span>What happened</span><span style="text-align:right">Rounds</span></div>${J.map(row).join('')}</div>`;
  list.innerHTML = html;
  list.querySelectorAll('.jrow').forEach((b) => b.onclick = () => { st.sel = b.dataset.id; st.confirm = false; render(); });
  sheet();
}
function f(l, v, mono) { return v == null || v === '' ? '' : `<dt>${l}</dt><dd class="${mono ? 'mono' : ''}">${v}</dd>`; }
function sheet() {
  const el = document.getElementById('insp'), j = J.find((x) => x.id === st.sel);
  if (!j || ['reading', 'empty', 'unreadable'].includes(st.state)) { el.classList.remove('open'); el.innerHTML = ''; return; }
  el.classList.add('open');
  const run = j.state === 'running';
  el.innerHTML = `<div class="chrome"><span class="sp"></span><button class="ib" id="x" aria-label="Close (Esc)">${P.close}</button></div>
  <div class="scroll"><div class="hd"><div class="row1">${badge(WORD[j.state][0], '', WORD[j.state][1])}</div>
    <h2>${KIND[j.kind] || j.kind}</h2>
    <div class="meta"><span>${run ? 'Started ' + ago(j.startedAt) + ' ago' : 'Ended ' + ago(j.endedAt) + ' ago'}</span><span class="dotsep">·</span><span>by ${BY(j.startedBy)}</span><span class="dotsep">·</span><span class="copy" data-copy="${j.id}">${P.copy}<span>${j.id}</span></span></div>
    ${run ? `<div class="acts">${st.confirm ? `<span class="muted" style="align-self:center;font-size:12px">Cancel this job?</span><button class="btn" id="yes">Confirm</button><button class="btn" id="no">Keep</button>` : `<button class="btn" id="cj">Cancel job</button>`}</div>` : ''}</div>
  ${j.state === 'failed' ? `<div class="callout bad"><h3>${P.fail}It did not finish</h3><p>${esc(j.summary || '')}</p></div>` : ''}
  <div class="cap">Details</div><dl class="kv">
    ${f('Job id', j.id, 1)}${f('Kind', j.kind, 1)}${f('Key', j.key, 1)}${f('Item', j.item, 1)}${f('Repo', j.repo)}${f('Service', j.service)}${f('Started by (as served)', j.startedBy, 1)}${f('Error code', j.code, 1)}${f('Cancelled by', j.cancelledBy, 1)}${f('Rounds', j.rounds)}</dl>
  <div class="cap">Progress <span class="n">${j.progress.length ? j.progress.length + ' lines' : ''}</span></div>
  ${j.progress.length ? j.progress.map((p) => `<div class="prog"><span class="r">round ${p.round}</span><span>${esc(p.line)}</span><span class="faint" style="margin-left:auto">${hm(p.at)}</span></div>`).join('') : '<p class="faint" style="padding:12px 16px;margin:0;font-size:12px">No progress yet.</p>'}
  ${j.result ? `<div class="cap">What it produced</div><pre class="res">${esc(JSON.stringify(j.result, null, 2))}</pre>` : ''}
  ${j.cli ? `<div class="cap">To cancel from a terminal</div><div style="padding:12px 16px"><span class="copy" data-copy="${j.cli}">${P.copy}<span>${j.cli}</span></span></div>` : ''}</div>`;
  document.getElementById('x').onclick = () => { st.sel = null; render(); };
  const cj = document.getElementById('cj'); if (cj) cj.onclick = () => { st.confirm = true; render(); };
  const no = document.getElementById('no'); if (no) no.onclick = () => { st.confirm = false; render(); };
  const yes = document.getElementById('yes'); if (yes) yes.onclick = () => { st.confirm = false; toast('Cancelled (not saved: this is a prototype)'); render(); };
}
document.getElementById('stsel').onchange = (e) => { st.state = e.target.value; render(); };
document.addEventListener('keydown', (e) => {
  if (['INPUT', 'SELECT'].includes(e.target.tagName) || e.metaKey || e.ctrlKey) return;
  const rows = [...document.querySelectorAll('.jrow')]; const i = rows.findIndex((r) => r.dataset.id === st.sel);
  if (e.key === 'j') { st.sel = (rows[Math.min(i + 1, rows.length - 1)] || {}).dataset?.id; render(); }
  else if (e.key === 'k') { st.sel = (rows[Math.max(i - 1, 0)] || {}).dataset?.id; render(); }
  else if (e.key === 'Escape') { st.sel = null; render(); }
});
render();
"""

def build():
    info = "<svg class='ico' viewBox='0 0 24 24'><circle cx='12' cy='12' r='9'/><path d='M12 8v5M12 16h.01'/></svg>"
    return page("Jobs", "System", TOP, BAR, BODY.replace("__INFO__", info), SCRIPT, aside=ASIDE, css=CSS, h1="Background jobs")
