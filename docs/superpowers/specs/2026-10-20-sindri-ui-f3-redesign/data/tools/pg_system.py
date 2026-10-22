from gap_common import page

BAR = """    <div class="bar">
      <div class="seg" role="group" aria-label="System" id="tabs">
        <button data-t="overview" aria-pressed="true">Overview</button>
        <button data-t="services" aria-pressed="false">Services <span class="n" id="n-svc"></span></button>
      </div>
      <a class="btn" href="jobs.html" style="height:28px">__JOBS__Background jobs</a>
      <span class="sp"></span>
      <label class="stsw" data-gap="demo of an outline">View as
        <select id="stsel" aria-label="Show this state (prototype only)">
          <option value="healthy">Everything is running</option>
          <option value="degraded">A service failed</option>
          <option value="profile">Profile needs approval</option>
          <option value="rebuilding">Rebuilding views</option>
          <option value="stalled">Views stalled</option>
          <option value="stale">Stopped answering</option>
          <option value="down">Not running</option>
          <option value="mismatch">Different versions</option>
          <option value="unreadable">Can't read the answer</option>
          <option value="connecting">Connecting</option>
        </select></label>
    </div>"""

BODY = """        <div id="sys"></div>"""

CSS = """.cards{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:16px;margin:16px}
@media (max-width:1100px){.cards{grid-template-columns:1fr}}
.card2{border:1px solid var(--border);border-radius:8px;background:var(--card-warm);overflow:hidden}
.card2 .ph{display:flex;align-items:center;gap:8px;height:40px;padding:0 16px;border-bottom:1px solid var(--border);font:600 12px/16px var(--font);letter-spacing:.06em;text-transform:uppercase;color:var(--faint)}
.card2 .ph .n{margin-left:auto;text-transform:none;letter-spacing:0;font-weight:500}
.card2 .kv{grid-template-columns:130px 1fr}
.headline{display:flex;align-items:center;gap:12px;margin:16px 16px 0;padding:14px 16px;border:1px solid var(--border);border-radius:8px;background:var(--card-warm)}
.headline h2{font:600 16px/22px var(--font);margin:0;flex:1}
.headline.warn{border-color:var(--wait-bd);background:var(--wait-bg)}
.headline.bad{border-color:var(--fail-bd);background:var(--fail-bg)}
.prob{display:flex;gap:10px;align-items:flex-start;padding:10px 16px;border-bottom:1px solid var(--border);font-size:13px}
.prob:last-child{border-bottom:0}
.scols{grid-template-columns:minmax(150px,1.3fr) 70px 120px minmax(160px,1.6fr) 80px 240px}
.sact{display:flex;gap:6px;justify-content:flex-end}
.sact .btn{height:26px;padding:0 8px;font-size:12px}
@container (max-width:900px){.scols{grid-template-columns:minmax(130px,1fr) 120px 240px}.scols>:nth-child(2),.scols>:nth-child(4),.scols>:nth-child(5){display:none}}
.pcq{container-type:inline-size}"""

SCRIPT = r"""
/* Data: GET /sindri/status (goldens status/*), GET /sindri/services (goldens services/*) and the command catalog (commands/catalog.json: 49 ready, 14 terminal only, 4 pending).
   The profile hashes, pid and times are mockup-authored; service names are real, the "what it does" words are plain-word proposals. */
const STATUS_OK = { running: true, pid: 4242, epoch: 3, leaseExpiresAt: '2026-10-19T16:41:00Z', apiVersion: 1, url: '127.0.0.1:7480',
  profile: { approved: '70e1581b3ecaf617318482f6b0844b67f79a287a7136b4e4b6ff5d26005e4261', live: '70e1581b3ecaf617318482f6b0844b67f79a287a7136b4e4b6ff5d26005e4261', source: '~/.agentic-workflow/sindri-app/profile/profile.yaml', needsApproval: false, changedSinceApproval: false },
  views: { cursor: 214, ledgerHead: 214, projectedAt: '2026-10-19T16:39:55Z', rebuilding: false, stalled: false },
  health: { word: 'healthy', problems: [] }, sessions: { live: 5, asking: 1, problems: 0 }, lastEvent: 'workflow.instance-changed · 16:39 · #214' };
const SERVICES = [
  { name: 'workflow-events', does: 'Reads workflow events and keeps runs moving', every: '5s', paused: false, lastRun: { outcome: 'ok', ranAt: '2026-10-19T16:39:55Z' }, nextSlot: '2026-10-19T16:40:00Z', job: null },
  { name: 'heartbeat', does: 'Tells Sindri it is alive', every: '5s', paused: false, lastRun: { outcome: 'ok', ranAt: '2026-10-19T16:39:55Z' }, nextSlot: '2026-10-19T16:40:00Z', job: null },
  { name: 'context-map-refresh', does: 'Keeps the map of the product current', every: '1m', paused: false, lastRun: { outcome: 'ok', ranAt: '2026-10-19T16:39:10Z' }, nextSlot: '2026-10-19T16:40:10Z', job: 'JOB-01KCM3' },
  { name: 'context-sync-gaps', does: 'Looks for sources that fell behind', every: '1m', paused: false, lastRun: { outcome: 'ok', ranAt: '2026-10-19T16:39:30Z' }, nextSlot: '2026-10-19T16:40:30Z', job: null },
  { name: 'setup', does: 'Checks the setup list and moves it on', every: '1m', paused: false, lastRun: { outcome: 'ok', ranAt: '2026-10-19T16:39:20Z' }, nextSlot: '2026-10-19T16:40:20Z', job: null },
  { name: 'pr-babysitter', does: 'Watches open pull requests for CI and comments', every: '2m', paused: true, lastRun: { outcome: 'ok', ranAt: '2026-10-19T14:02:00Z' }, nextSlot: null, job: null },
];
const VARIANTS = {
  healthy: {}, connecting: { connecting: true },
  degraded: { word: 'degraded', problems: [{ code: 'SND-SCHED-001', text: 'Service heartbeat failed at 2026-10-19T16:38:00Z', clause: 'service heartbeat failed' }], svcFail: 'heartbeat' },
  profile: { profile: true }, rebuilding: { rebuilding: true }, stalled: { stalled: true }, stale: { stale: true }, down: { down: true }, mismatch: { mismatch: true }, unreadable: { unreadable: true },
};
const st = { state: Q.get('state') || 'healthy', tab: Q.get('tab') || 'overview', confirm: null };
const short = (h) => h ? h.replace('sha256:', '').slice(0, 12) : 'none';
function kv(l, v, mono) { return `<dt>${l}</dt><dd class="${mono ? 'mono' : ''}">${v}</dd>`; }
function msg(title, text, acts, bad) { return `<div class="empty-st" style="padding:64px 16px"><div class="box"><div class="tile">${bad ? P.fail : P.warn}</div><h2>${title}</h2><p>${text}</p>${acts ? '<div class="acts">' + acts + '</div>' : ''}</div></div>`; }
const cp = (c) => `<span class="copy" data-copy="${c}">${P.copy}<span>${c}</span></span>`;

function overview(v) {
  const s = JSON.parse(JSON.stringify(STATUS_OK));
  if (v.profile) { s.profile.live = 'sha256:9c1f0a47be22d6c8a15e03f4b7d19a60c2e58f713b04d96a1c7e5f8d20b43e61'; s.profile.needsApproval = true; s.profile.changedSinceApproval = true; }
  if (v.problems) { s.health = { word: v.word, problems: v.problems }; }
  if (v.rebuilding) { s.views.rebuilding = true; s.views.cursor = 120; s.health.word = 'rebuilding'; }
  if (v.stalled) { s.views.stalled = true; s.views.cursor = 212; s.views.ledgerHead = 214; s.health = { word: 'stalled', problems: [{ code: 'SND-VIEW-001', text: 'Views are stalled at 212: run `sindri views rebuild`', clause: 'views stalled' }] }; }
  let sentence = 'Everything is running.', tone = '', b = badge('ok', '', 'Healthy');
  if (v.problems) { sentence = 'Something needs a look: ' + v.problems[0].clause + '.'; tone = 'warn'; b = badge('degraded', '', 'Degraded'); }
  if (v.profile) { sentence = 'The profile needs your approval.'; tone = 'warn'; b = badge('degraded', '', 'Needs approval'); }
  if (v.rebuilding) { sentence = 'Sindri is rebuilding its views.'; tone = ''; b = badge('running', '', 'Rebuilding'); }
  if (v.stalled) { sentence = 'Something needs a look: views stalled.'; tone = 'bad'; b = badge('failed', '', 'Stalled'); }
  if (v.stale) { sentence = 'Sindri has stopped answering, so what you see may be out of date.'; tone = 'warn'; b = badge('degraded', '', 'Out of date'); }
  let html = `<div class="headline ${tone}">${b}<h2>${sentence}</h2></div>`;
  if (v.profile) html += `<div class="note-line warn" style="margin-top:8px">${P.warn}<span>Approving is done in the terminal for now: ${cp('sindri profile approve')}</span></div>`;
  if (s.health.problems.length) html += `<div class="panel" style="margin-top:12px"><div class="ph">Needs a look <span class="n">${s.health.problems.length}</span></div>${s.health.problems.map((p) => `<div class="prob">${P.warn}<div><div>${esc(p.text)}</div><div class="faint mono" style="margin-top:2px">${p.code}</div></div></div>`).join('')}</div>`;
  const p = s.profile;
  html += `<div class="cards">
    <section class="card2" aria-label="Control plane"><div class="ph">Control plane</div><dl class="kv">
      ${kv('Process', 'Running · pid ' + s.pid)}${kv('Epoch · lease', s.epoch + ' · held, expires ' + hm(s.leaseExpiresAt), 0)}${kv('API', s.url + ' · v' + s.apiVersion, 1)}
      ${kv('This app', 'API v1')}${kv('Last event', s.lastEvent, 1)}</dl></section>
    <section class="card2" aria-label="Profile"><div class="ph">Profile <span class="n">${badge(v.profile ? 'degraded' : 'done', '', v.profile ? 'Changed since approval' : 'Approved')}</span></div><dl class="kv">
      ${kv('Approved', short(p.approved), 1)}${p.live !== p.approved ? kv('Live', short(p.live), 1) : ''}${kv('File', p.source, 1)}</dl></section>
    <section class="card2" aria-label="Views"><div class="ph">Views <span class="n">${s.views.stalled ? badge('failed', '', 'Stalled') : s.views.rebuilding ? badge('running', '', 'Rebuilding') : badge('done', '', 'Up to date')}</span></div><dl class="kv">
      ${kv('Read up to', 'event ' + s.views.cursor + ' of ' + s.views.ledgerHead)}${kv('Updated', s.views.projectedAt ? agoShort(s.views.projectedAt) : 'not yet')}
      ${(s.views.stalled || s.views.rebuilding) ? kv('Fix', cp('sindri views rebuild')) : ''}</dl></section>
    <section class="card2" aria-label="Agents"><div class="ph">Agents <a class="n" href="agents.html" style="text-decoration:underline">Open Agents</a></div><dl class="kv">
      ${kv('Running', s.sessions.live + ' agents')}${kv('Asking you', s.sessions.asking)}${kv('Problems', s.sessions.problems || 'none')}</dl></section>
    <section class="card2" aria-label="Services"><div class="ph">Services <a class="n" href="system.html?tab=services" style="text-decoration:underline">${SERVICES.length} scheduled</a></div>
      ${SERVICES.slice(0, 4).map((x) => `<div class="prob" style="align-items:center"><b style="font-weight:500;flex:1">${x.name}</b><span class="muted">every ${x.every}</span><span class="faint tnum">${x.lastRun ? 'last run ' + hm(x.lastRun.ranAt) : 'not run yet'}</span></div>`).join('')}</section>
    <section class="card2" aria-label="Actions"><div class="ph">Actions</div><dl class="kv">
      ${kv('In the app', '49 actions')}${kv('In the terminal', '14 actions, kept there on purpose')}${kv('Arriving', '4 actions in later phases')}</dl>
      <div style="padding:0 16px 14px"><button class="btn" id="stop" ${st.confirm ? 'hidden' : ''}>${P.fail}Stop Sindri</button>
      ${st.confirm ? `<span class="muted" style="font-size:12px">Stop Sindri? It stops every service and run.</span> <button class="btn" id="yes">Confirm</button> <button class="btn" id="no">Keep running</button>` : ''}</div></section>
  </div>`;
  return html;
}
function services(v) {
  const rows = SERVICES.map((x) => ({ ...x, lastRun: (v.svcFail === x.name) ? { outcome: 'failed', code: 'SND-SCHED-001', ranAt: '2026-10-19T16:38:00Z' } : x.lastRun }));
  const stat = (x) => x.paused ? badge('idle', '', 'Paused') : x.job ? badge('running', '', 'Running now') : x.lastRun && x.lastRun.outcome === 'failed' ? badge('failed', '', 'Last run failed') : badge('done', '', 'Scheduled');
  return `<div class="panel pcq"><div class="cols chead scols"><span>Service</span><span>Every</span><span>Status</span><span data-gap="what each service does is not served">What it does</span><span>Last run</span><span style="text-align:right">Actions</span></div>
  ${rows.map((x) => `<div class="crow cols scols"><span style="font-weight:500">${x.name}</span><span class="muted">${x.every}</span><span>${stat(x)}</span><span class="muted">${x.does}</span>
    <span class="faint tnum">${x.lastRun ? hm(x.lastRun.ranAt) : 'never'}</span>
    <span class="sact">${x.job ? `<a class="btn" href="jobs.html?open=${x.job}">View job</a>` : ''}<button class="btn" data-run="${x.name}">${P.play}Run now</button><button class="btn" data-pause="${x.name}">${x.paused ? 'Resume' : 'Pause'}</button></span></div>`).join('')}</div>
  <p class="foot">${P.info}<span>Run now, Pause and Resume are ready actions in the command catalog. Pausing a service asks nothing; it only stops the schedule.</span></p>`;
}
function render() {
  document.getElementById('stsel').value = st.state;
  document.querySelectorAll('#tabs button').forEach((b) => b.setAttribute('aria-pressed', b.dataset.t === st.tab));
  document.getElementById('n-svc').textContent = SERVICES.length;
  const v = VARIANTS[st.state] || {}; let html;
  if (v.down) html = msg('Sindri is not running', 'Start it, then this page shows its health.', cp('sindri serve'), true);
  else if (v.connecting) html = `<div class="headline">${P.spin}<h2>Connecting…</h2></div><div class="panel"><div class="ph">Checking…</div>${Array.from({ length: 5 }, () => '<div class="skel"><i style="width:40%"></i></div>').join('')}</div>`;
  else if (v.mismatch) html = msg('Sindri and this app are different versions', 'This app speaks API v1; the running Sindri speaks API v2. Update the app, or restart Sindri from the same build.', `<div class="copy" style="margin-right:6px"><span>This app: API v1</span></div><div class="copy"><span>Sindri: API v2</span></div>`);
  else if (v.unreadable) html = msg("Sindri is running, but this app can't read its answer", 'The answer is in a shape this version of the app does not know.', '');
  else html = (v.stale ? `<div class="note-line warn">${P.warn}<span>Showing the last answer. Sindri has not answered for 30 seconds.</span></div>` : '') + (st.tab === 'services' ? services(v) : overview(v));
  document.getElementById('sys').innerHTML = html;
  document.querySelectorAll('[data-run]').forEach((b) => b.onclick = () => toast('Started a job for ' + b.dataset.run + ' (not saved: this is a prototype)'));
  document.querySelectorAll('[data-pause]').forEach((b) => b.onclick = () => toast('Done (not saved: this is a prototype)'));
  const stop = document.getElementById('stop'); if (stop) stop.onclick = () => { st.confirm = true; render(); };
  const no = document.getElementById('no'); if (no) no.onclick = () => { st.confirm = null; render(); };
  const yes = document.getElementById('yes'); if (yes) yes.onclick = () => { st.confirm = null; st.state = 'down'; render(); };
}
document.getElementById('stsel').onchange = (e) => { st.state = e.target.value; render(); };
document.getElementById('tabs').onclick = (e) => { const b = e.target.closest('button'); if (b) { st.tab = b.dataset.t; render(); } };
render();
"""

def build():
    jobs = "<svg class='ico' viewBox='0 0 24 24'><path d='M4 6h16M4 12h16M4 18h10'/></svg>"
    return page("System", "System", '', BAR.replace("__JOBS__", jobs), BODY, SCRIPT, css=CSS)
