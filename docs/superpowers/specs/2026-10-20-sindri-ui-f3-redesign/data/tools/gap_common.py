"""Shared head, rail and script for the screens added in the all-screens pass (agents, work, system, jobs, recipes, home).
The CSS is copied from screens/workflows.html, so every token and component is the prototype's own; only a few
page-specific rules are appended per page. Run build_gap_screens.py to write the pages."""
import re, pathlib

ROOT = pathlib.Path(__file__).resolve().parents[2] / "screens"
_wf = (ROOT / "workflows.html").read_text()
CSS = re.search(r"<style>(.*?)</style>", _wf, re.S).group(1)

def ico(d, cls="ico"):
    return f'<svg class="{cls}" viewBox="0 0 24 24">{d}</svg>'

NAV = [
    ("Home", "home.html", '<path d="M3 11l9-8 9 8"/><path d="M5 10v10h5v-6h4v6h5V10"/>'),
    ("Work", "work.html", '<path d="M9 6h12M9 12h12M9 18h12"/><path d="M3 6l1 1 2-2M3 12l1 1 2-2M3 18l1 1 2-2"/>'),
    ("Workflows", "workflows.html", '<rect x="3" y="3" width="7" height="6" rx="1.5"/><rect x="14" y="15" width="7" height="6" rx="1.5"/><path d="M6.5 9v3a3 3 0 003 3H14"/>'),
    ("Recipes", "recipes.html", '<rect x="4" y="3" width="16" height="18" rx="2"/><path d="M8 8h8M8 12h8M8 16h5"/>'),
    ("Context", "context.html", '<circle cx="6" cy="6" r="2.5"/><circle cx="18" cy="8" r="2.5"/><circle cx="12" cy="18" r="2.5"/><path d="M8 7l8 1M7 8l4 8M17 10.5l-4 5.5"/>'),
    ("Agents", "agents.html", '<rect x="4" y="8" width="16" height="12" rx="3"/><path d="M12 4v4M9 14h.01M15 14h.01M2 13v2M22 13v2"/>'),
    ("System", "system.html", '<path d="M3 12h4l3-8 4 16 3-8h4"/>'),
]
_SET = re.search(r'<a href="setup.html".*?</a>', _wf, re.S).group(0)
_Q = re.search(r'<a href="questions.html".*?</a>', _wf, re.S).group(0)

def rail(active):
    out = ['<nav class="rail" aria-label="Sindri pages">', '    <div class="ws" aria-hidden="true">S</div>']
    for name, href, d in NAV:
        cur = ' aria-current="page"' if name == active else ""
        out.append(f'    <a href="{href}" aria-label="{name}"{cur}>{ico(d)}<span class="tip">{name}</span></a>')
    s = _SET.replace(' aria-current="page"', "")
    if active == "Settings":
        s = s.replace('aria-label="Settings"', 'aria-label="Settings" aria-current="page"')
    out += ["    " + s, '    <div class="sp"></div>', "    " + _Q, '    <div class="me" aria-hidden="true">You</div>', "  </nav>"]
    return "\n".join(out)

EXTRA_CSS = """
/* ---------- Added in the all-screens pass (same tokens; nothing new in :root) ---------- */
.stsw{display:inline-flex;align-items:center;gap:8px;font-size:12px;color:var(--muted-fg)}
.stsw select{height:28px;border-radius:6px;border:1px solid var(--border);background:var(--field);color:var(--fg);padding:0 8px;font:500 12px var(--font)}
.sel{position:relative;display:inline-flex;align-items:center;gap:6px;height:28px;padding:0 10px;border:1px solid var(--border);border-radius:6px;background:var(--field);font:500 12px/16px var(--font);color:var(--fg);white-space:nowrap}
.sel .ico{width:14px;height:14px;color:var(--muted-fg)}
.sel .k{color:var(--faint);font-weight:400}
.panel{margin:16px;border:1px solid var(--border);border-radius:8px;background:var(--card-warm);overflow:hidden}
.panel .ph{display:flex;align-items:center;gap:8px;height:40px;padding:0 16px;border-bottom:1px solid var(--border);font:600 12px/16px var(--font);letter-spacing:.06em;text-transform:uppercase;color:var(--faint)}
.panel .ph .n{margin-left:auto;text-transform:none;letter-spacing:0;font-weight:500;font-variant-numeric:tabular-nums}
.note-line{display:flex;gap:8px;align-items:flex-start;margin:12px 16px 0;padding:8px 12px;border-radius:6px;border:1px solid var(--border);background:var(--field);color:var(--muted-fg);font-size:12px;line-height:18px}
.note-line .ico{width:14px;height:14px;margin-top:2px;flex:none}
.note-line.warn{border-color:var(--wait-bd);background:var(--wait-bg);color:var(--wait-fg)}
.note-line.bad{border-color:var(--fail-bd);background:var(--fail-bg);color:var(--fail-fg)}
.skel{height:36px;border-bottom:1px solid color-mix(in oklab,var(--border) 60%,transparent);display:grid;align-items:center;padding:0 16px}
.skel i{display:block;height:10px;border-radius:5px;background:linear-gradient(90deg,var(--muted),var(--seg),var(--muted));background-size:200% 100%;animation:sk 1.4s linear infinite}
@keyframes sk{to{background-position:-200% 0}}
@media (prefers-reduced-motion:reduce){.skel i{animation:none}}
.fold{width:100%;display:flex;align-items:center;gap:8px;height:36px;padding:0 16px;color:var(--muted-fg);font:500 13px/16px var(--font);border-top:1px solid var(--border);text-align:left}
.fold:hover{background:var(--hover)}
.fold .ico{width:14px;height:14px;transition:transform .15s}
.fold[aria-expanded=true] .ico{transform:rotate(90deg)}
.kv{display:grid;grid-template-columns:150px 1fr;gap:6px 12px;padding:12px 16px;font-size:12px;line-height:18px;margin:0}
.kv dt{color:var(--faint)}
.kv dd{margin:0;color:var(--fg);min-width:0;overflow-wrap:anywhere}
.kv dd.mono{font-family:var(--mono);font-size:12px}
.toast{position:fixed;left:50%;bottom:64px;transform:translateX(-50%);background:var(--card);border:1px solid var(--border);border-radius:8px;padding:8px 14px;font:500 12px/16px var(--font);z-index:150;display:none}
.toast.on{display:block}
.tile{width:40px;height:40px;border-radius:10px;border:1px solid var(--border);background:var(--field);display:flex;align-items:center;justify-content:center;color:var(--muted-fg);margin:0 auto 12px}
.empty-st{flex:1;display:flex;align-items:center;justify-content:center;padding:32px}
.empty-st .box{max-width:420px;text-align:center}
.empty-st h2{font:600 16px/22px var(--font);margin:0 0 6px}
.empty-st p{margin:0 0 16px;color:var(--muted-fg);font-size:13px;line-height:20px}
.empty-st .acts{justify-content:center;margin-top:0}
.meter{height:6px;border-radius:3px;background:var(--seg);overflow:hidden;min-width:80px}
.meter i{display:block;height:100%;background:var(--run-dot)}
.meter.warn i{background:var(--wait-dot)}
.cols{display:grid;column-gap:12px;align-items:center;padding:0 16px}
.crow{height:36px;border-bottom:1px solid color-mix(in oklab,var(--border) 60%,transparent);width:100%;text-align:left;transition:background-color .1s}
.crow:hover{background:var(--hover)}
.crow[aria-selected=true]{background:var(--select);box-shadow:inset 0 0 0 1px color-mix(in oklab,var(--fg) 15%,transparent)}
.crow > *{min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.chead{height:28px;border-bottom:1px solid var(--border);color:var(--faint);font:500 12px/16px var(--font);background:var(--bg)}
.muted{color:var(--muted-fg)}
.faint{color:var(--faint)}
.sec{padding:16px;border-bottom:1px solid var(--panel-border)}
.sec h3{font:600 12px/16px var(--font);letter-spacing:.06em;text-transform:uppercase;color:var(--faint);margin:0 0 8px}
.foot{padding:12px 16px 24px;color:var(--faint);font-size:12px;line-height:18px;display:flex;gap:8px;align-items:flex-start;margin:0}
.foot .ico{width:14px;height:14px;margin-top:2px}
.page{overflow:auto}
"""

JS_COMMON = r"""
const NOW = Date.parse('2026-10-19T16:40:00Z');
const I = (d) => `<svg class="ico" viewBox="0 0 24 24">${d}</svg>`;
const P = {
  spin: '<svg class="ico spin" viewBox="0 0 24 24"><circle cx="12" cy="12" r="9" opacity=".25"/><path d="M21 12a9 9 0 00-9-9"/></svg>',
  clock: I('<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>'),
  person: I('<circle cx="12" cy="8" r="4"/><path d="M4 21a8 8 0 0116 0"/>'),
  pause: I('<circle cx="12" cy="12" r="9"/><path d="M10 9v6M14 9v6"/>'),
  fail: I('<circle cx="12" cy="12" r="9"/><path d="M15 9l-6 6M9 9l6 6"/>'),
  done: I('<circle cx="12" cy="12" r="9"/><path d="M8 12.5l3 3 5-6"/>'),
  idle: I('<circle cx="12" cy="12" r="9" stroke-dasharray="3 3"/>'),
  ask: I('<path d="M21 12a8 8 0 01-11.6 7.1L4 20l1-4.5A8 8 0 1121 12z"/><path d="M9.7 9.7a2.4 2.4 0 114 1.7c-.8.5-1.2.9-1.2 1.6M12.5 16h.01"/>'),
  copy: I('<rect x="9" y="9" width="11" height="11" rx="2"/><path d="M5 15V6a2 2 0 012-2h9"/>'),
  close: I('<path d="M6 6l12 12M18 6L6 18"/>'),
  chev: I('<path d="M9 6l6 6-6 6"/>'),
  open: I('<path d="M7 17L17 7M8 7h9v9"/>'),
  info: I('<circle cx="12" cy="12" r="9"/><path d="M12 8v5M12 16h.01"/>'),
  warn: I('<path d="M12 3l10 18H2z"/><path d="M12 10v5M12 18h.01"/>'),
  term: I('<rect x="3" y="4" width="18" height="16" rx="2"/><path d="M7 9l3 3-3 3M13 15h4"/>'),
  search: I('<circle cx="11" cy="11" r="7"/><path d="M21 21l-4.3-4.3"/>'),
  plus: I('<path d="M12 5v14M5 12h14"/>'),
  play: I('<path d="M7 4l13 8-13 8z"/>'),
};
const STATUS = {
  running: { word: 'Working', cls: 's-running', g: P.spin },
  asking:  { word: 'Asking you', cls: 's-wait', g: P.ask },
  idle:    { word: 'Idle', cls: 's-idle', g: P.idle },
  need:    { word: 'Waiting for you', cls: 's-wait', g: P.clock },
  took:    { word: 'You took over', cls: 's-you', g: P.person },
  paused:  { word: 'Paused, needs you', cls: 's-wait', g: P.pause },
  failed:  { word: 'Failed', cls: 's-fail', g: P.fail },
  done:    { word: 'Done', cls: 's-done', g: P.done },
  unres:   { word: "Didn't work out", cls: 's-idle', g: P.fail },
  cancel:  { word: 'Cancelled', cls: 's-idle', g: P.pause },
  queued:  { word: 'Queued', cls: 's-idle', g: P.idle },
  ok:      { word: 'Running', cls: 's-done', g: P.done },
  degraded:{ word: 'Needs a look', cls: 's-wait', g: P.warn },
  stopped: { word: 'Stopped', cls: 's-fail', g: P.fail },
};
const badge = (k, extra = '', word) => { const s = STATUS[k]; return `<span class="badge ${s.cls}" ${extra}>${s.g}${word || s.word}</span>`; };
const mins = (iso) => Math.round((NOW - Date.parse(iso)) / 60000);
const ago = (iso) => { const m = mins(iso); if (m >= 2880) return Math.round(m / 1440) + 'd'; if (m < 1) return 'now'; return m < 60 ? m + 'm' : (Math.floor(m / 60) + 'h ' + (m % 60 ? (m % 60) + 'm' : '')).trim(); };
const agoShort = (iso) => ago(iso) === 'now' ? 'just now' : ago(iso) + ' ago';
const hm = (iso) => iso.slice(11, 16);
const utc = (iso) => iso.slice(0, 10) + ' ' + iso.slice(11, 19) + ' UTC';
const esc = (s) => String(s).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const Q = new URLSearchParams(location.search);
function toast(t) { let e = document.getElementById('toast'); if (!e) { e = document.createElement('div'); e.id = 'toast'; e.className = 'toast'; e.setAttribute('role', 'status'); document.body.appendChild(e); } e.textContent = t; e.classList.add('on'); clearTimeout(e._t); e._t = setTimeout(() => e.classList.remove('on'), 1800); }
document.addEventListener('click', (ev) => { const c = ev.target.closest('[data-copy]'); if (c) { try { navigator.clipboard.writeText(c.dataset.copy); } catch (e) {} toast('Copied'); } });
const annotBtn = document.getElementById('annot');
annotBtn.onclick = () => { const on = annotBtn.getAttribute('aria-checked') !== 'true'; annotBtn.setAttribute('aria-checked', on); document.body.classList.toggle('annot', on); };
if (Q.get('annot')) { document.body.classList.add('annot'); annotBtn.setAttribute('aria-checked', 'true'); }
"""

def page(title, active, top_extra, bar, body, script, css="", h1=None, aside=""):
    """One screen: rail + 56 px top bar + optional 48 px bar + body + script."""
    h1 = h1 or title
    return f"""<!doctype html>
<html lang="en" class="dark">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{title}</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap" rel="stylesheet">
<style>{CSS}{EXTRA_CSS}{css}</style>
</head>
<body>
<div class="app">
  {rail(active)}

  <div class="main">
    <header class="top">
      <h1>{h1}</h1>
      {top_extra}
      <span class="sp"></span>
      <span class="cmdk" title="Command palette"><kbd>⌘K</kbd> Jump to anything</span>
      <button class="switch" id="annot" role="switch" aria-checked="false"><i></i>Annotations</button>
    </header>
{bar}
    <div class="work">
      <div class="page" id="pg">
{body}
      </div>
{aside}
    </div>
  </div>
</div>
<script>
{JS_COMMON}
{script}
</script>
</body>
</html>
"""
