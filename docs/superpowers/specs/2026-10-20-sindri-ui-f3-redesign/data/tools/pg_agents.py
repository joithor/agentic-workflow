from gap_common import page

TOP = '<label class="search"><svg class="ico" viewBox="0 0 24 24"><circle cx="11" cy="11" r="7"/><path d="M21 21l-4.3-4.3"/></svg><input id="q" type="search" placeholder="Search agents" aria-label="Search agents" autocomplete="off"><kbd>/</kbd></label>'

BAR = """    <div class="bar">
      <span class="sel" id="fProv" data-gap="filter lists come from the served sessions" role="group" aria-label="Provider"><span class="k">Provider</span><span id="fProvV">All providers</span></span>
      <span class="sel" id="fState" role="group" aria-label="Status"><span class="k">Status</span><span id="fStateV">Any status</span></span>
      <span class="sel" id="fRepo" role="group" aria-label="Repo"><span class="k">Repo</span><span id="fRepoV">All repos</span></span>
      <span class="sp"></span>
      <span class="hint" id="hint"><kbd>J</kbd><kbd>K</kbd> move <kbd>Enter</kbd> open <kbd>Esc</kbd> close</span>
      <label class="stsw" data-gap="demo of an outline">View as
        <select id="stsel" aria-label="Show this state (prototype only)">
          <option value="populated">Agents running</option>
          <option value="reading">Reading sessions</option>
          <option value="empty">No agents</option>
          <option value="nomatch">Filters match nothing</option>
          <option value="problems">A source failed</option>
          <option value="stale">Out of date</option>
          <option value="unavailable">Can't see sessions</option>
          <option value="unreadable">Can't read the answer</option>
        </select></label>
    </div>"""

BODY = """        <div id="stale" class="note-line warn hidden">__WARN__<span>Showing the last answer. Sindri has not answered for 30 seconds.</span></div>
        <div id="problems"></div>
        <div class="panel pcq" id="panel" aria-label="Agents"></div>
        <p class="foot" id="foot">__INFO__<span>Cursor IDE chats aren't shown.</span></p>"""

ASIDE = '        <aside class="insp" id="insp" aria-label="Agent details"></aside>'

SCRIPT = r"""
/* Data: the shape of GET /sindri/sessions and GET /sindri/sessions/<ref> (goldens sessions/*, sessions-id/*).
   "Now" is the sample clock. Names are from the sample world (aw-notes); the box, panes and pids are mockup-authored. */
const S = [
  { ref: 'claude-code/6f2a91c4-7b1e-4d0a-9a53-2c8d1e07b3a1', provider: 'claude-code', providerLabel: 'Claude Code', id: '6f2a91c4-7b1e-4d0a-9a53-2c8d1e07b3a1', idKnown: true,
    state: 'running', asking: null, stateSource: 'hook', estimated: false, repo: 'aw-notes', worktree: '.claude/worktrees/rate-limit-headers',
    host: { kind: 'tmux', label: 'tmux · Warp', socketKind: 'team', attachedClients: 1, terminal: 'warp', session: 'sindri-builders', pane: '%4', socket: '/tmp/tmux-1000/sindri' },
    lane: null, launchId: 'LCH-01K4E2', startedAt: '2026-10-19T13:50:00Z', since: '2026-10-19T16:31:00Z', title: 'Build phase 2: rate limit headers', pid: 48211,
    launch: { instance: 'ITM-01K4E/line/1/phase.1/build.2', state: 'build', attempt: 1 }, cwd: '/home/thor/repos/aw-wt/aw-notes-rate-limit',
    history: [['asking', 'approval', '2026-10-19T16:12:00Z', 'hook'], ['running', null, '2026-10-19T16:14:00Z', 'hook'], ['idle', null, '2026-10-19T16:29:00Z', 'hook'], ['running', null, '2026-10-19T16:31:00Z', 'hook']] },
  { ref: 'claude-code/b3d07e55-1a92-4c6f-8e10-90a4f2c5d6e7', provider: 'claude-code', providerLabel: 'Claude Code', id: 'b3d07e55-1a92-4c6f-8e10-90a4f2c5d6e7', idKnown: true,
    state: 'asking', asking: { reason: 'approval', tool: 'Bash', since: '2026-10-19T16:36:00Z' }, stateSource: 'hook', estimated: false, repo: 'aw-notes', worktree: '.claude/worktrees/audit-log-filters',
    host: { kind: 'tmux', label: 'tmux · Warp', socketKind: 'team', attachedClients: 0, terminal: 'warp', session: 'sindri-builders', pane: '%7', socket: '/tmp/tmux-1000/sindri' },
    lane: null, launchId: 'LCH-01K3D9', startedAt: '2026-10-19T15:04:00Z', since: '2026-10-19T16:36:00Z', title: 'Dry-run each phase: audit log filters', pid: 48630,
    launch: { instance: 'ITM-01K3D/line/1/plan.1/dryrun.1', state: 'dry-run', attempt: 1 }, cwd: '/home/thor/repos/aw-wt/aw-notes-audit-log',
    history: [['running', null, '2026-10-19T15:05:00Z', 'hook'], ['asking', 'approval', '2026-10-19T16:36:00Z', 'hook']] },
  { ref: 'codex/0194f8a2-c7d3-7e41-b2a0-5d9e11f03a68', provider: 'codex', providerLabel: 'Codex', id: '0194f8a2-c7d3-7e41-b2a0-5d9e11f03a68', idKnown: true,
    state: 'running', asking: null, stateSource: 'record', estimated: false, repo: 'aw-notes', worktree: null,
    host: { kind: 'warp', label: 'Warp', socketKind: null, attachedClients: null, terminal: 'warp', session: null, pane: null, socket: null },
    lane: { team: 'sindri', lane: 'ui' }, launchId: null, startedAt: '2026-10-19T14:20:00Z', since: '2026-10-19T16:22:00Z', title: 'Fix the empty state of the notes list', pid: 40977,
    launch: null, cwd: '/home/thor/repos/aw-notes',
    history: [['running', null, '2026-10-19T14:20:00Z', 'record'], ['idle', null, '2026-10-19T16:10:00Z', 'record'], ['running', null, '2026-10-19T16:22:00Z', 'record']] },
  { ref: 'claude-code/e81c3b20-44f5-4a7d-a6b9-f0d27c9e8a15', provider: 'claude-code', providerLabel: 'Claude Code', id: 'e81c3b20-44f5-4a7d-a6b9-f0d27c9e8a15', idKnown: true,
    state: 'idle', asking: null, stateSource: 'record', estimated: true, repo: 'aw-notes-api', worktree: null,
    host: { kind: 'tmux', label: 'tmux · default', socketKind: 'default', attachedClients: 1, terminal: 'gnome-terminal', session: 'main', pane: '%1', socket: '/tmp/tmux-1000/default' },
    lane: null, launchId: null, startedAt: '2026-10-19T09:02:00Z', since: '2026-10-19T12:58:00Z', title: null, pid: 31560,
    launch: null, cwd: '/home/thor/repos/aw-notes-api',
    history: [['running', null, '2026-10-19T09:02:00Z', 'record'], ['idle', null, '2026-10-19T12:58:00Z', 'record']] },
  { ref: 'cursor/pid-52044-1760894400', provider: 'cursor', providerLabel: 'Cursor', id: 'pid-52044-1760894400', idKnown: false,
    state: 'idle', asking: null, stateSource: 'record', estimated: true, repo: null, worktree: null,
    host: { kind: 'unknown', label: 'unknown', socketKind: null, attachedClients: null, terminal: null, session: null, pane: null, socket: null },
    lane: null, launchId: null, startedAt: null, since: '2026-10-19T15:40:00Z', title: null, pid: 52044, launch: null, cwd: null, history: [] },
];
const ENDED = [
  { s: { ...S[2], ref: 'claude-code/2c9ab417-8e03-41d2-b6f5-71a0c3d9e4b2', provider: 'claude-code', providerLabel: 'Claude Code', id: '2c9ab417-8e03-41d2-b6f5-71a0c3d9e4b2', repo: 'aw-notes', title: 'Scoping: email digest settings', lane: null, launch: { instance: 'ITM-01K1A/line/1/scoping.1', state: 'gather', attempt: 1 }, launchId: 'LCH-01K1A4', pid: 47102 },
    endedAt: '2026-10-19T16:20:00Z', resume: ['claude', '--resume', '2c9ab417-8e03-41d2-b6f5-71a0c3d9e4b2'] },
  { s: { ...S[3], ref: 'codex/0194f1d0-aa31-7c02-9f6e-3b7d20c81e44', provider: 'codex', providerLabel: 'Codex', id: '0194f1d0-aa31-7c02-9f6e-3b7d20c81e44', repo: 'aw-notes-api', title: 'Add the export endpoint', pid: 29981 },
    endedAt: '2026-10-19T14:02:00Z', resume: ['codex', 'resume', '0194f1d0-aa31-7c02-9f6e-3b7d20c81e44'] },
  { s: { ...S[4], ref: 'cursor/pid-18820-1760880000', id: 'pid-18820-1760880000', pid: 18820 }, endedAt: '2026-10-19T12:10:00Z', resume: null },
];
const st = { state: Q.get('state') || 'populated', sel: Q.get('open'), ended: false, q: '' };

function where(s) { return s.worktree ? s.repo + ' · ' + s.worktree.split('/').pop() : (s.repo || '<span class="faint">Unknown repository</span>'); }
function doing(s) {
  if (s.state === 'asking') return `<span>${esc(s.asking.reason === 'approval' ? 'Wants to run' : s.asking.reason)}${s.asking.tool ? ' ' + esc(s.asking.tool) : ''}</span>`;
  if (s.launch) return `<span>On a workflow step · ${esc(s.launch.state)}${s.launch.attempt > 1 ? ' · attempt ' + s.launch.attempt : ''}</span>`;
  if (s.lane) return `<span>Lane ${esc(s.lane.team)}/${esc(s.lane.lane)}</span>`;
  return '<span class="faint">Not part of a workflow</span>';
}
function row(s) {
  const t = s.title ? esc(s.title) : '<span class="faint">No title</span>';
  return `<button class="crow cols agrow" role="row" data-ref="${s.ref}" aria-selected="${st.sel === s.ref}" style="grid-template-columns:150px minmax(160px,2fr) minmax(120px,1.2fr) 100px minmax(130px,1.4fr) 56px">
    <span data-gap="state word">${badge(s.state, '', s.state === 'asking' ? 'Asking you' : undefined)}${s.estimated ? ' <span class="faint" title="Worked out from the process, not reported by the agent" style="font-size:11px">estimated</span>' : ''}</span>
    <span style="font-weight:500"><span class="muted" style="font-weight:400">${esc(s.providerLabel)}</span> &nbsp;${t}</span>
    <span class="muted">${where(s)}</span>
    <span class="muted">${esc(s.host.label)}</span>
    <span class="muted">${doing(s)}</span>
    <span class="faint tnum tm" style="text-align:right">${ago(s.since)}</span></button>`;
}
const HEAD = '<div class="cols chead" style="grid-template-columns:150px minmax(160px,2fr) minmax(120px,1.2fr) 100px minmax(130px,1.4fr) 56px"><span>Status</span><span>Agent</span><span>Repository</span><span>Where it runs</span><span>What it is doing</span><span style="text-align:right">For</span></div>';
const HEAD2 = HEAD.replace('minmax(150px,1.8fr)', 'minmax(150px,1.6fr)');
function group(name, list, n) { return `<div class="grp" style="position:static" data-gap="${name === 'Started by Sindri' ? 'launch link: served' : 'demo of an outline'}">${name} <span class="n">(${n})</span></div>` + list.map(row).join(''); }
function endedRow(e) {
  const s = e.s;
  return `<button class="crow cols agrow" data-ref="${s.ref}" data-ended="1" aria-selected="${st.sel === s.ref}" style="grid-template-columns:150px minmax(160px,2fr) minmax(120px,1.2fr) minmax(200px,1.8fr) 100px">
    <span><span class="badge s-idle">${P.done}Ended</span></span>
    <span style="font-weight:500"><span class="muted" style="font-weight:400">${esc(s.providerLabel)}</span> &nbsp;${s.title ? esc(s.title) : '<span class="faint">No title</span>'}</span>
    <span class="muted">${where(s)}</span>
    <span>${e.resume ? `<span class="copy" data-copy="${esc(e.resume.join(' '))}" title="Copy the command that picks this session up again">${P.copy}<span>${esc(e.resume.join(' '))}</span></span>` : '<span class="faint">No resume command</span>'}</span>
    <span class="faint tnum tm" style="text-align:right">ended ${ago(e.endedAt)} ago</span></button>`;
}
function matches(s) { return !st.q || (s.providerLabel + ' ' + (s.title || '') + ' ' + (s.repo || '')).toLowerCase().includes(st.q); }
function skeleton() { return Array.from({ length: 5 }, () => '<div class="skel"><i style="width:' + (40 + Math.random() * 40 | 0) + '%"></i></div>').join(''); }

function render() {
  const panel = document.getElementById('panel'), pb = document.getElementById('problems'), stale = document.getElementById('stale');
  const sel = document.getElementById('stsel'); sel.value = st.state;
  stale.classList.toggle('hidden', st.state !== 'stale');
  pb.innerHTML = st.state === 'problems' ? `<div class="note-line warn">${P.warn}<span>The Claude Code records could not be read in the last pass. The list may be missing agents. <span class="mono faint">claude-records</span></span></div>` : '';
  const live = S.filter(matches);
  const asking = S.filter((s) => s.state === 'asking').length;
  let head = S.length + ' agents · ' + asking + ' asking you';
  const msg = (t, extra = '') => `<div class="empty-st" style="padding:40px 16px"><div class="box"><div class="tile">${P.term}</div>${t}</div></div>`;
  let html = '';
  if (st.state === 'reading') { head = ''; html = `<div class="ph">Agents <span class="n">Reading sessions…</span></div>` + skeleton(); }
  else if (st.state === 'unavailable') { head = ''; html = msg(`<h2>Sindri can't see agent sessions</h2><p>Sindri can't see agent sessions on this system: the process list could not be read.</p><div class="acts"><a class="btn" href="system.html">Open System</a></div>`); }
  else if (st.state === 'unreadable') { head = ''; html = msg(`<h2>This app is older than Sindri</h2><p>The agent list can't be read from this Sindri. Update the app, then it will show again.</p>`); }
  else if (st.state === 'empty') { head = '0 agents · 0 asking you'; html = msg(`<h2>No agent sessions are running</h2><p>When you start an agent in a terminal, or Sindri starts one for a workflow step, it shows up here.</p><div class="acts"><a class="btn primary" href="workflows.html">Go to Workflows</a></div>`); }
  else if (st.state === 'nomatch') { document.getElementById('fRepoV').textContent = 'aw-notes-api'; document.getElementById('fStateV').textContent = 'Asking you'; html = msg(`<h2>No sessions match these filters</h2><p>Change a filter above, or clear them.</p><div class="acts"><a class="btn" href="agents.html">Clear filters</a></div>`); }
  else {
    const launched = live.filter((s) => s.launchId), outside = live.filter((s) => !s.launchId);
    html = HEAD + (launched.length ? group('Started by Sindri', launched, launched.length) : '') + (outside.length ? group('Not part of a workflow', outside, outside.length) : '');
    if (!live.length) html = msg('<h2>No sessions match</h2><p>Nothing matches your search.</p>');
    html += `<button class="fold" id="fold" aria-expanded="${st.ended}">${P.chev}<span>Recently ended (${ENDED.length})</span></button>`;
    if (st.ended) html += ENDED.map(endedRow).join('');
  }
  panel.innerHTML = html;
  document.getElementById('foot').classList.toggle('hidden', ['reading', 'unavailable', 'unreadable'].includes(st.state));
  const h = document.getElementById('hd'); if (h) h.textContent = head;
  panel.querySelectorAll('.agrow').forEach((b) => b.onclick = () => { st.sel = b.dataset.ref; render(); });
  const f = document.getElementById('fold'); if (f) f.onclick = () => { st.ended = !st.ended; render(); };
  sheet();
}

function field(l, v, mono) { return v == null || v === '' ? '' : `<dt>${l}</dt><dd class="${mono ? 'mono' : ''}">${v}</dd>`; }
function sheet() {
  const el = document.getElementById('insp');
  const all = [...S, ...ENDED.map((e) => ({ ...e.s, _e: e }))];
  const s = all.find((x) => x.ref === st.sel);
  if (!s || ['reading', 'unavailable', 'unreadable', 'empty'].includes(st.state)) { el.classList.remove('open'); el.innerHTML = ''; return; }
  const e = s._e, h = s.host;
  const stBadge = e ? '<span class="badge s-idle">' + P.done + 'Ended</span>' : badge(s.state);
  const hist = (s.history || []).slice().reverse();
  el.classList.add('open');
  el.innerHTML = `<div class="chrome"><span class="sp"></span><button class="ib" id="x" aria-label="Close (Esc)">${P.close}</button></div>
  <div class="scroll"><div class="hd"><div class="row1">${stBadge}<span class="muted">${esc(s.providerLabel)}</span></div>
    <h2>${s.title ? esc(s.title) : 'No title'}</h2>
    <div class="meta"><span>${e ? 'Ended ' + ago(e.endedAt) + ' ago' : (s.state === 'asking' ? 'Asking you for ' : 'In this status for ') + ago(s.since)}</span><span class="dotsep">·</span><span class="copy" data-copy="${s.ref}">${P.copy}<span>${s.ref}</span></span></div>
    <div class="acts">${s.launch ? `<a class="btn primary" href="run-canvas.html?run=${s.launch.instance.split('/').slice(0, 3).join('/')}">${P.open}Open the run</a>` : ''}
      <button class="btn" disabled style="opacity:.6;cursor:not-allowed" title="Coming soon" data-gap="focus is not built (focus.why = not-built)">${P.term}Watch the agent <span class="faint">Coming soon</span></button></div>
    <div class="meta" style="margin-top:10px"><span class="faint">Until it is built:</span><span class="copy" data-copy="sindri agents focus ${s.ref}">${P.copy}<span>sindri agents focus ${s.ref.split('/')[0]}/…</span></span></div></div>
  ${s.state === 'asking' && !e ? `<div class="callout need"><h3>${P.ask}It is asking you</h3><p>${esc(s.asking.reason === 'approval' ? 'It wants approval to run' : s.asking.reason)} ${esc(s.asking.tool || '')}. Answer it in its own terminal; Sindri only shows that it is waiting.</p></div>` : ''}
  ${e && !e.resume ? `<div class="callout"><h3>${P.info}This session has ended</h3><p>There is no command to pick it up again.</p></div>` : ''}
  ${e && e.resume ? `<div class="callout"><h3>${P.info}This session has ended</h3><p>Pick it up again with:</p><div class="acts"><span class="copy" data-copy="${esc(e.resume.join(' '))}">${P.copy}<span>${esc(e.resume.join(' '))}</span></span></div></div>` : ''}
  <div class="cap">Where it is working</div><dl class="kv">
    ${field('Repository', s.repo ? esc(s.repo) : null)}${field('Working directory', s.cwd ? esc(s.cwd) : null, 1)}${field('Worktree', s.worktree ? esc(s.worktree) : null, 1)}
    ${field('Lane', s.lane ? esc(s.lane.team + '/' + s.lane.lane) : null)}${field('Agent session', s.launchId ? esc(s.launchId) : null, 1)}
    ${field('Run', s.launch ? `<a href="run-canvas.html?run=${s.launch.instance.split('/').slice(0, 3).join('/')}" style="text-decoration:underline">${esc(s.launch.instance)}</a>` : null, 1)}
    ${field('Step', s.launch ? esc(s.launch.state) : null)}${field('Attempt', s.launch ? s.launch.attempt : null)}</dl>
  <div class="cap">Timing</div><dl class="kv">
    ${field('Started', s.startedAt ? utc(s.startedAt) : null)}${field('In this status since', utc(s.since))}${field('How this was detected', s.stateSource + (s.estimated ? ' (estimated)' : ''))}</dl>
  <div class="cap">Host</div><dl class="kv">
    ${field('Host', esc(h.label))}${field('Terminal', h.terminal)}${field('Socket kind', h.socketKind)}${field('Tmux session', h.session)}${field('Pane', h.pane, 1)}${field('Socket', h.socket, 1)}${field('Attached clients', h.attachedClients)}${field('Process', s.pid, 1)}</dl>
  <div class="cap">History <span class="n">${hist.length ? hist.length + ' changes' : ''}</span></div>
  ${hist.length ? hist.map((x, i) => `<div class="stg ${i === 0 ? 'cur' : ''}"><div class="sh">${badge(x[0])}<div class="sn">${x[1] ? esc(x[1]) : '<span class="muted">no reason given</span>'}<small>${utc(x[2])} · ${esc(x[3])}</small></div><span class="sd">${i === 0 && !e ? 'now' : ago(x[2]) + ' ago'}</span></div></div>`).join('') : '<p class="faint" style="padding:12px 16px;margin:0;font-size:12px">No history yet.</p>'}
  <p class="foot">${P.info}<span>Read from the process list and the agents' own records. The window and the allowance an agent has used are not served yet.</span></p></div>`;
  document.getElementById('x').onclick = () => { st.sel = null; render(); };
}
document.getElementById('stsel').onchange = (e) => { st.state = e.target.value; render(); };
document.getElementById('q').oninput = (e) => { st.q = e.target.value.toLowerCase(); render(); };
document.addEventListener('keydown', (e) => {
  if (e.metaKey || e.ctrlKey || e.altKey || e.target.tagName === 'INPUT' || e.target.tagName === 'SELECT') return;
  const rows = [...document.querySelectorAll('.agrow')]; const i = rows.findIndex((r) => r.dataset.ref === st.sel);
  if (e.key === 'j' || e.key === 'ArrowDown') { st.sel = (rows[Math.min(i + 1, rows.length - 1)] || {}).dataset?.ref; render(); }
  else if (e.key === 'k' || e.key === 'ArrowUp') { st.sel = (rows[Math.max(i - 1, 0)] || {}).dataset?.ref; render(); }
  else if (e.key === 'Escape') { st.sel = null; render(); }
  else if (e.key === '/') { e.preventDefault(); document.getElementById('q').focus(); }
});
if (st.state === 'populated' && !st.sel && !Q.has('closed')) st.sel = S[1].ref;
if (Q.get('ended')) st.ended = true;
render();
"""

def build():
    body = BODY.replace("__WARN__", "<svg class='ico' viewBox='0 0 24 24'><path d='M12 3l10 18H2z'/><path d='M12 10v5M12 18h.01'/></svg>").replace("__INFO__", "<svg class='ico' viewBox='0 0 24 24'><circle cx='12' cy='12' r='9'/><path d='M12 8v5M12 16h.01'/></svg>")
    top = TOP + '<span class="muted" id="hd" style="font-size:13px"></span>'
    return page("Agents", "Agents", top, BAR, body, SCRIPT, aside=ASIDE, css=".pcq{container-type:inline-size}@container (max-width:640px){.agrow,.chead{grid-template-columns:112px minmax(100px,1fr) 56px !important}.agrow>:not(:nth-child(1)):not(:nth-child(2)):not(.tm),.chead>:not(:nth-child(1)):not(:nth-child(2)):last-child{display:none}.chead>:nth-child(3),.chead>:nth-child(4),.chead>:nth-child(5){display:none}}.foot{padding:12px 16px 24px;color:var(--faint);font-size:12px;line-height:18px;display:flex;gap:8px;align-items:center;margin:0}.hidden{display:none !important}")
