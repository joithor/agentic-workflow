import json
from gap_common import page, ROOT

_d = json.load(open(ROOT.parent / "data" / "sample.json"))
NAMES = {
    "workflow.line": ("Idea to merged pull request", "The whole pipeline: scope, design, plan, then build and prove each phase."),
    "workflow.scoping": ("Scoping", "Decide what will change, from a brief or a ticket."),
    "workflow.idea-to-brief": ("Idea to brief", "Turn a rough idea typed in the app into a cited brief."),
    "workflow.design": ("Design", "Turn the locked scope into one reviewed spec."),
    "workflow.plan": ("Plan", "Split the design into buildable phases and check a builder could do them."),
    "workflow.phase": ("Build and prove", "Build, prove, review and ship one phase as one pull request."),
    "workflow.build": ("Build", "One test-first builder for one phase, in its own worktree."),
    "workflow.prove": ("Prove", "Prove every acceptance item of a phase on its final commit."),
    "workflow.ship": ("Ship", "Check the change is green, then open the pull request."),
    "workflow.review-loop": ("Review loop", "Review a phase's change one angle at a time, round by round."),
    "workflow.pr-babysitter": ("Pull request babysitter", "Placeholder until the babysitter lands: one step done by a person."),
    "workflow.step": ("Plain step", "One step with a checks-and-revise loop; used for trying workflows out."),
}
STAND = {"workflow.pr-babysitter": ("unapproved", []), "workflow.review-loop": ("approved", ["child-stale"])}
R = []
for r in _d["recipes"]:
    n, d = NAMES[r["id"]]
    s, stale = STAND.get(r["id"], ("approved", []))
    R.append({"id": r["id"], "name": n, "does": d, "version": r["version"], "standing": s, "stale": stale, "via": "desk", "urgency": r["urgency"],
              "hash": "sha256:" + (r.get("hash") or "").replace("sha256:", "")[:64] if r.get("hash") else "sha256:" + (abs(hash(r["id"])) % 10**16).__format__("016x") + "0" * 48,
              "inputs": [i["label"] for i in r["inputs"]], "outputs": [o["label"] for o in r["outputs"]],
              "states": [{"id": x["id"], "kind": x["kind"], "runtime": x.get("runtime"), "human": x.get("humanOnly", False), "order": x.get("order", 0)} for x in r["nodes"]],
              "loops": [{"name": k, "cap": v["cap"]} for k, v in (r.get("loops") or {}).items()]})
# a stable, readable fake hash per recipe (the sample has none)
import hashlib
for x in R:
    x["hash"] = "sha256:" + hashlib.sha256(x["id"].encode()).hexdigest()

BAR = """    <div class="bar">
      <div class="seg" role="group" aria-label="Show" id="filters">
        <button data-f="all" aria-pressed="true">All <span class="n" id="n-all"></span></button>
        <button data-f="look" aria-pressed="false">Needs approval <span class="n" id="n-look"></span></button>
      </div>
      <span class="sp"></span>
      <span class="hint"><kbd>J</kbd><kbd>K</kbd> move <kbd>Enter</kbd> open <kbd>Esc</kbd> close</span>
      <label class="stsw" data-gap="demo of an outline">View as
        <select id="stsel" aria-label="Show this state (prototype only)">
          <option value="populated">Workflows</option>
          <option value="reading">Reading</option>
          <option value="empty">No workflows</option>
          <option value="nomatch">Filter matches nothing</option>
          <option value="stale">Out of date</option>
          <option value="unreadable">Can't read the answer</option>
        </select></label>
      <a class="btn primary" href="setup.html#approveall" data-gap="approve all: hold button, lives in Settings">__CHECK__Approve new workflows</a>
    </div>"""
TOP = '<label class="search"><svg class="ico" viewBox="0 0 24 24"><circle cx="11" cy="11" r="7"/><path d="M21 21l-4.3-4.3"/></svg><input id="q" type="search" placeholder="Search workflows" aria-label="Search workflows" autocomplete="off"><kbd>/</kbd></label><span class="muted" id="hd" style="font-size:13px"></span>'
BODY = """        <div id="list"></div>"""
ASIDE = '        <aside class="insp" id="insp" aria-label="Workflow details"></aside>'
CSS = """.rcols{grid-template-columns:minmax(200px,2fr) 70px 170px 80px 90px 100px}
@container (max-width:780px){.rcols{grid-template-columns:minmax(150px,1fr) 170px 100px}.rcols>:nth-child(2),.rcols>:nth-child(4),.rcols>:nth-child(5){display:none}}
.pcq{container-type:inline-size}
.band{display:flex;flex-wrap:wrap;gap:6px;padding:12px 16px}
.node{display:inline-flex;align-items:center;gap:6px;height:26px;padding:0 8px;border:1px solid var(--panel-border);border-radius:6px;background:var(--panel-card);font:500 12px/16px var(--font)}
.node .ico{width:12px;height:12px;color:var(--muted-fg)}
.node.end{color:var(--faint)}
.lst{margin:0;padding:8px 16px 12px 32px;font-size:13px;line-height:20px}
.lst.none{list-style:none;padding-left:16px;color:var(--faint)}"""

SCRIPT = "const R = " + json.dumps(R) + r""";
/* Data: GET /sindri/workflow/recipes (golden workflow-recipes/list.json: id, version, hash, standing, via, stale[], inputs, urgency, outputs, states[{id, order, branchOf}], loops[{name, from, to}]).
   The states and their kinds come from sample.json (the real recipes); plain names, the standing of two rows and the hashes are mockup-authored. */
const STAND = { approved: ['done', 'Approved'], unapproved: ['need', 'Not approved'], revoked: ['failed', 'Revoked'] };
const STALE = { 'pin-drift': 'A step it uses changed since it was approved', 'child-stale': 'A workflow it starts is out of date', 'legacy-shape': 'It is written in an old format' };
const KIND = { agent: P.term, block: I('<rect x="4" y="4" width="16" height="16" rx="3"/>'), human: P.person, wait: P.clock, terminal: P.done };
const st = { state: Q.get('state') || 'populated', filter: Q.get('filter') || 'all', q: '', sel: Q.get('open') };
const stal = (x) => x.stale.length ? '<span class="faint" title="' + esc(x.stale.map((s) => STALE[s]).join('; ')) + '"> · out of date</span>' : '';
function row(x) {
  const s = STAND[x.standing];
  return `<button class="crow cols rcols rrow" data-id="${x.id}" aria-selected="${st.sel === x.id}">
    <span style="font-weight:500;display:flex;gap:8px;align-items:center;min-width:0"><span style="overflow:hidden;text-overflow:ellipsis">${esc(x.name)}</span><span class="mono faint" style="font-weight:400">${x.id.replace('workflow.', '')}</span></span>
    <span class="muted tnum">v${x.version}</span><span>${badge(s[0], '', s[1])}${stal(x)}</span><span class="muted tnum">${x.states.length} steps</span>
    <span class="muted">${x.inputs.length} in · ${x.outputs.length} out</span><span class="mono faint">${x.hash.slice(7, 19)}</span></button>`;
}
function msg(t, p) { return `<div class="empty-st" style="padding:56px 16px"><div class="box"><div class="tile">${I('<rect x="4" y="3" width="16" height="18" rx="2"/><path d="M8 8h8M8 12h8M8 16h5"/>')}</div><h2>${t}</h2><p>${p}</p></div></div>`; }
function render() {
  document.getElementById('stsel').value = st.state;
  document.querySelectorAll('#filters button').forEach((b) => b.setAttribute('aria-pressed', b.dataset.f === st.filter));
  const look = (x) => x.standing !== 'approved' || x.stale.length;
  document.getElementById('n-all').textContent = R.length; document.getElementById('n-look').textContent = R.filter(look).length;
  const vis = R.filter((x) => (st.filter === 'all' || look(x)) && (!st.q || (x.name + x.id).toLowerCase().includes(st.q)));
  document.getElementById('hd').textContent = ['reading', 'empty', 'unreadable'].includes(st.state) ? '' : R.length + ' workflows · ' + R.filter((x) => x.standing === 'approved').length + ' approved';
  let html = '';
  if (st.state === 'stale') html += `<div class="note-line warn">${P.warn}<span>Showing the last answer. Sindri has not answered for 30 seconds.</span></div>`;
  if (st.state === 'reading') html = `<div class="panel"><div class="ph">Reading workflows…</div>${Array.from({ length: 6 }, () => '<div class="skel"><i style="width:' + (30 + Math.random() * 40 | 0) + '%"></i></div>').join('')}</div>`;
  else if (st.state === 'empty') html = msg('No workflows are installed', 'Sindri has no workflow templates yet. They are installed with Sindri itself.');
  else if (st.state === 'unreadable') html = msg("This app can't read the workflow list", 'The answer is in a shape this version of the app does not know. Update the app.');
  else if (!vis.length || st.state === 'nomatch') html += msg('No workflows match', 'Nothing matches your search. Clear it to see all of them.');
  else html += `<div class="panel pcq"><div class="cols chead rcols"><span>Workflow</span><span>Version</span><span>Standing</span><span>Size</span><span data-gap="usage stats (runs, success rate, time) are API P12, not served">Needs and produces</span><span>Fingerprint</span></div>${vis.map(row).join('')}</div>
    <p class="foot">${P.info}<span>Only approved workflows can start a run. Runs, success rate and average time per workflow are not served yet.</span></p>`;
  document.getElementById('list').innerHTML = html;
  document.querySelectorAll('.rrow').forEach((b) => b.onclick = () => { st.sel = b.dataset.id; render(); });
  sheet();
}
function sheet() {
  const el = document.getElementById('insp'), x = R.find((r) => r.id === st.sel);
  if (!x || ['reading', 'empty', 'unreadable'].includes(st.state)) { el.classList.remove('open'); el.innerHTML = ''; return; }
  el.classList.add('open');
  const s = STAND[x.standing];
  el.innerHTML = `<div class="chrome"><span class="sp"></span><button class="ib" id="x" aria-label="Close (Esc)">${P.close}</button></div>
  <div class="scroll"><div class="hd"><div class="row1">${badge(s[0], '', s[1])}<span class="muted">v${x.version}</span></div>
    <h2>${esc(x.name)}</h2><div class="meta"><span>${esc(x.does)}</span></div>
    <div class="meta"><span class="copy" data-copy="${x.id}">${P.copy}<span>${x.id}</span></span></div>
    ${x.standing === 'unapproved' ? `<div class="acts"><span class="copy" data-copy="sindri recipe approve ${x.id}">${P.copy}<span>sindri recipe approve ${x.id}</span></span></div>` : ''}</div>
  ${x.standing === 'unapproved' ? `<div class="callout need"><h3>${P.clock}Not approved</h3><p>This workflow cannot start a run until you approve it. Approving takes a typed confirmation, in the terminal for now.</p></div>` : ''}
  ${x.stale.map((t) => `<div class="callout need"><h3>${P.warn}Out of date</h3><p>${STALE[t]}. Approve it again once you have looked at what changed.</p></div>`).join('')}
  <div class="cap">How it works <span class="n">${x.states.length} steps</span></div>
  <div class="band" data-gap="plain step names are not served">${x.states.map((n) => `<span class="node ${n.kind === 'terminal' ? 'end' : ''}" title="${n.kind}${n.runtime ? ', ' + n.runtime : ''}">${KIND[n.human ? 'human' : n.kind === 'terminal' ? 'terminal' : n.kind === 'wait' ? 'wait' : (n.runtime === 'agent' ? 'agent' : 'block')]}${esc(n.id)}</span>`).join('')}</div>
  ${x.loops.length ? `<ul class="lst">${x.loops.map((l) => `<li>${esc(l.name)}: up to ${l.cap} rounds</li>`).join('')}</ul>` : ''}
  <div class="cap">What it needs <span class="n">${x.inputs.length}</span></div><ul class="lst">${x.inputs.map((i) => '<li>' + esc(i) + '</li>').join('')}</ul>
  <div class="cap">What it produces <span class="n">${x.outputs.length}</span></div>${x.outputs.length ? '<ul class="lst">' + x.outputs.map((i) => '<li>' + esc(i) + '</li>').join('') + '</ul>' : '<ul class="lst none"><li>This workflow does not produce anything.</li></ul>'}
  <div class="cap">Details</div><dl class="kv"><dt>Id</dt><dd class="mono">${x.id}</dd><dt>Version</dt><dd>${x.version}</dd><dt>Fingerprint</dt><dd class="mono">${x.hash.slice(7, 19)}</dd><dt>Approved through</dt><dd>${x.via === 'desk' ? 'you, at your desk' : x.via}</dd><dt>Used for</dt><dd>${x.urgency}</dd></dl>
  </div>`;
  document.getElementById('x').onclick = () => { st.sel = null; render(); };
}
document.getElementById('stsel').onchange = (e) => { st.state = e.target.value; render(); };
document.getElementById('q').oninput = (e) => { st.q = e.target.value.toLowerCase(); render(); };
document.getElementById('filters').onclick = (e) => { const b = e.target.closest('button'); if (b) { st.filter = b.dataset.f; render(); } };
document.addEventListener('keydown', (e) => {
  if (['INPUT', 'SELECT'].includes(e.target.tagName) || e.metaKey || e.ctrlKey) return;
  const rows = [...document.querySelectorAll('.rrow')]; const i = rows.findIndex((r) => r.dataset.id === st.sel);
  if (e.key === 'j') { st.sel = (rows[Math.min(i + 1, rows.length - 1)] || {}).dataset?.id; render(); }
  else if (e.key === 'k') { st.sel = (rows[Math.max(i - 1, 0)] || {}).dataset?.id; render(); }
  else if (e.key === 'Escape') { st.sel = null; render(); } else if (e.key === '/') { e.preventDefault(); document.getElementById('q').focus(); }
});
if (!st.sel && st.state === 'populated' && !Q.has('closed')) st.sel = 'workflow.plan';
render();
"""

def build():
    chk = "<svg class='ico' viewBox='0 0 24 24'><path d='M5 12.5l4.5 4.5L19 7'/></svg>"
    return page("Recipes", "Recipes", TOP, BAR.replace("__CHECK__", chk), BODY, SCRIPT, aside=ASIDE, css=CSS, h1="Workflows")
