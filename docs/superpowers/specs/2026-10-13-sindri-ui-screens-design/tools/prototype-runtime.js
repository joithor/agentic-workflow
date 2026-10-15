'use strict';
/* ───────── helpers ───────── */
const $ = (s,r=document)=>r.querySelector(s), $$ = (s,r=document)=>[...r.querySelectorAll(s)];
const E = s=>String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const A = E; // attribute-safe
const G = (c,ch,lbl)=>'<span class="g '+c+'"'+(lbl?' role="img" aria-label="'+lbl+'"':' aria-hidden="true"')+'>'+ch+'</span>';
const I = {
  lock:'<svg width="12" height="12" viewBox="0 0 12 12" aria-hidden="true"><rect x="2" y="5.2" width="8" height="5.6" rx="1.2" stroke="currentColor" fill="none" stroke-width="1.2"/><path d="M4 5.2V3.8a2 2 0 0 1 4 0v1.4" stroke="currentColor" fill="none" stroke-width="1.2"/></svg>',
  warp:'<span class="warp" aria-hidden="true"><svg width="8" height="8" viewBox="0 0 8 8"><path d="m1.5 2 2 2-2 2M4.5 6H7" stroke="#d0d6e0" fill="none" stroke-width="1"/></svg></span>',
  chev:'<svg width="10" height="10" viewBox="0 0 10 10" aria-hidden="true"><path d="M2.5 4 5 6.5 7.5 4" stroke="currentColor" fill="none" stroke-width="1.3"/></svg>',
  copy:'<svg width="11" height="11" viewBox="0 0 12 12" aria-hidden="true"><rect x="3.5" y="3.5" width="6.5" height="6.5" rx="1.2" stroke="currentColor" fill="none" stroke-width="1.2"/><path d="M2 8V2.8C2 2.4 2.4 2 2.8 2H8" stroke="currentColor" fill="none" stroke-width="1.2"/></svg>',
  close:'<svg width="12" height="12" viewBox="0 0 12 12" aria-hidden="true"><path d="m3 3 6 6M9 3 3 9" stroke="currentColor" stroke-width="1.3"/></svg>',
  search:'<svg width="13" height="13" viewBox="0 0 14 14" aria-hidden="true"><circle cx="6" cy="6" r="4.2" stroke="currentColor" fill="none" stroke-width="1.4"/><path d="m9.2 9.2 3 3" stroke="currentColor" stroke-width="1.4"/></svg>',
  mark:'<svg width="12" height="12" viewBox="0 0 12 12"><path d="M2 9.5 6 2l4 7.5H7.4L6 6.8 4.6 9.5z" fill="currentColor"/></svg>',
  ext:'<svg width="10" height="10" viewBox="0 0 10 10" aria-hidden="true"><path d="M3 2h5v5M8 2 2.5 7.5" stroke="currentColor" fill="none" stroke-width="1.2"/></svg>',
};
const ATTACH_RULE = 'Attach joins the live session; your first keystroke takes it over.';
const DECISION_RULE = 'Accept records your decision now; the engine acts on it when the workflow runs.';
const TARGETS = [
  {id:'claude-code',label:'Claude Code',cli:'claude',why:'last used in agentic-workflow'},
  {id:'codex',label:'Codex',cli:'codex',why:'installed · 2.0.1'},
  {id:'cursor',label:'Cursor',cli:'cursor-agent',why:'not found on this host',off:true},
];
const tgtLabel = id=>(TARGETS.find(t=>t.id===id)||TARGETS[0]).label;
function rng(seed){ return ()=>{ seed|=0; seed=seed+0x6D2B79F5|0; let t=Math.imul(seed^seed>>>15,1|seed); t=t+Math.imul(t^t>>>7,61|t)^t; return ((t^t>>>14)>>>0)/4294967296; }; }

/* ───────── the fixture (fixture.md) ───────── */
/* fixture.md "State chips: one mapping of word to colour": the word and its class, nothing else */
const CHIP_TABLE={'PARKED':'attn','ESCALATED':'attn','DECISION':'attn','PROFILE':'attn','CHANGED':'attn','ORPHANED':'attn','DEGRADED':'attn','FAILED':'fail','RUNNING':'ok','OK':'ok','WITH YOU':'','QUEUED':'','DONE':'','DRAFT':'','APPROVED':'','DETERMINISTIC':'','MODEL':'','CHILD':'','POLLING':'','ABSENT':''};

function it(o){ return Object.assign({repo:'agentic-workflow',tgt:'claude-code'},o); }
function baseItems(){ return [
  it({id:'ITM-01JB4',title:'Context map review for tabs.layout',src:'Linear ENG-490',g:'esc',step:'gate.verify.judge',since:'09:10',age:'2h 56m',tgt:'codex',score:'0.62',min:'0.80'}),
  it({id:'ITM-01JD2',title:'Re-scope requested: drop the iOS head from this item',repo:'sindri-app',src:'request REQ-01JD4 from a session',g:'decision',since:'11:40',age:'26m',req:'REQ-01JD4'}),
  it({id:'ITM-01JA1',title:'Onyx adapter secret pointers',repo:'sindri-app',src:'Linear APP-212',g:'withyou',step:'produce',since:'12:01',age:'25m',agent:'Claude Code',sess:'snd-01JA1.3',tok:41,term:true,att:true,grp:'workflow.item › produce'}),
  it({id:'ITM-01JC8',title:'Retry storm in outbox dispatcher',src:'Linear ENG-503',g:'park',reason:'no-progress',why:'checks failed twice with the same findings',step:'step.2 · round 3/3',since:'11:54',age:'12m'}),
  it({id:'ITM-01JE7',title:'Add needs-you ack route',src:'Linear ENG-511',g:'running',step:'produce',round:'2/3',agent:'Claude Code in Warp',sess:'snd-01JE7.2',tok:84,term:true,since:'12:20',age:'6m',grp:'workflow.item › produce'}),
  it({id:'ITM-01JE9',title:'Tester: decision race integration test',src:'Linear ENG-514',g:'running',step:'checks',round:'1/1',agent:'Codex',sess:'snd-01JE9.1',tok:12,term:false,since:'12:24',age:'2m',tgt:'codex',grp:'workflow.item › checks'}),
  it({id:'ITM-01JD9',title:'Judge outbox drain on shutdown',src:'Linear ENG-508',g:'running',step:'produce',round:'1/3',agent:'Claude Code in Warp',sess:'snd-01JD9.1',tok:22,term:true,since:'12:25',age:'1m',grp:'workflow.item › produce'}),
  it({id:'ITM-01JD6',title:'Services page job panel',repo:'sindri-app',src:'packet task t4 of PRJ-sync (ws-2)',g:'running',step:'checks',agent:'factory worker',sess:'snd-01JD6.1',tok:3,term:false,since:'12:26',age:'0m',grp:'workflow.item › checks'}),
  it({id:'ITM-01JF0',title:'Stream limits 429 fallback',src:'triage sentry #8809',g:'queued',age:'1h'}),
  it({id:'ITM-01JF3',title:'doctor views line',repo:'sindri-app',src:'packet task t6 of PRJ-sync (ws-3)',g:'queued',age:'52m'}),
  it({id:'ITM-01JF4',title:'Tray badge on Linux',repo:'sindri-app',src:'packet task t7 of PRJ-sync (ws-3)',g:'queued',age:'40m'}),
  it({id:'ITM-01JF8',title:'Receipt verify route',repo:'sindri-app',src:'packet task t8 of PRJ-sync (ws-4)',g:'queued',age:'12m'}),
]; }
const EXTRA_TITLES=['Budget line rounding in doctor','Ledger compaction stalls on WAL','Recipe drift check for tabs.layout','Judge health probe timeout','Scope map recall below baseline','Profile watcher misses symlinks','Outbox retry jitter','Intake dedupe by Linear id','Tray menu row order','Warp tab title escaping','Seed template audit','Lane budget report','Context map version pin','Request text scrubber length','Decision race on reconnect','Readiness check remote mode','Packet freeze hash mismatch','Bugfix state lock busy'];
function largeItems(base){
  const r=rng(8), out=base.slice(), reasons=['needs-answer','rounds','budget','no-progress','timeout'];
  let n=0; const nid=()=>'ITM-01K'+String.fromCharCode(65+Math.floor(n/10))+(n++%10);
  const mk=(g,o)=>{ const t=EXTRA_TITLES[(out.length+base.length)%EXTRA_TITLES.length]; out.push(it(Object.assign({id:nid(),title:EXTRA_TITLES[out.length-12],repo:r()<.5?'agentic-workflow':'sindri-app',src:'Linear ENG-'+(520+out.length),g,since:'11:'+String(10+Math.floor(r()*40)),age:Math.floor(r()*50+5)+'m'},o))); };
  ['needs-answer','rounds','budget'].forEach(re=>mk('park',{reason:re,why:re==='needs-answer'?'the agent asked a question':re==='budget'?'token budget reached at 200k':'round cap reached',step:'produce',round:'3/3'}));
  mk('esc',{step:'gate.verify.judge',score:'0.55',min:'0.80'}); mk('esc',{step:'gate.verify.judge',score:'0.71',min:'0.80'});
  mk('decision',{req:'REQ-01K'+(n)});mk('decision',{req:'REQ-01K'+(n+1)});
  for(let k=0;k<5;k++) mk('running',{step:k%2?'produce':'checks',round:'1/3',agent:k%2?'Claude Code in Warp':'Codex',sess:'snd-01K'+k+'.1',tok:10+k*9,term:!!(k%2),grp:k%2?'workflow.item › produce':'workflow.item › checks',tgt:k%2?'claude-code':'codex',age:(k+1)+'m'});
  for(let k=0;k<4;k++) mk('queued',{age:(15+k*9)+'m'});
  return out;
}
const WF_TEXT={ // path of states per recipe
  'workflow.project':['intake','studio','scoping','design','build','verify','ship'],
  'triage.alert':['received','classify','route','done'],
  'incident.response':['detected','triage','mitigate ⇄ verify','comms','postmortem','closed'],
};
function baseRoots(){ return [
  {a:'INC-14',rec:'incident.response',hash:'5d90e1b2',v:4,subj:'Outbox retry storm',inp:'P-2211',st:'mitigate',badge:'r1/3',loop:true,age:'1h 24m',since:'11:02',now:'agent rolling back in <span class="mono">ITM-01JG2</span> · comms posting',cells:['d','d','p r1/3','p child','t','t'],glyph:'⇄',stageTxt:'mitigate r1/3'},
  {a:'sentry #8812',rec:'triage.alert',hash:'d41f2a07',v:2,subj:'TypeError in tabs.layout',short:'sentry #8812 TypeError',inp:'payload',st:'classify',badge:'desk',age:'4m',since:'12:22',now:'needs your severity',cells:['d','p desk','t','t'],attn:true,desk:true},
  {a:'sentry #8809',rec:'triage.alert',subj:'429s on stream',short:'sentry #8809 429s',inp:'payload',st:'done',age:'12m',since:'11:58',now:'done 12m',cells:['d','d','d','d →'],done:true,note:'ITM-01JF0 queued'},
  {a:'P-2211',rec:'triage.alert',subj:'PagerDuty P-2211',short:'pagerduty P-2211',inp:'payload',st:'escalated',age:'1h 24m',since:'11:02',now:'accept at a terminal',cells:['d','esc'],attn:true,esc:true,note:'accept at a terminal; INC-14 opened'},
  {a:'sentry #8801',rec:'triage.alert',subj:'sentry #8801',inp:'payload',st:'done',age:'3h',since:'09:20',cells:['d','d','d','d'],done:true,more:true},
  {a:'sentry #8797',rec:'triage.alert',subj:'sentry #8797',inp:'payload',st:'done',age:'5h',since:'07:30',cells:['d','d','d','d'],done:true,more:true},
  {a:'sentry #8790',rec:'triage.alert',subj:'sentry #8790',inp:'payload',st:'done',age:'7h',since:'05:44',cells:['d','d','d','d'],done:true,more:true},
  {a:'PRJ-sync',rec:'workflow.project',hash:'a8c21e7f',v:12,team:'APP',subj:'Offline sync',inp:'PRD-sync.md',st:'build',badge:'4/9',age:'3h 12m',since:'09:14',now:'<span class="mono">ITM-01JD6</span> checks · 4 done · 1 running · 1 parked · 3 queued',nowShort:'<span class="mono">ITM-01JD6</span> checks · 1 parked',cells:['d','d','d','d','p 4/9','t','t'],pg:'build 4/9'},
  {a:'PRJ-exports',rec:'workflow.project',hash:'a8c21e7f',v:12,team:'ENG',subj:'Billing exports',inp:'PRD-exports.md',st:'design',badge:'desk',age:'20h',since:'10-11 16:02',now:'with you: import the design output',cells:['d','d','d','p desk','t','t','t'],you:true},
  {a:'PRJ-tabs',rec:'workflow.project',hash:'a8c21e7f',v:12,team:'ENG',subj:'Tab manager v2',inp:'PRD-tabs.md',st:'scoping',badge:'r2/3',age:'46m',since:'11:40',now:'challenger round 2 · <span class="mono">84k / 600k</span>',cells:['d','d','p r2/3','t','t','t','t']},
  {a:'LIN-ENG',rec:'intake.linear',subj:'Linear ENG',inp:'',st:'polling',age:'—',now:'polling every 15m',cells:[],intake:true},
  {a:'LIN-APP',rec:'intake.linear',subj:'Linear APP',inp:'',st:'polling',age:'—',now:'last 12:15 · 2 new items',cells:[],intake:true},
]; }
function largeRoots(base){
  const out=base.slice(), r=rng(21);
  const subs=['Exports retry policy','Search index v3','Inbox zero digest','Billing proration','Mobile share sheet','Audit log export'];
  for(let k=0;k<4;k++){ const s=subs[k]; out.splice(10+k,0,{a:'PRJ-x'+(k+1),rec:'workflow.project',hash:'a8c21e7f',v:12,team:k%2?'APP':'ENG',subj:s,inp:'PRD-x'+(k+1)+'.md',st:'scoping',badge:'r1/3',age:(k+1)+'h',since:'0'+(8-k)+':10',now:'challenger round 1 · <span class="mono">'+(20+k*11)+'k / 600k</span>',cells:['d','d','p r1/3','t','t','t','t']}); }
  for(let k=0;k<4;k++) out.splice(7+k,0,{a:'sentry #88'+(13+k),rec:'triage.alert',subj:'sentry #88'+(13+k),short:'sentry #88'+(13+k)+' Timeout',inp:'payload',st:'classify',badge:'desk',age:(k+5)+'m',since:'12:1'+k,now:'needs your severity',cells:['d','p desk','t','t'],attn:true,desk:true});
  return out;
}
/* fixture.md "Recipes: the numbers (30 days, one source for Recipes and the inspector)". runs = done + failed + cancelled;
   dwy = done without you; int = interventions [parks answered, escalations decided, takeovers]; wk = runs per week, oldest first;
   hw = interventions per week (only workflow.item has the table's weekly figures; the others are spread by their weekly runs). */
const RECIPES=[
 {n:'workflow.project',st:'approved',open:3,runs:9,done:5,dwy:3,fail:1,canc:3,int:[4,1,1],med:'2d 4h',tok:'1.9M',wk:[1,2,3,3],last:['10-06','v12'],hash:'a8c21e7f',states:['intake','studio','scoping','design','build','verify','ship']},
 {n:'workflow.item',st:'approved',open:6,runs:112,done:97,dwy:81,fail:6,canc:9,int:[16,11,7],after:[12,4],med:'1h 40m',tok:'310k',wk:[22,27,30,33],hw:[5,8,9,12],last:['10-02','v7'],hash:'3f1c09a2',states:['intake','produce','checks','gate.verify.judge','review','done']},
 {n:'workflow.step',st:'approved',open:6,child:true,last:['10-02','v4'],hash:'7b20ce11',states:['start','run','done']},
 {n:'builder.feature',st:'approved',open:9,runs:38,done:34,dwy:29,fail:2,canc:2,int:[5,3,2],med:'2h 05m',tok:'420k',wk:[6,9,11,12],last:['10-08','v3'],hash:'0c9d41ab',states:['plan','children','verify','done']},
 {n:'triage.alert',st:'approved',open:3,runs:24,done:22,dwy:14,fail:0,canc:2,int:[8,2,0],med:'6m',tok:'18k',wk:[5,6,6,7],last:['09-28','v2'],hash:'d41f2a07',states:['received','classify','route','done']},
 {n:'incident.response',st:'approved',open:1,runs:3,done:2,dwy:0,fail:1,canc:0,int:[2,0,1],med:'3h 10m',tok:'190k',wk:[1,1,0,1],last:['09-30','v1'],hash:'5d90e1b2',states:['detected','triage','mitigate','verify','comms','postmortem','closed']},
 {n:'scoping.project',st:'changed',open:1,runs:7,done:6,dwy:5,fail:0,canc:1,int:[2,0,0],med:'48m',tok:'540k',wk:[0,2,2,3],last:['today','v3'],hash:'9e51d803',states:['draft','challenge','accept']},
 {n:'intake.linear',st:'draft',open:0,last:['never approved'],hash:'e0b3a7c4',states:['poll','emit']},
];
const SERVICES_NOTE='';
function baseFixture(large){
  const items=baseItems(); const its=large?largeItems(items):items;
  const roots=large?largeRoots(baseRoots()):baseRoots();
  const props=[
   {id:'p1',file:'src/session/restore.ts',key:'feature:session.restore',conf:'0.74',reason:'no intent cites it; the file calls Manager.restoreLayout',impact:'2 nodes · 1 file'},
  ];
  const names=['src/layout/Resize.ts','src/tabs/Strip.tsx','src/layout/dock.ts','src/tabs/Reorder.ts','src/session/persist.ts','src/panes/Focus.ts','src/panes/Zoom.ts','src/tabs/Pin.ts','src/layout/snap.ts','src/session/lock.ts','src/tabs/Group.ts','src/panes/Swap.ts','src/layout/grid.ts','src/tabs/Close.ts'];
  const keys=['feature:layout.split-pane.resize','surface:screen/tab-strip','feature:layout.dock','action:tab-manager.reorder','feature:session.persist','feature:panes.focus','feature:panes.zoom','action:tab-manager.pin','feature:layout.snap','feature:session.lock','action:tab-manager.group','action:panes.swap','feature:layout.grid','action:tab-manager.close'];
  names.forEach((f,i)=>props.push({id:'p'+(i+2),file:f,key:keys[i],conf:(0.71-i*0.03).toFixed(2),reason:'the file is referenced from '+keys[i].split(':')[1].split('.')[0]+' tests',impact:(2+i%3)+' nodes · 1 file'}));
  return {
    items:its, roots,
    svc:{index:{state:'failed',code:'SND-INDEX-012',why:'graphify timed out',since:'03:02',age:'9h'}},
    profile:{changed:true,keys:['services.index.every','context.maxBudget'],since:'10:15',age:'2h 11m'},
    recipes:RECIPES.map(r=>Object.assign({},r)),
    ctx:{det:31,detAccepted:false,props,orphans:[{id:'ENG-301',title:'Per-workspace pane restore',to:'feature:workspace.restore',since:'v12'},{id:'ENG-288',title:'Pane restore on reconnect',to:'feature:session.restore-pane',since:'v12'},{id:'ENG-263',title:'Tab drag between windows',to:'feature:tabs.drag-window',since:'v11'}],anchor:'prod-anchor',sugg:'pending'},
    requests:[],
    budget:{claude:3.10,codex:1.02,cursor:0},
  };
}

/* ───────── state ───────── */
const PAGES=['home','workflows','work','recipes','context','agents','system'];
const PAGE_LABEL={home:'Home',workflows:'Workflows',work:'Work',recipes:'Recipes',context:'Context',agents:'Agents',system:'System'};
const S = {route:'home', sheet:null, stack:[], sheetW:760, last:null, conn:'normal', rm:matchMedia('(prefers-reduced-motion: reduce)').matches, ts:1, large:false,
  menu:null, pop:null, dialog:null, palette:false, seen:new Set(), revealed:{}, launch:{}, attached:{}, confirm:null, copied:null, toast:null,
  job:null, evals:null, lens:{intent:1,product:1,files:1,draft:0}, filesForm:'graph', ctxNode:'prod-anchor', collapsedGroups:{}, nyAll:false, wfAll:{}, recipeVer:null, recipeWin:'30 days',
  open:{}, note:{}, forms:{}, rebuildPct:28, jobs:{}, wkFilter:'', req:[], more:{}, boot:true};
function load(large){
  const f=baseFixture(large); Object.assign(S,f);
  S.seen=new Set(); S.revealed={}; S.launch={}; S.attached={}; S.confirm=null; S.copied=null; S.toast=null; S.job=null; S.evals=null;
  S.lens={intent:1,product:1,files:1,draft:0}; S.filesForm='graph'; S.ctxNode='prod-anchor'; S.menu=null; S.pop=null; S.dialog=null; S.stack=[]; S.sheet=null;
  S.forms={}; S.req=[]; S.recipeVer=null; S.nyAll=false; S.wfAll={}; S.more={}; S.jobs={}; S.note={}; S.open={}; S.sheetTab='timeline';
  S.sync={onyx:false,slack:false}; S.depth=2; S.tl={}; S.sev={}; S.sysSel='ITM-01JE9'; S.jd7ok=false; S.anchorText=null; S.fmNoFeat=false; S.fmNoCnt=false; S.fmOnlyUnmapped=false; S.undoSeen=null; S.sheetReturn=null; S.wkFilter=''; S.recipeWin='30 days';
}
load(false);
const T0=Date.now();
function live(m){ const l=$('#live'); l.textContent=''; setTimeout(()=>{ l.textContent=m; },30); }
const byId=id=>S.items.find(x=>x.id===id);
const rootBy=a=>S.roots.find(x=>x.a===a);
const recBy=n=>S.recipes.find(x=>x.n===n);
const openItems=()=>S.items.filter(x=>x.g!=='cancelled'&&x.g!=='done');
const inG=g=>S.items.filter(x=>x.g===g);
function needs(){
  const L=[];
  inG('park').forEach(x=>L.push({key:x.id,kind:'park',it:x}));
  inG('decision').forEach(x=>L.push({key:x.req,kind:'decision',it:x}));
  inG('esc').forEach(x=>L.push({key:x.id,kind:'esc',it:x}));
  if(S.svc.index.state!=='ok') L.push({key:'svc:index',kind:'fail'});
  if(S.profile.changed) L.push({key:'profile',kind:'profile'});
  return L;
}
const isPulse=n=>n.kind==='park'||n.kind==='decision'||n.kind==='esc';
const unseen=()=>needs().filter(n=>isPulse(n)&&!S.seen.has(n.key));
const unseenAll=()=>needs().filter(n=>!S.seen.has(n.key));
const nyCap=()=>S.ts>=1.3?4:5;
const liveSessions=()=>S.items.filter(x=>x.g==='running'&&x.sess);
const nOpenRoots=()=>S.roots.length;
const nYouRoots=()=>S.roots.filter(r=>r.you&&!r.done).length;
/* fixture.md "The human queue": the count is the attention-chip rows on the page (a project's parked child is a child, not a root row) */
function wfNeeds(){ return S.roots.filter(r=>r.attn&&!r.done).length; }
function healthParts(){
  const parts=[]; if(S.svc.index.state!=='ok') parts.push('index failed'); if(S.profile.changed) parts.push('profile changed');
  const word = (parts.length||true)?'Degraded':'Healthy';
  return {word,parts};
}
function stampAge(){ return S.conn==='stale'?'updated 47s ago':'updated 2s ago'; }
function titleSync(){ const n=needs().length, u=unseen().length; document.title = (u&&S.conn==='normal') ? '('+n+') Needs you · Sindri' : 'Sindri'; }

/* ───────── pieces shared by the pages ───────── */
function kbd(k){ return '<kbd>'+k+'</kbd>'; }
function cmdLine(cli,key){
  const c=S.copied===key;
  return '<button class="cmd" data-act="copy" data-cli="'+A(cli)+'" data-x="'+A(key)+'" aria-label="Copy command: '+A(cli)+'" title="Copy to clipboard"><span class="prompt" aria-hidden="true">$</span><code>'+E(cli)+'</code><span class="cp"'+(c?' style="color:var(--success)"':'')+'>'+(c?'Copied':I.copy)+'</span></button>';
}
function lockBtn(label,cli,key,sm){
  return '<button class="btn term'+(sm?' sm':'')+'" data-act="lock" data-cli="'+A(cli)+'" data-x="'+A(key)+'" title="Needs a typed confirmation at a terminal">'+I.lock+E(label)+'</button>';
}
function openSplit(x,sm,primary){
  const id=x.id, tg=x.tgt||'claude-code', m=S.menu==='open:'+id;
  const lbl='Open in '+tgtLabel(tg);
  let menu='';
  if(m){
    menu='<div class="menu" role="menu" aria-label="More for '+id+'"><div class="cap">Open in</div>'+TARGETS.map(t=>t.off?
      '<button class="mi" role="menuitem" aria-disabled="true" disabled data-x="'+t.id+'">'+t.label+'<small>'+t.why+'</small></button>':
      '<button class="mi" role="menuitem" data-act="open" data-id="'+id+'" data-x="'+t.id+'">'+t.label+(t.id===tg?' <small>current · '+t.why+'</small>':'<small>'+t.why+'</small>')+'</button>').join('')+
      '<div class="cap">Destructive</div><button class="mi" role="menuitem" data-act="ask-cancel" data-id="'+id+'">Cancel '+id+'…<small>Asks first: "Cancel '+id+'? The item closes. [Cancel item] [Keep]"</small></button></div>';
  }
  return '<div class="split"><button class="btn main'+(sm?' sm':'')+(primary?' primary':'')+'" data-act="open" data-id="'+id+'" aria-label="'+lbl+' '+id+'">'+I.warp+lbl+'</button><button class="btn chev'+(sm?' sm':'')+(primary?' primary':'')+'" data-act="menu" data-id="open:'+id+'" aria-label="More for '+id+': other agents, cancel" aria-haspopup="menu" aria-expanded="'+m+'">'+I.chev+'</button>'+menu+'</div>';
}
function attachSplit(x,sm,opts){
  opts=opts||{}; const id=x.id, m=S.menu==='att:'+id, att=S.attached[id];
  let menu='';
  if(m) menu='<div class="menu" role="menu" aria-label="Choose agent for '+id+'"><div class="cap">Open another in</div>'+TARGETS.filter(t=>!t.off).map(t=>'<button class="mi" role="menuitem" data-act="open" data-id="'+id+'" data-x="'+t.id+'">'+t.label+'<small>'+t.why+'</small></button>').join('')+'</div>';
  if(att) return '<span class="px-att" role="status">'+G('g-live','●')+'Attached in Warp</span>';
  return '<div class="split"><button class="btn main'+(sm?' sm':'')+(opts.primary?' primary':'')+'" data-act="attach" data-id="'+id+'" aria-label="Attach '+id+'">'+I.warp+'Attach'+(opts.kbd?' '+kbd('O'):'')+'</button><button class="btn chev'+(sm?' sm':'')+(opts.primary?' primary':'')+'" data-act="menu" data-id="att:'+id+'" aria-label="Choose agent for '+id+'" aria-haspopup="menu" aria-expanded="'+m+'">'+I.chev+'</button>'+menu+'</div>';
}
function sentLine(x){ const l=S.launch[x.id]; return l?'<div class="px-sent" role="status">'+G('g-live','●')+'Sent to Warp · '+tgtLabel(l.tgt)+' · tab <span class="mono">sindri '+x.id+'</span> · just now</div>':''; }
function confirmRow(key,html,yes,no){ return '<div class="px-confirm" role="alertdialog" aria-label="'+A(key)+'"><span>'+html+'</span><span class="acts"><button class="btn sm danger" data-act="confirm-yes" data-x="'+A(key)+'">'+yes+'</button><button class="btn sm" data-act="confirm-no" data-x="'+A(key)+'" data-first>'+no+'</button></span></div>'; }
const chipFor={park:['g-park','▌','park'],decision:['','?','decision'],esc:['g-esc','▲','escalated'],fail:['h-fail','✕','failure'],profile:['g-chg','◆','profile']};

/* ───────── chrome: top bar, band, banners, shared states ───────── */
function wfCount(){ return S.roots.length; }
function navLink(r,l,c){ return '<a href="#'+r+'" data-act="go" data-id="'+r+'"'+(S.route===r?' aria-current="page"':'')+'>'+l+(c!==''&&c!=null?' <span class="count">'+c+'</span>':'')+'</a>'; }
function pill(){
  const c=S.conn;
  if(c==='notrunning') return '<span class="health"><span class="word" style="color:var(--error)"><span class="g h-abs" aria-hidden="true">○</span>Not running</span></span>';
  if(c==='connecting') return '<span class="health"><span class="word" style="color:var(--ink-muted)">Connecting…</span></span>';
  if(c==='mismatch') return '<span class="health"><span class="word" style="color:var(--error)"><span class="g h-fail" aria-hidden="true">✕</span>Version mismatch</span></span>';
  const hp=healthParts(); let word=hp.word, gl='◐', age=stampAge(), cls='h-deg';
  if(c==='stalled'){ word='Stalled'; gl='✕'; cls='h-fail'; age='views stale'; }
  if(c==='rebuilding'){ word='Rebuilding'; gl='◐'; age='views rebuilding'; }
  if(c==='stale'){ word='Stale'; gl='◐'; }
  const parts=hp.parts.map(p=>'<span class="sep" aria-hidden="true">·</span><span>'+p+'</span>').join('');
  return '<button class="health" data-act="go" data-id="system" aria-label="Health: '+word+', '+age+(hp.parts.length?', '+hp.parts.join(', '):'')+'"><span class="word"'+(c==='stalled'?' style="color:var(--error)"':c==='rebuilding'||c==='stale'?' style="color:var(--ink-muted)"':'')+'><span class="g '+cls+'" aria-hidden="true">'+gl+'</span>'+word+'</span><span class="sep" aria-hidden="true">·</span><span class="age">'+age+'</span>'+parts+
    (c==='stale'?'<span class="sep" aria-hidden="true">·</span><span class="btn sm" role="button" tabindex="0" data-act="refresh">Refresh</span>':'')+'</button>';
}
function topbar(){
  const off=['notrunning','connecting','mismatch'].includes(S.conn);
  const nav=S.conn==='mismatch'?'':'<nav class="seg" aria-label="Primary">'+navLink('home','Home','')+navLink('workflows','Workflows',wfCount())+navLink('work','Work',openItems().length)+navLink('recipes','Recipes',S.recipes.length)+navLink('context','Context','')+navLink('agents','Agents',liveSessions().length)+navLink('system','System','')+'</nav>';
  return '<header class="topbar"><div class="brand"><span class="brand-mark" aria-hidden="true">'+I.mark+'</span>Sindri</div>'+nav+
    (S.conn==='mismatch'?'':'<button class="search" data-act="palette" aria-label="Search or run a command (Command K)">'+I.search+'<span>Search or run a command</span><kbd>⌘K</kbd></button>')+pill()+'</header>';
}
function banner(){
  const c=S.conn;
  if(c==='stalled') return '<div class="px-banner err" role="alert"><span class="w"><span class="g h-fail" aria-hidden="true">✕</span>Stalled</span><span class="t">Views are stalled at 4211: run <code>sindri views rebuild</code></span><span class="aside">Rows below are marked stale</span><button class="btn primary" data-act="rebuild">Rebuild views</button></div>';
  if(c==='rebuilding'){ const cur=Math.round(4211*S.rebuildPct/100); return '<div class="px-banner" role="status"><span style="display:flex;flex-direction:column;flex:1;gap:4px"><span><span class="g" aria-hidden="true">⟳</span> Rebuilding views (cursor '+cur+' of 4211)…</span><span class="px-prog"><i style="width:'+S.rebuildPct+'%"></i></span></span><span class="aside">rows come back as they are rebuilt · actions that read views wait (SND-API-010)</span></div>'; }
  if(c==='stale') return '<div class="px-banner" role="status"><span class="w"><span class="g" aria-hidden="true">⌛</span>Stale</span><span class="t">No event for more than 30 s. The numbers below are the last ones received.</span><span class="aside">updated 47s ago</span><button class="btn" data-act="refresh">Refresh</button></div>';
  return '';
}
function screenNotRunning(){
  return '<main class="page"><div class="px-center"><div class="px-card" role="alert"><h2><span class="g h-abs" aria-hidden="true">○</span> Not running</h2><p>Nothing answered at <span class="mono">127.0.0.1:47811</span> <span class="mono" style="color:var(--ink-subtle)">(SND-UI-002)</span></p><div style="margin-top:12px">'+cmdLine('sindri','nr')+'</div><p class="help">The app looks again every 2 s. It never starts Sindri itself.</p></div></div></main>';
}
function screenMismatch(){
  return '<main class="page"><div class="px-center"><div class="px-card" role="alert"><h2>Sindri and the app are different versions</h2><div class="px-vers"><span class="k">This app</span><span class="v">API v1</span><span class="k">Sindri</span><span class="v">API v2</span></div><p style="margin-top:12px">Fix: quit both and run <span class="mono" style="color:var(--ink)">sindri</span>.</p><div style="margin-top:8px">'+cmdLine('sindri','vm')+'</div><p class="help">No views and no actions until the versions match.</p></div></div></main>';
}
function screenConnecting(){
  let rows=''; for(let i=0;i<5;i++) rows+='<div class="px-skrow"><span class="sk" style="width:'+(70+i*9)+'px"></span><span class="sk" style="width:'+(70-i*6)+'%"></span><span class="sk"></span></div>';
  return '<main class="page" aria-busy="true"><header class="pagehead"><div class="ph-l"><h1 class="page-title">'+PAGE_LABEL[S.route]+'</h1><span class="sk" style="width:220px"></span></div></header><section class="band"><div class="section"><div class="sh"><h2>Loading</h2><span class="meta">Connecting… loading the views</span></div>'+rows+'</div></section></main>';
}
const wrapV=(cls,html)=>'<div class="'+cls+'">'+html+'</div>';

/* ───────── calm-pass UI pieces shared by every page (chips, ▾ menus, rows) ───────── */
const chipH=(label)=>{ const k=String(label).toUpperCase(); if(!(k in CHIP_TABLE)) console.error('chip word not in the fixture table: '+label); const c=CHIP_TABLE[k]; return '<span class="chip'+(c?' '+c:'')+'">'+label+'</span>'; };
const shortAge=a=>String(a||'').split(' ')[0];
function chevBtn(mid,label,o){
  o=o||{}; const open=S.menu===mid;
  return '<button class="btn '+(o.solid?'':'ghost ')+'chev'+(o.sm?' sm':'')+'" data-act="menu" data-id="'+A(mid)+'" aria-label="'+A(label)+'" aria-haspopup="menu" aria-expanded="'+open+'">'+I.chev+'</button>';
}
const menuBox=(mid,label,inner)=>S.menu===mid?'<div class="menu" role="menu" aria-label="'+A(label)+'">'+inner+'</div>':'';
function mwrap(mid,label,inner,o){ return '<span class="mwrap">'+chevBtn(mid,label,o)+menuBox(mid,label,inner)+'</span>'; }
/* a labelled dropdown button ("Attach ▾", "Open ▾") with its menu, as the lr rows draw it */
function dropBtn(mid,text,aria,inner,o){ o=o||{}; const open=S.menu===mid; return '<span class="mwrap"><button class="btn sm'+(o.cls?' '+o.cls:'')+'" data-act="menu" data-id="'+A(mid)+'" aria-label="'+A(aria)+'" aria-haspopup="menu" aria-expanded="'+open+'">'+text+' '+I.chev+'</button>'+menuBox(mid,aria,inner)+'</span>'; }
const mCap=t=>'<div class="cap">'+t+'</div>';
const mItem=(label,act,id,x,small,kb)=>'<button class="mi" role="menuitem" data-act="'+act+'" data-id="'+A(id||'')+'"'+(x!=null?' data-x="'+A(x)+'"':'')+'>'+label+(kb?' '+kbd(kb):'')+(small?'<small>'+small+'</small>':'')+'</button>';
const mCopy=(cli,key)=>'<button class="mi" role="menuitem" data-act="copy" data-cli="'+A(cli)+'" data-x="'+A(key)+'"><span class="mono">$ '+E(cli)+'</span><small>Copy command</small></button>';
function mOpenIn(x){ // launch targets that exist on this host, the item's own first (cursor is hidden: not found)
  const ts=TARGETS.filter(t=>!t.off).sort((a,b)=>(b.id===(x.tgt||'claude-code'))-(a.id===(x.tgt||'claude-code')));
  return ts.map(t=>mItem('Open in '+t.label,'open',x.id,t.id)).join('');
}
const mCancel=(id,txt)=>mCap('Destructive')+mItem('Cancel '+id+'…','ask-cancel',id,null,txt||'Asks first: "Cancel '+id+'? The item closes. [Cancel item] [Keep]"');
const noteWaits='<div class="px-note">Waits for the terminal: nothing changes here until you run it there.</div>';
/* a full-width line under a row (revealed command, sent-to-Warp line, job output, confirm) */
const rx=h=>h?'<div class="rx">'+h+'</div>':'';
/* a calm row: chip · title (+id, tag) · meta · when · action */
function calmRow(o){ // {cls,row,aria,chip,t,m,w,a,extra,attrs}
  return '<div class="row '+o.cls+'"'+(o.row?' data-row="'+A(o.row)+'" tabindex="0"':'')+' aria-label="'+A(o.aria||'')+'">'+o.chip+'<div class="t">'+o.t+'</div>'+(o.m!=null?'<span class="m">'+o.m+'</span>':'')+(o.w!=null?'<span class="w">'+o.w+'</span>':'')+'<div class="a">'+(o.a||'')+'</div>'+(o.extra||'')+'</div>';
}
/* repo tag only when the row's repo differs from the section's majority */
function majority(list){ const c={}; list.forEach(x=>{ c[x.repo]=(c[x.repo]||0)+1; }); const k=Object.keys(c).sort((a,b)=>c[b]-c[a]); return k.length>1&&c[k[0]]===c[k[1]]?'*':k[0]; }
const tagH=(x,maj)=>x.repo&&maj!=='*'&&x.repo!==maj?'<span class="tag">'+x.repo+'</span>':'';
/* section and band wrappers */
const sectionH=(id,title,meta,more,inner)=>'<div class="section"'+(id?' aria-labelledby="'+id+'"':'')+'><div class="sh"><h2'+(id?' id="'+id+'"':'')+'>'+title+'</h2>'+(meta||'')+(more||'')+'</div>'+inner+'</div>';
const bandH=(cls,label,inner)=>'<section class="band'+(cls?' '+cls:'')+'" aria-label="'+A(label)+'">'+inner+'</section>';
const moreLink=(txt,route,aria)=>'<a class="more" href="#'+route+'" data-act="go" data-id="'+route+'" aria-label="'+A(aria||txt)+'">'+txt+'</a>';

/* ───────── Home (the calm pass: bands, chips, one action per row) ───────── */
function decText(x){ return x.id==='ITM-01JD2'?'Re-scope ITM-01JD2: drop the iOS head from this item':'Re-scope '+x.id+': '+x.title; }
function jobLines(key){ const j=S.jobs[key]; if(!j) return ''; return '<div class="px-job" role="log" aria-label="Job output">'+j.lines.map(l=>'<div>'+E(l)+'</div>').join('')+(j.done?'':'<div class="blink">…</div>')+'</div>'; }
function cancelConfirmH(key,id,txt){ return S.confirm===key?confirmRow(key,'<b>Cancel '+id+'?</b> '+txt,'Cancel item','Keep'):''; }
function nyRow(n,maj){
  const x=n.it, key='ny:'+n.key, rv=key;
  const reveal=S.revealed[key]?noteWaits:'';
  let chip,t,m,w,a,aria,menu,extra='';
  if(n.kind==='park'){
    const rs=x.reason, cli=rs==='budget'?'sindri workflow answer '+x.id+' --grant-tokens 100000':'sindri workflow answer '+x.id+' --grant 1';
    aria='Needs you: parked, '+x.id; chip=chipH('Parked','attn');
    t='<span class="id at-rest mono">'+x.id+'</span><span class="row-title">'+E(x.title)+'</span>'+tagH(x,maj); m=E(x.why||''); w=shortAge(x.age);
    const first=(rs==='no-progress'||rs==='rounds')?lockBtn('Grant rounds · terminal',cli,rv):rs==='budget'?lockBtn('Grant tokens · terminal',cli,rv):rs==='needs-answer'?'<button class="btn primary" data-act="open-sheet" data-id="'+x.id+'" data-x="answer">Answer</button>':'<button class="btn primary" data-act="resume" data-id="'+x.id+'">Resume</button>';
    const copy=(rs==='needs-answer'||rs==='timeout')?'':mCap('Copy command')+mCopy(cli,rv);
    menu=mwrap('ny:'+n.key,'More for '+x.id,mCap('Open in')+mOpenIn(x)+copy+mCancel(x.id,'Asks first: "Cancel '+x.id+'? The parked round is discarded. [Cancel item] [Keep]"'));
    a=first+menu;
    extra=rx((S.revealed[key]&&!(rs==='needs-answer'||rs==='timeout')?cmdLine(cli,rv)+reveal:'')+(S.launch[x.id]?sentLine(x):'')+cancelConfirmH('cancel:'+x.id,x.id,'The parked round is discarded.'));
  } else if(n.kind==='decision'){
    aria='Needs you: decision on '+x.id; chip=chipH('Decision','attn');
    t='<span class="id at-rest mono">'+x.req+'</span><span class="row-title">'+E(x.title)+'</span>'+tagH(x,maj); m='filed by a session'; w=shortAge(x.age);
    menu=mwrap('ny:'+n.key,'More for '+x.id,mCap(DECISION_RULE)+mOpenIn(x)+mCancel(x.id,'Asks first: "Cancel '+x.id+'? The item closes. [Cancel item] [Keep]"'));
    a='<button class="btn primary" data-act="decide" data-id="'+x.id+'" data-x="accept" title="'+A(DECISION_RULE)+'">Accept</button><button class="btn" data-act="decide" data-id="'+x.id+'" data-x="decline">Decline</button>'+menu;
    extra=rx((S.launch[x.id]?sentLine(x):'')+cancelConfirmH('cancel:'+x.id,x.id,'The item closes and its session stops.'));
  } else if(n.kind==='esc'){
    const cli='sindri workflow answer '+x.id+' --accept';
    aria='Needs you: escalated gate, '+x.id; chip=chipH('Escalated','attn');
    t='<span class="id at-rest mono">'+x.id+'</span><span class="row-title">'+E(x.title)+'</span>'+tagH(x,maj); m='judge '+x.score+', below '+x.min; w=shortAge(x.age);
    menu=mwrap('ny:'+n.key,'More for '+x.id,mItem('Reject','reject',x.id)+mCap('Open in')+mOpenIn(x)+mCap('Copy command')+mCopy(cli,rv)+mCancel(x.id,'Asks first: "Cancel '+x.id+'? The item closes. [Cancel item] [Keep]"'));
    a=lockBtn('Accept · terminal',cli,rv)+menu;
    extra=rx((S.revealed[key]?cmdLine(cli,rv)+reveal:'')+(S.launch[x.id]?sentLine(x):'')+cancelConfirmH('cancel:'+x.id,x.id,'The item closes and its session stops.'));
  } else if(n.kind==='fail'){
    const jr=S.jobs.index&&!S.jobs.index.done;
    aria='Needs you: failed, service index'; chip=chipH('Failed','fail');
    t='<span class="row-title">Nightly index build</span><span class="id at-rest mono">SND-INDEX-012</span>'; m='graphify timed out'; w='9h';
    menu=mwrap('ny:svc:index','More for the index service',mCap('Copy command')+mCopy('sindri services run index','svcrun'));
    a='<span class="pending">Available with API P6</span><button class="btn" disabled title="Available with API P6 · sindri services run index">Run again</button>'+menu;
    extra=rx(S.jobs.index?jobLines('index'):'');
  } else {
    const cli='sindri profile approve 3f1c09';
    aria='Needs you: profile changed'; chip=chipH('Profile','attn');
    t='<span class="row-title">Profile changed since approval · '+S.profile.keys.length+' keys</span>'; m='<span class="mono">'+S.profile.keys.join(', ')+'</span>'; w='2h';
    menu=mwrap('ny:profile','More for the profile',mCap('Copy command')+mCopy(cli,rv));
    a=lockBtn('Approve · terminal',cli,rv)+menu;
    extra=rx(S.revealed[key]?cmdLine(cli,rv)+reveal:'');
  }
  return calmRow({cls:'ny',row:key,aria,chip,t,m,w,a,extra});
}
function needsBand(){
  const L=needs(), cap=nyCap(), show=S.nyAll?L:L.slice(0,cap), u=S.undoSeen;
  const maj=majority(L.filter(n=>n.it).map(n=>n.it));
  const tint=L.length>0&&unseenAll().length>0;
  let rows=show.map(n=>nyRow(n,maj)).join('');
  if(!L.length) rows='<div class="row" style="display:block"><span class="m">Nothing needs you. Last cleared 11:12.</span></div>';
  const all=L.length>cap?'<button class="px-link more" data-act="ny-all">'+(S.nyAll?'fewer':'all '+L.length+' →')+'</button>':'';
  const seenBtn=u?'<span class="meta" role="status">'+u+' marked seen · <button class="px-link" data-act="undo-seen">Undo</button></span>':'<button class="btn ghost sm more" data-act="seen-all" aria-label="Mark all seen">Mark all seen</button>';
  return '<section class="band'+(tint?' attn':'')+'" aria-labelledby="ny-h"><div class="section"><div class="sh"><h2 id="ny-h">Needs you</h2><span class="shr">'+all+seenBtn+'</span></div><div class="rows">'+rows+'</div></div></section>';
}
function wfStripRows(){
  const rank=r=>r.rec==='incident.response'?0:r.rec==='triage.alert'?1:2;
  return S.roots.filter(r=>!r.done&&!r.intake&&!r.esc).map((r,i)=>({r,i})).sort((a,b)=>rank(a.r)-rank(b.r)||a.i-b.i).map(x=>x.r);
}
/* the slim segmented bar: one segment per recipe state, the current one named beside it */
function wfStep(r){
  const cols=WF_COLS[r.rec]||[]; const n=cols.length||r.cells.length||1;
  if(r.esc) return {n,cur:1,done:false};
  if(r.done) return {n,cur:n-1,done:true};
  let cur=r.cells.findIndex(c=>c[0]!=='d'); if(cur<0) cur=r.cells.length-1; return {n,cur:Math.min(cur,n-1),done:false};
}
function barH(r){
  const s=wfStep(r), segs=[]; for(let i=0;i<s.n;i++) segs.push('<i class="'+(i<s.cur?'on':i===s.cur?'cur':'')+'"></i>');
  const nm=r.esc?'classify':r.st;
  return '<span class="bar'+(r.attn||r.esc?' attn':'')+'" role="img" aria-label="'+A(nm)+', step '+(s.cur+1)+' of '+s.n+'">'+segs.join('')+'</span>';
}
function stageName(r,full){
  if(r.esc) return 'classify';
  const b=r.badge; let tail='';
  if(r.rec==='incident.response') tail=full?' <span class="b">⇄ verify</span>':' <span class="b">r1/3</span>';
  else if(b==='desk') tail=' <span class="b">· desk</span>'; else if(b) tail=' <span class="b">'+b+'</span>';
  return (r.rec==='incident.response'?'mitigate':r.st)+tail;
}
const wfSubject=r=>E(r.rec==='incident.response'?r.subj:(r.short||r.subj));
function wfRow(r){
  const ans=r.desk&&!r.done?'<button class="btn sm" data-act="wf-answer" data-id="'+A(r.a)+'" aria-label="Answer: classify '+A(r.short||r.subj)+'">Answer</button>':'';
  return '<div class="row wf" data-row="wf:'+A(r.a)+'" tabindex="0" aria-label="'+A(wfSubject(r).replace(/&amp;/g,'&'))+', '+r.rec+'">'+wfChip(r)+'<div class="t"><span class="row-title">'+wfSubject(r)+'</span><span class="recipe">'+r.rec+'</span></div><span class="pb">'+barH(r)+'<span class="stage">'+stageName(r,false)+'</span></span><span class="m now">'+r.now+'</span><span class="w">'+r.age+'</span><div class="a">'+ans+'</div></div>';
}
function wfBand(){
  const L=wfStripRows(), cap=S.ts>=1.3?4:5, rows=L.slice(0,cap).map(wfRow).join('');
  return '<section class="band" aria-labelledby="wf-h"><div class="section"><div class="sh"><h2 id="wf-h">Workflows</h2><span class="meta">'+S.roots.length+' open</span>'+moreLink('all '+S.roots.length+' →','workflows','All '+S.roots.length+' open workflows, opens Workflows')+'</div><div class="rows">'+rows+'</div></div></section>';
}
function lrRow(x,maj){
  const you=x.g==='withyou', id=x.id, key='run:'+id;
  const chip=you?chipH('With you'):chipH(x.g==='queued'?'Queued':'Running');
  let a='', aria='';
  if(x.g==='queued'){
    aria='Queued: '+id; a=dropBtn('q:'+id,'Open','Open '+id,mOpenIn(x));
    return calmRow({cls:'lr',row:'q:'+id,aria,chip,t:'<span class="id at-rest mono">'+id+'</span><span class="row-title">'+E(x.title)+'</span>'+tagH(x,maj),w:x.age.replace(/^(\d+h)$/,'$1'),a,extra:rx((S.launch[id]?sentLine(x):'')+cancelConfirmH('cancel:'+id,id,'The item closes.'))});
  }
  if(you){
    aria='With you: '+id;
    a='<button class="btn sm" data-act="handback" data-id="'+id+'">Hand back</button>';
  } else if(x.term){
    aria='Running: '+id;
    a=S.attached[id]?'<span class="px-att" role="status">'+G('g-live','●')+'Attached in Warp</span>':dropBtn('run:'+id,'Attach','Attach '+id,mCap(ATTACH_RULE)+mItem('Attach in Warp','attach',id)+mItem('Take over','takeover',id,null,'the automation pauses; you hand back')+mItem('Open in Claude Code','open',id,'claude-code')+mCancel(id,'Asks first'));
  } else {
    aria='Running: '+id;
    a=dropBtn('run:'+id,'Open instance','Open instance '+id,mItem('Open instance','open-sheet',id)+mOpenIn(x)+mItem('Take over','takeover',id,null,'the automation pauses; you hand back')+mCancel(id,'Asks first'));
  }
  return calmRow({cls:'lr',row:key,aria,chip,t:'<span class="id at-rest mono">'+id+'</span><span class="row-title">'+E(x.title)+'</span>'+tagH(x,maj),w:x.age,a,extra:rx((S.launch[id]?sentLine(x):'')+cancelConfirmH('cancel:'+id,id,'Its session stops and the item closes.'))});
}
/* fixture.md "The health sentence": the pill's reasons in words, verbatim on Home and System; markup adds the colour spans only */
function healthSentence(){
  const bits=[]; if(S.svc.index.state!=='ok') bits.push('the code index <span class="f">failed</span> at the 03:00 build'); if(S.profile.changed) bits.push('the profile changed since approval'); if(!S.sync.onyx) bits.push('Onyx is 2h behind');
  const list=bits.length<3?bits.join(' and '):bits.slice(0,-1).join(', ')+', and '+bits[bits.length-1];
  const upd=S.conn==='normal'?'Updated 2s ago.':S.conn==='stalled'?'Views are stale.':'Updated 47s ago.';
  return bits.length?'Sindri is <span class="d">degraded</span>: '+list+'. '+upd:'Sindri is healthy. '+upd;
}
function home(){
  const L=needs(), run=inG('running'), you=inG('withyou'), q=inG('queued');
  const runL=you.concat(run), majR=majority(runL), majQ=majority(q);
  const head='<header class="pagehead"><div class="ph-l"><h1 class="page-title">Home</h1><p class="sub">'+(L.length?'<span class="attn-word">'+L.length+' need you</span>':'<span>Nothing needs you</span>')+'<span>·</span><span>'+run.length+' running</span><span>·</span><span>'+you.length+' with you</span><span>·</span><span>'+q.length+' queued</span></p></div></header>';
  const runSec='<div class="section" aria-labelledby="run-h"><div class="sh"><h2 id="run-h">Running</h2><span class="meta">'+run.length+' live · '+you.length+' with you</span>'+moreLink('all '+runL.length+' →','work','All '+runL.length+' in Work')+'</div><div class="rows">'+runL.map(x=>lrRow(x,majR)).join('')+'</div>'+(runL.some(x=>x.term)?'<p class="attach-note">'+ATTACH_RULE+'</p>':'')+'</div>';
  const qSec='<div class="section" aria-labelledby="q-h"><div class="sh"><h2 id="q-h">Queued</h2><span class="meta">'+q.length+'</span>'+moreLink('all '+q.length+' →','work','All '+q.length+' queued in Work')+'</div><div class="rows">'+q.map(x=>lrRow(x,majQ)).join('')+'</div></div>';
  return wrapV('v-home'+(S.sheet?' narrow':'')+(S.conn==='stalled'?' stale':''),'<main class="page'+(S.sheet?' behind':'')+'">'+head+banner()+needsBand()+wfBand()+'<section class="band" aria-label="Running and queued"><div class="cols">'+runSec+qSec+'</div></section><p class="hsent" aria-label="Sindri health">'+healthSentence()+'</p></main>');
}

/* ───────── the instance frame (§4.2, §4.7, §5): states band, facts, children, tabs ───────── */
const GL={d:['g-done','●'],p:['g-prog','◐'],t:['g-todo','○'],live:['g-live','●'],park:['g-park','▌'],esc:['g-esc','▲'],you:['g-you','◎']};
function node(n){ // {name,s:'d|p|t',badge,mono,cur,park,loop,gate}
  const g=GL[n.s]||GL.t;
  return '<span class="node'+(n.cur?' cur" aria-current="step':'')+(n.s==='t'?' todo':'')+'">'+(n.cur?G('g-cur','▣'):'')+'<b'+(n.gate?' class="mono" style="font-size:12px"':'')+'>'+n.name+'</b>'+G(g[0],g[1])+(n.badge?'<span class="badge'+(n.mono?' mono':'')+'">'+n.badge+'</span>':'')+(n.xbadge||'')+'</span>';
}
function pathHtml(nodes){
  return '<div class="path" aria-label="States in path order">'+nodes.map((n,i)=>'<span class="step">'+(i?(n.loop?'<span class="link loop" role="img" aria-label="loops with">⇄</span>':'<span class="link" aria-hidden="true">──</span>'):'')+node(n)+'</span>').join('')+'</div>';
}
function krow(k,v){ return '<div class="krow"><span class="k">'+k+'</span><span class="v ink">'+v+'</span></div>'; }
function expandHtml(o){ // {name, decl, next, trace, stat, acts, hint, extra}
  return '<div class="expand" aria-label="Current state: '+A(o.name)+'"><div class="decl">'+o.decl+'</div>'+krow('next',o.next)+(o.trace?'<div class="krow"><span class="k">trace</span><ol class="trace" aria-label="Activities of the state">'+o.trace+'</ol></div>':'')+(o.extra||'')+'<div class="ctl"><div class="stat">'+o.stat+'</div><div class="acts">'+(o.acts||'')+'</div>'+(o.attach?'<p class="attach-note">'+ATTACH_RULE+'</p>':'')+(o.hint?'<div class="hint">'+o.hint+'</div>':'')+'</div></div>';
}
function statesPanel(path,exp){ return '<div class="sb"><section class="section" aria-labelledby="st-h"><div class="sh"><h2 id="st-h">States</h2></div>'+pathHtml(path)+exp+'</section></div>'; }
function factRow(key,s,sum,acts){ return '<div class="fact"><span class="key">'+key+'</span><span class="sum">'+sum+'</span><div class="acts">'+(acts||'')+'</div></div>'; }
const fb=(t,x)=>'<button class="btn sm" data-act="fact" data-x="'+A(x||t)+'">'+t+'</button>';
function factsPanel(rows,foot){ return '<div class="sb alt"><section class="section facts" aria-labelledby="fa-h"><div class="sh"><h2 id="fa-h">Facts</h2><span class="meta">declared by the recipe, produced so far</span></div>'+rows.join('')+(foot||'')+'</section></div>'; }
function childrenSec(n,meta,inner,foot){ return '<div class="sb"><section class="section" aria-labelledby="ch-h"><div class="sh"><h2 id="ch-h">Children</h2><span class="meta">'+meta+'</span></div>'+inner+(foot||'')+'</section></div>'; }
function tabsSec(counts,active,tl,decs,reqs,ctx){ return '<div class="sb alt">'+tabs(counts,active)+tabPanel(active,tl,decs,reqs,ctx,counts[0])+'</div>'; }
function sheetConfirm(key,html,yes,no){ return '<div class="confirm" role="alertdialog" aria-labelledby="cf-q"><span id="cf-q">'+html+'</span><div class="acts"><button class="btn sm danger" data-act="confirm-yes" data-x="'+A(key)+'">'+yes+'</button><button class="btn sm" data-act="confirm-no" data-x="'+A(key)+'" data-first>'+no+'</button></div></div>'; }
function stat(parts){ return parts.filter(p=>!p.startsWith('<span>commands</span>')).map(p=>'<span class="kv">'+p+'</span>').join('<span>·</span>'); }
function bar4(p,l){ return '<span class="bar4" role="img" aria-label="'+l+'"><i style="width:'+p+'%"></i></span>'; }
function tabs(counts,active){
  const T=[['timeline','Timeline'],['decisions','Decisions'],['requests','Requests'],['context','Context']];
  return '<div class="tabs" role="tablist">'+T.map((t,i)=>'<button class="tab" role="tab" data-act="stab" data-x="'+t[0]+'" aria-selected="'+(active===t[0])+'"'+(active===t[0]?' aria-controls="tp"':'')+'>'+t[1]+' <span class="n">'+counts[i]+'</span>'+kbd(i+1)+'</button>').join('')+'</div>';
}
function tabPanel(active,tl,decs,reqs,ctx,total){
  let body='';
  if(active==='timeline'){
    const key='tl:'+(S.sheet?S.sheet.kind+':'+S.sheet.id:''), shown=S.more[key]?tl:tl.slice(0,2), rest=(total==null?tl.length:total)-shown.length;
    body=shown.map(e=>'<div class="ev"><span class="t">'+e[0]+'</span><span class="a">'+e[1]+'</span><span class="d">'+e[2]+'</span></div>').join('')+(rest>0?'<div class="ev more"><span class="t"></span><span class="a"></span><span class="d"><a href="#" data-act="tl-more" data-x="'+A(key)+'" aria-label="'+rest+' more events, opens the full timeline">… '+rest+' more</a></span></div>':'');
  }
  else if(active==='decisions') body=decs.length?decs.map(e=>'<div class="ev"><span class="t">'+e[0]+'</span><span class="a">'+e[1]+'</span><span class="d">'+e[2]+'</span></div>').join(''):'<div class="ev"><span class="t"></span><span class="a"></span><span class="d">No decisions recorded.</span></div>';
  else if(active==='requests') body=reqs.length?reqs.map(e=>'<div class="ev"><span class="t">'+e[0]+'</span><span class="a">'+e[1]+'</span><span class="d">'+e[2]+'</span></div>').join(''):'<div class="ev"><span class="t"></span><span class="a"></span><span class="d">No requests filed.</span></div>';
  else body=ctx.map(e=>'<div class="ev"><span class="t"></span><span class="a mono">'+e[0]+'</span><span class="d">'+e[1]+'</span></div>').join('');
  return '<div class="tabpanel" id="tp" role="tabpanel" aria-label="'+({timeline:'Timeline, newest first',decisions:'Decisions',requests:'Requests',context:'Context'}[active])+'">'+body+'</div>';
}
function sheetShell(cls,label,inner,w){ // wrapper carries the scoped CSS classes
  return '<div class="shw '+cls+'"><aside class="sheet wide" aria-label="'+A(label)+'" role="complementary"><div class="sheet-grip" role="separator" aria-orientation="vertical" aria-label="Resize sheet" aria-valuemin="420" aria-valuemax="760" aria-valuenow="'+S.sheetW+'" tabindex="0" data-grip></div>'+inner+'</aside></div>';
}
function head(o){ // o:{addr,ttl,meta,subj,sacts,menu,confirm,cancelLabel,id}
  const back=S.stack.length?'<button class="btn ghost sm sheetback show" data-act="sheet-back">◂ '+E(S.stack[S.stack.length-1].id)+'</button>':'<button class="btn ghost sm sheetback" data-act="sheet-back">◂ Back</button>';
  const mnu=S.menu==='sub:'+o.id;
  return '<div class="sheet-head"><div class="crumb"><div class="lead">'+back+'<span class="addr id">'+o.addr+'</span><span class="ttl">'+E(o.ttl)+'</span><span class="meta">'+o.meta+'</span></div><div class="r">'+kbd('Esc')+'<button class="icon-btn" data-act="close-sheet" aria-label="Close sheet">'+I.close+'</button></div></div>'+
   '<div class="subject'+(o.stack?' stack':'')+'"><span class="lbl">subject</span><span class="txt">'+o.subj+'</span><div class="acts">'+o.sacts+'<button class="btn sm chev" data-act="menu" data-id="sub:'+o.id+'" aria-label="More actions for '+A(o.id)+'" aria-haspopup="menu" aria-expanded="'+mnu+'">'+I.chev+'</button>'+(mnu?'<div class="menu" role="menu"><button class="mi" role="menuitem" data-act="ask-cancel-sub" data-id="'+A(o.id)+'">'+o.cancelLabel+' '+kbd('X')+'<small>'+o.cancelNote+'</small></button></div>':'')+'</div></div>'+(o.confirm||'')+'</div>';
}
const SHEET_CLS='v-workflow-project v-workflow-incident v-item-sheet';
function jobBtns(){ return ''; }

/* ───────── item sheet and instance sheets ───────── */
const FEATURE={'ITM-01JE7':'feature:needs-you.ack','ITM-01JD6':'feature:services.job-panel','ITM-01JC8':'feature:outbox.dispatch','ITM-01JB4':'feature:tabs.layout'};
function featOf(x){ return FEATURE[x.id]||('feature:'+x.title.toLowerCase().replace(/[^a-z0-9]+/g,'-').slice(0,22).replace(/-$/,'')); }
function baseTL(x){
  const t={'ITM-01JE7':[['12:20','ITM-01JE7','produce round 2 started · Claude Code · <span class="mono">snd-01JE7.2</span>'],['12:19','ITM-01JE7','checks failed · 2 findings'],['12:17','ITM-01JE7','produce round 1 done · 4 files · <span class="mono">+182 −9</span>'],['12:15','ITM-01JE7','spec.md written · <span class="mono">sha256:1c0e…</span>'],['12:14','ITM-01JE7','started · workflow.item@3f1c09a2'],['12:14','ITM-01JE7','intake · Linear ENG-511']],
   'ITM-01JC8':[['11:54','ITM-01JC8','parked: no-progress · checks failed twice with the same findings'],['11:50','ITM-01JC8','checks failed · same 2 findings'],['11:41','ITM-01JC8','produce round 3 started'],['10:32','ITM-01JC8','intake · Linear ENG-503']],
   'ITM-01JB4':[['09:10','ITM-01JB4','escalated: gate.verify.judge 0.62 below 0.80'],['09:08','ITM-01JB4','checks passed'],['08:41','ITM-01JB4','intake · Linear ENG-490']],
   'ITM-01JA1':[['12:01','ITM-01JA1','taken over by you · Claude Code · <span class="mono">snd-01JA1.3</span>'],['11:34','ITM-01JA1','produce started'],['11:30','ITM-01JA1','intake · Linear APP-212']],
   'ITM-01JD2':[['11:40','ITM-01JD2','request REQ-01JD4 filed by a session'],['11:12','ITM-01JD2','intake · repo sindri-app']]}[x.id];
  return t||[[x.since||'12:00',x.id,(x.g==='queued'?'queued · waiting for a slot':'started')],['12:00',x.id,'intake · '+E(x.src||'')]];
}
/* events older than the ones this prototype stores: the Timeline count is the whole timeline (ITM-01JE7: 41) */
const TL_OLDER={'ITM-01JE7':35};
function tlOf(x){ return (S.tl[x.id]||[]).concat(baseTL(x)); }
function pushTL(id,line){ (S.tl[id]=S.tl[id]||[]).unshift([CLOCK,id,line]); }
const CLOCK='12:26';
function itemPath(x){
  const N=(name,s,o)=>Object.assign({name,s},o||{});
  const p=[N('intake','d'),N('produce','t'),N('checks','t'),N('gate.verify.judge','t',{gate:true,badge:'≥ 0.80',mono:true}),N('review','t',{badge:'desk'}),N('done','t')];
  let cur=1;
  if(x.g==='esc') cur=3; else if(x.step==='checks') cur=2; else if(x.g==='queued'||x.g==='decision') cur=0;
  for(let i=0;i<cur;i++) p[i].s='d';
  const c=p[cur]; c.cur=true; c.s=(x.g==='queued')?'t':'p';
  if(x.id==='ITM-01JE7'){ p[2].s='p'; p[2].badge='r1 failed'; p[2].mono=true; p[2].loop=true; c.badge='r2/3'; c.mono=true; }
  else if(x.round&&cur<3){ c.badge='r'+x.round; c.mono=true; }
  if(x.g==='park') c.xbadge='<span class="badge">'+G('g-park','▌')+'parked: '+x.reason+'</span>';
  if(x.g==='esc'){ c.xbadge='<span class="badge">'+G('g-esc','▲')+'escalated: gate</span>'; c.gate=true; c.badge='0.62 &lt; 0.80'; c.mono=true; }
  if(x.g==='withyou') c.xbadge='<span class="badge">'+G('g-you','◎')+'with you</span>';
  if(x.g==='decision') c.xbadge='<span class="badge">'+G('','?')+'decision</span>';
  return p;
}
function itemFacts(x){
  const num=x.src&&x.src.match(/ENG-\d+|APP-\d+/); const k=num?num[0]:'intake';
  const rows=[factRow('fact:item','d','<b>'+k+'</b> intake record · '+(x.src&&x.src.startsWith('Linear')?'Linear':E(x.src||''))+' · <span class="note">'+(x.src&&x.src.includes('PRJ-sync')?'project PRJ-sync':'no project')+'</span>',fb('Open ↗','Linear'))];
  if(x.g==='queued'||x.g==='decision'){ rows.push(factRow('fact:spec','t','<span class="note">written when the item starts</span>')); return rows; }
  rows.push(factRow('fact:spec','d','<b>spec.md</b> <span class="mono">sha256:1c0e…</span>',fb('Open ↗','spec')));
  if(x.id==='ITM-01JE7'){ rows.push(factRow('fact:output','p','round 1 · 4 files · <span class="mono">+182 −9</span> · round 2 running',fb('Diff ↗','diff')),factRow('fact:findings','d','2 findings: second ack returns 500; route not in catalog',fb('View ▸','findings')),factRow('fact:evidence','t','<span class="note">after checks pass</span>')); return rows; }
  rows.push(factRow('fact:output',x.g==='running'?'p':'d',x.g==='running'?'round '+(x.round||'1')+' running':'round 3 · 3 files · <span class="mono">+96 −14</span>',fb('Diff ↗','diff')));
  if(x.g==='park'||x.g==='esc') rows.push(factRow('fact:findings','d','2 findings, the same in the last two checks',fb('View ▸','findings')));
  rows.push(factRow('fact:evidence','t','<span class="note">after checks pass</span>'));
  return rows;
}
function itemExpand(x){
  const id=x.id, you=x.g==='withyou', att=S.attached[id], reqOpen=S.forms['req:'+id];
  const decl=(extra)=>'<b>'+G('g-cur','▣')+(x.g==='esc'?'gate.verify.judge':x.g==='queued'||x.g==='decision'?'intake':x.step&&x.step[0]!=='s'?x.step:'produce')+'</b><span class="sep">·</span>'+extra;
  let o={name:x.step||'intake'};
  const first=(btn)=>btn;
  if(x.g==='running'&&x.term){
    o.decl=decl('<span class="mono">step.'+(x.step||'produce')+'</span><span class="sep">·</span><span class="live">'+G('g-live','●')+E(x.agent)+'</span><span class="sep">·</span><span class="mono">'+x.sess+'</span><span class="sep">·</span><span>since '+x.since+' ('+x.age+')</span><span class="sep">·</span><span class="mono">'+x.tok+'k / 200k</span>'+bar4(Math.round(x.tok/2),Math.round(x.tok/2)+' percent of the token budget used'));
    o.next=id==='ITM-01JE7'?'Produce the Step output (round 2/3). Round 1\'s checks found 2 findings: second ack returns 500; route missing from the catalog.':'Produce the Step output (round '+(x.round||'1/3')+').';
    o.trace=id==='ITM-01JE7'?'<li>'+G('g-done','●')+'<span class="nm">step.produce r1</span><span class="st">done</span><span class="t">12:17</span><span class="d">4 files · +182 −9</span></li><li>'+G('g-done','●')+'<span class="nm">step.checks</span><span class="st fail">failed</span><span class="t">12:19</span><span class="d">2 findings</span></li><li>'+G('g-live','●')+'<span class="nm">step.produce r2</span><span class="st">running</span><span class="t">12:20</span><span class="d">attempt 1</span></li>':'';
    o.stat=stat(['<span>timeout</span><span class="mono">parks in 1d 23h</span>','<span>commands</span><b>takeover, request, cancel</b>']);
    o.acts=(att?'<span class="px-att" role="status">'+G('g-live','●')+'Attached in Warp</span>':'<div class="split">'+'<button class="btn main primary" data-first data-act="attach" data-id="'+id+'" aria-label="Attach to the '+(x.step||'produce')+' session">'+I.warp+'Attach '+kbd('O')+'</button><button class="btn chev primary" data-act="menu" data-id="att:'+id+'" aria-label="Choose agent" aria-haspopup="menu" aria-expanded="'+(S.menu==='att:'+id)+'" style="border-left-color:rgba(247,248,248,.25)">'+I.chev+'</button>'+(S.menu==='att:'+id?'<div class="menu" role="menu" aria-label="Choose agent"><div class="cap">'+ATTACH_RULE+'</div><div class="cap">Open another in</div>'+TARGETS.filter(t=>!t.off).map(t=>'<button class="mi" role="menuitem" data-act="open" data-id="'+id+'" data-x="'+t.id+'">'+t.label+'<small>'+t.why+'</small></button>').join('')+'</div>':'')+'</div>')+
      '<button class="btn" data-act="takeover" data-id="'+id+'">Take over '+kbd('T')+'</button><button class="btn" data-act="req-form" data-id="'+id+'">Request '+kbd('R')+'</button>'; o.attach=true;
  } else if(x.g==='running'){
    o.decl=decl('<span class="mono">step.'+x.step+'</span><span class="sep">·</span><span class="live">'+G('g-live','●')+E(x.agent)+'</span><span class="sep">·</span><span class="mono">'+x.sess+'</span><span class="sep">·</span><span>tool Step, no terminal</span><span class="sep">·</span><span>since '+x.since+' ('+x.age+')</span>');
    o.next=x.step==='checks'?'Run the checks for round '+(x.round||'1')+'; a failure loops back to produce.':'Run the Step.';
    o.stat=stat(['<span>timeout</span><span class="mono">parks in 1d 23h</span>','<span>commands</span><b>takeover, cancel</b>']);
    o.acts='<button class="btn" data-first data-act="takeover" data-id="'+id+'">Take over '+kbd('T')+'</button>'; o.hint='No terminal: there is nothing to attach to. Take over pauses the automation at once.';
  } else if(you){
    o.decl=decl('<span class="mono">step.produce</span><span class="sep">·</span><span>taken over · since '+x.since+' ('+x.age+')</span><span class="sep">·</span><span class="mono">'+(x.sess||'')+'</span>');
    o.next='The automation is paused. Hand back when you are done and the engine takes the item from here.';
    o.stat=stat(['<span>commands</span><b>handback, cancel</b>']);
    o.acts=(x.att?'<span class="px-att" role="status">'+G('g-live','●')+'Attached in Warp</span>':(att?'<span class="px-att" role="status">'+G('g-live','●')+'Attached in Warp</span>':'<button class="btn main" data-act="attach" data-id="'+id+'">'+I.warp+'Attach</button>'))+'<button class="btn primary" data-first data-act="handback" data-id="'+id+'">Hand back '+kbd('B')+'</button>'; o.attach=true;
  } else if(x.g==='park'){
    const cli=x.reason==='budget'?'sindri workflow answer '+id+' --grant-tokens 100000':'sindri workflow answer '+id+' --grant 1'; const k='sh:'+id;
    o.decl=decl('<span class="mono">step.produce</span><span class="sep">·</span><span class="unit">'+G('g-park','▌','parked')+'parked: '+x.reason+' · '+E(x.why||'')+'</span>');
    o.next=x.reason==='needs-answer'?'The agent asked a question and waits. Answer with text and the round continues.':'Parked at round 3/3: the checks failed twice with the same findings. Grant one more round at a terminal, or answer with a note the agent reads.';
    o.stat=stat(['<span>commands</span><b>'+(x.reason==='needs-answer'?'answer':'answer, grant (terminal), cancel')+'</b>']);
    const prot=(x.reason==='needs-answer'||x.reason==='timeout')?'':(x.reason==='budget'?lockBtn('Grant tokens · terminal',cli,k):lockBtn('Grant rounds · terminal',cli,k));
    o.acts=prot+(x.reason==='timeout'?'<button class="btn primary" data-act="resume" data-id="'+id+'" data-first>Resume</button>':'')+openSplit(x);
    o.extra=(S.revealed[k]?'<div class="krow"><span class="k">cli</span><span class="v">'+cmdLine(cli,k)+'<div class="px-note">Waits for the terminal: nothing changes here until you run it there.</div></span></div>':'')+
      ((x.reason==='no-progress'||x.reason==='needs-answer')?'<div class="krow"><span class="k">answer</span><span class="v"><textarea class="px-ta" id="ans" rows="2" data-focus-answer aria-label="Answer with text" placeholder="A note the agent reads on its next turn">'+E(S.note['ans:'+id]||'')+'</textarea><div class="px-formrow"><button class="btn primary sm" data-act="send-answer" data-id="'+id+'">Send answer '+kbd('⌘↵')+'</button><span class="note">'+(x.reason==='needs-answer'?'Desk answer; the engine records it now.':'Desk answer; sent as text, no round is granted.')+'</span></div></span></div>':'');
    o.hint=x.reason==='needs-answer'||x.reason==='timeout'?'':'Grant rounds needs a typed confirmation at a terminal; the lock reveals the command.';
  } else if(x.g==='esc'){
    const cli='sindri workflow answer '+id+' --accept', k='sh:'+id;
    o.decl=decl('<span class="mono">gate.verify.judge</span><span class="sep">·</span><span class="unit">'+G('g-esc','▲','escalated')+'escalated: scored '+x.score+', below '+x.min+'</span>');
    o.next='The judge scored the output '+x.score+'. Accept it at a terminal, or reject it and produce re-runs with the findings.';
    o.stat=stat(['<span>commands</span><b>accept (terminal), reject, cancel</b>']);
    o.acts=lockBtn('Accept · terminal',cli,k)+'<button class="btn" data-act="reject" data-id="'+id+'" data-first>Reject</button>'+openSplit(x);
    o.extra=S.revealed[k]?'<div class="krow"><span class="k">cli</span><span class="v">'+cmdLine(cli,k)+'<div class="px-note">Waits for the terminal: nothing changes here until you run it there.</div></span></div>':'';
  } else if(x.g==='decision'){
    o.decl=decl('<span class="mono">'+x.req+'</span><span class="sep">·</span><span class="unit">'+G('','?','decision')+'request from a session</span>');
    o.next=decText(x)+'. '+DECISION_RULE;
    o.stat=stat(['<span>commands</span><b>accept, decline</b>']);
    o.acts='<button class="btn primary" data-first data-act="decide" data-id="'+id+'" data-x="accept">Accept</button><button class="btn" data-act="decide" data-id="'+id+'" data-x="decline">Decline</button>';
  } else { // queued
    o.decl=decl('<span class="mono">workflow.item</span><span class="sep">·</span><span>queued '+x.age+', waiting for a slot</span>');
    o.next='The next free slot starts this item. Open it in an agent to start now.'; o.stat=stat(['<span>commands</span><b>start, cancel</b>']);
    o.acts=openSplit(x,false,true).replace('<button class="btn main primary"','<button data-first class="btn main primary"'); 
  }
  if(reqOpen) o.extra=(o.extra||'')+'<div class="krow"><span class="k">request</span><span class="v"><div class="px-formrow"><input class="px-in" id="reqin" aria-label="Request text" placeholder="A note for the session\'s next turn" value="'+A(S.note['req:'+id]||'')+'"><button class="btn primary sm" data-act="req-send" data-id="'+id+'">File request</button><button class="btn sm" data-act="req-form" data-id="'+id+'">Cancel</button></div></span></div>';
  return expandHtml(o);
}
function sheetItem(x){
  const id=x.id, meta='<span class="mono">workflow.item@3f1c09a2</span><span>·</span><span>v7</span><span>·</span><b>'+(x.g==='esc'?'gate.verify.judge':x.g==='queued'||x.g==='decision'?'intake':(x.step&&x.step[0]!=='s'?x.step:'produce'))+'</b>'+(x.round?'<span class="mono">r'+x.round+'</span>':'')+'<span>·</span><span>since '+(id==='ITM-01JE7'?'12:14 (12m)':(x.since?x.since+' ('+x.age+')':'queued '+x.age))+'</span>';
  const subj=(x.src&&x.src.startsWith('Linear')?'Linear <b>'+x.src.slice(7)+'</b>':'<b>'+E(x.src||'')+'</b>')+' · repo <b>'+x.repo+'</b> · '+(x.src&&x.src.includes('PRJ-sync')?'project PRJ-sync':'no project')+' · <span class="mono">'+featOf(x)+'</span>';
  const conf=S.confirm==='cancelsub:'+id?sheetConfirm('cancelsub:'+id,'<b>Cancel item '+id+'?</b> The session stops and the item closes.','Cancel item','Keep'):'';
  const reqs=S.req.filter(r=>r.item===id).map(r=>[r.t,id,E(r.text)]);
  const decs=x.g==='decision'?[[x.since,x.req,'pending: '+E(decText(x))]]:(x.note?[[CLOCK,x.req||id,E(x.note)]]:[]);
  const tl=tlOf(x), ctxc=[[featOf(x),'the feature this item implements'],['spec.md','the item\'s spec, from intake'],['repo '+x.repo,'the repository the session runs in']];
  const inner=head({id,addr:'item/'+id,ttl:x.title,meta,subj,stack:true,sacts:'<button class="btn sm" data-act="ext" data-x="Linear">Open in Linear ↗</button><button class="btn sm" data-act="ctx" data-id="'+A(featOf(x))+'">Open in Context ▸</button>',cancelLabel:'Cancel item…',cancelNote:'asks first: the session stops and the item closes',confirm:conf})+
   '<div class="sheet-body">'+statesPanel(itemPath(x),itemExpand(x))+factsPanel(itemFacts(x))+childrenSec(0,'0','<p class="meta"><span class="mono">workflow.item</span> declares no children</p>')+tabsSec([tl.length+(TL_OLDER[id]||0),decs.length,reqs.length,12],S.sheetTab,tl,decs,reqs,ctxc)+'</div>';
  return sheetShell(SHEET_CLS,'Item '+id+', '+x.title,inner);
}
/* ── workflow instances ── */
const WFX={}; // per-root detail
function rootNodes(r){
  const names=r.rec==='incident.response'?['detected','triage','mitigate','comms','postmortem','closed']:r.rec==='triage.alert'?['received','classify','route','done']:r.rec==='workflow.project'?['intake','studio','scoping','design','build','verify','ship']:['poll','emit'];
  const cells=r.cells.length?r.cells:['p','t'];
  return names.map((n,i)=>{ const c=cells[i]||'t', s=c[0]==='e'?'p':c[0], b=c.split(' ')[1]; const o={name:n,s,badge:b&&b!=='→'?b:(c==='esc'?null:null),mono:/\d/.test(b||'')};
    if(c==='esc'){ o.s='p'; o.xbadge='<span class="badge">'+G('g-esc','▲')+'escalated: gate.severity 0.91</span>'; }
    return o; });
}
function sheetWf(r){
  const a=r.a, spec=r.rec;
  let nodes, exp, facts, children='', meta, subj, ttl=r.team?r.subj:r.subj, tl, tabsC=[3,0,0,3], decs=[], reqs=[], sacts='<button class="btn sm" data-act="ext" data-x="Linear">Open in Linear ↗</button><button class="btn sm" data-act="ctx" data-id="feature:sync">Open in Context ▸</button>', cancelNote='asks first: the running children stop and the instance closes';
  const conf=S.confirm==='cancelsub:'+a?sheetConfirm('cancelsub:'+a,'<b>Cancel '+E(a)+'?</b> '+(a==='INC-14'?'The mitigation agent is stopped and the comms child closes.':'The running children stop and the instance closes.'),'Cancel instance','Keep'):'';
  if(a==='INC-14'){
    nodes=[{name:'detected',s:'d'},{name:'triage',s:'d',badge:'sev2',mono:1},{name:'mitigate',s:'p',cur:1,badge:'r1/3',mono:1},{name:'verify',s:'t',loop:1},{name:'comms',s:'p',badge:'child'},{name:'postmortem',s:'t',badge:'desk'},{name:'closed',s:'t'}];
    const lite=S.attached['INC-14'];
    exp=expandHtml({name:'mitigate',decl:'<b>'+G('g-cur','▣')+'mitigate</b><span class="sep">·</span><span class="mono">step.produce</span><span>(agent)</span><span class="sep">·</span><span>loop ≤ 3 with <span class="mono">verify</span></span><span class="sep">·</span><span>gate on exit:</span><span class="mono">gate.metric.below(error_rate, 1%)</span><span>for 10m</span>',next:'Agent is applying the rollback in ITM-01JG2; verify re-checks the error rate 10 min after each attempt.',
      stat:stat(['<span>budget</span><span class="mono">60k / 200k</span>'+bar4(30,'30 percent of this state\'s budget used')+'<span>this state</span>','<span>timeout</span><span class="mono">escalates in 36m</span>','<span>commands</span><b>takeover, cancel</b>']),
      acts:(lite?'<span class="px-att" role="status">'+G('g-live','●')+'Attached in Warp</span>':'<div class="split"><button class="btn main primary" data-first data-act="attach" data-id="INC-14" aria-label="Attach to the mitigate session">'+I.warp+'Attach '+kbd('O')+'</button><button class="btn chev primary" data-act="menu" data-id="att:INC-14" aria-label="Choose agent" aria-haspopup="menu" aria-expanded="'+(S.menu==='att:INC-14')+'" style="border-left-color:rgba(247,248,248,.25)">'+I.chev+'</button>'+(S.menu==='att:INC-14'?'<div class="menu" role="menu" aria-label="Choose agent"><div class="cap">'+ATTACH_RULE+'</div></div>':'')+'</div>')+'<button class="btn" data-act="wf-takeover" data-id="INC-14">Take over '+kbd('T')+'</button>',attach:true});
    facts=[factRow('fact:alert','d','<b>P-2211 payload</b> · 11:02 · <span class="mono">error_rate 4.2%</span>',fb('View ▸','payload')),factRow('fact:severity','d','<b>sev2</b> · decided by you 11:09 <span class="note">("customer-visible, no data loss")</span>'),factRow('fact:runbook','d','<b>runbooks/outbox-retry.md</b> <span class="note">(repo-docs)</span> · cited <span class="mono">R2</span>',fb('Open ↗','runbook')),factRow('fact:timeline','p','comms child posts to <span class="mono">#incidents</span> every 15m · last 12:15'),factRow('fact:postmortem','t','<span class="note">desk step, opens after closed</span>')];
    const cm=S.menu==='ch:ITM-01JG2';
    children=childrenSec(2,'2 · <span class="mono">mitigate</span>, <span class="mono">comms</span>','<div class="children"><div class="child" aria-label="mitigate: ITM-01JG2 running">'+chipH('Running')+'<span class="ttl">Roll back dispatcher 1.4.2<span class="tag meta">mitigate</span><span class="id at-rest mono">ITM-01JG2</span></span><span class="when">14m</span><div class="acts"><div class="split"><button class="btn sm main" data-act="open-sheet" data-id="ITM-01JG2" aria-label="Open ITM-01JG2">Open</button><button class="btn sm chev" data-act="menu" data-id="ch:ITM-01JG2" aria-label="More actions for ITM-01JG2" aria-haspopup="menu" aria-expanded="'+cm+'">'+I.chev+'</button></div>'+(cm?'<div class="menu" role="menu"><button class="mi" role="menuitem" data-act="attach" data-id="INC-14">Attach '+kbd('O')+'<small>'+ATTACH_RULE+'</small></button><button class="mi" role="menuitem" data-act="wf-takeover" data-id="INC-14">Take over '+kbd('T')+'<small>the agent pauses; you hand back with B</small></button><button class="mi" role="menuitem" data-act="ask-cancel-sub" data-id="INC-14">Cancel… '+kbd('X')+'<small>asks first</small></button></div>':'')+'</div></div><div class="child" aria-label="comms: INC-14/comms/1 in progress">'+chipH('Running')+'<span class="ttl muted mono">incident.comms<span class="tag meta">comms</span><span class="id at-rest mono">INC-14/comms/1</span></span><span class="when">next 12:30</span><div class="acts"></div></div></div>');
    meta='<span class="mono">incident.response@5d90e1b2</span><span>·</span><span>v4</span><span>·</span><b>mitigate</b><span class="mono">r1/3</span><span>·</span><span>since 11:02 (1h 24m)</span>';
    subj='<b>PagerDuty P-2211</b> <span class="note">(webhook)</span> · <span class="mono">feature:outbox.dispatch</span>'; sacts='<button class="btn sm" data-act="ext" data-x="PagerDuty">Open in PagerDuty ↗</button><button class="btn sm" data-act="ctx" data-id="feature:outbox.dispatch">Open in Context ▸</button>'; cancelNote='asks first: the mitigation agent stops and the comms child closes';
    tl=[['12:15','INC-14/comms/1','posted to <span class="mono">#incidents</span>'],['12:12','ITM-01JG2','mitigate round 1 started · Claude Code'],['11:09','INC-14','severity sev2 decided by you'],['11:02','INC-14','opened from P-2211']]; tabsC=[18,1,0,6]; decs=[['11:09','INC-14','severity sev2 · "customer-visible, no data loss"']];
  } else if(a==='PRJ-sync'){
    nodes=[{name:'intake',s:'d'},{name:'studio',s:'d'},{name:'scoping',s:'d',badge:'r1',mono:1},{name:'design',s:'d',badge:'desk'},{name:'build',s:'p',cur:1,badge:'4/9',mono:1},{name:'verify',s:'t',badge:'desk'},{name:'ship',s:'t'}];
    const jd7=S.jd7ok;
    exp=expandHtml({name:'build',decl:'<b>'+G('g-cur','▣')+'build</b><span class="sep">·</span><span class="mono">step.children(builder.feature)</span><span>one child per packet task</span><span class="sep">·</span><span class="unit">'+(jd7?'no children parked':G('g-park','▌','parked')+'1 parked: <span class="mono">ITM-01JD7</span> no-progress, mirrored from the child')+'</span><span class="sep">·</span><span>gates on exit:</span><span class="mono">gate.evidence.all</span>,<span class="mono">gate.judge.verify ≥ 0.8</span>',next:'Build runs each packet task as a child item; verify opens for you when every child is done.',
      stat:stat(['<span>budget</span><span class="mono">1.2M / 3M</span>'+bar4(40,'40 percent of this state\'s budget used')+'<span>this state</span>','<span>timeout</span><span class="mono">parks in 2d 4h</span>','<span>commands</span><b>takeover, cancel</b>'])});
    facts=[factRow('fact:brief','d','<b>PRD-sync.md</b> · <span class="mono">linear:doc</span> · updated 5d ago',fb('Open ↗','brief')),factRow('fact:scope-map','d','<b>scope-sync-v3.json</b> · 14 surfaces · backtest <span class="mono">0.74 / 0.70</span> <b>PASS</b>','<div class="split"><button class="btn sm main" data-act="fact" data-x="scope" aria-label="View ▸">View ▸</button><button class="btn sm chev" data-act="menu" data-id="fact:scope" aria-label="More actions for scope-map" aria-haspopup="menu" aria-expanded="'+(S.menu==='fact:scope')+'">'+I.chev+'</button>'+(S.menu==='fact:scope'?'<div class="menu" role="menu">'+mItem('Export ↗','fact','', 'export')+'</div>':'')+'</div>'),factRow('fact:design','d','<b>design-output/</b> · imported 10-12 08:50 <span class="note">(desk step, completed by you)</span>',fb('Open in Figma ↗','Figma')),factRow('fact:packet','d','<b>packet.json</b> · frozen <span class="mono">sha256:3f1c09</span> · 9 tasks · checks 9/9 ✓',fb('View ▸','packet')),factRow('fact:evidence','p','4 of 9 packs')];
    const kidMenu=(id,inner)=>S.menu==='ch:'+id?'<div class="menu" role="menu">'+inner+'</div>':'';
    const chev=(id)=>'<button class="btn sm chev" data-act="menu" data-id="ch:'+id+'" aria-label="More actions for '+id+'" aria-haspopup="menu" aria-expanded="'+(S.menu==='ch:'+id)+'">'+I.chev+'</button>';
    const kid=(id,t,ws,chip,when,mainBtn,menu,attn)=>'<div class="child'+(attn?' attn':'')+'" aria-label="'+id+' '+(attn?'parked, needs you':chip.replace(/<[^>]+>/g,'').toLowerCase())+'">'+chip+'<span class="ttl">'+t+'<span class="tag meta">'+ws+'</span><span class="id at-rest mono">'+id+'</span></span><span class="when">'+when+'</span><div class="acts"><div class="split">'+mainBtn+chev(id)+'</div>'+kidMenu(id,menu)+'</div></div>';
    const d6=byId('ITM-01JD6'); const d6chip=d6.g==='withyou'?chipH('With you'):d6.g==='cancelled'?chipH('Cancelled'):chipH('Running');
    const openBtn=(id)=>'<button class="btn sm main" data-act="open-sheet" data-id="'+id+'" aria-label="Open '+id+'">Open</button>';
    const d7btn=jd7?openBtn('ITM-01JD7'):'<button class="btn sm main term" data-act="lock" data-cli="sindri workflow answer ITM-01JD7 --grant 1" data-x="kid:JD7" title="Needs a typed confirmation at a terminal">'+I.lock+'Grant rounds · terminal</button>';
    children=childrenSec(9,'9 · <span class="mono">builder.feature</span>, by packet workstream','<div class="children">'+
      kid('ITM-01JD6','Services page job panel','ws-2 <span class="mono">Store</span>'.replace(/<[^>]+>/g,''),d6chip,d6.g==='cancelled'?'':d6.age||'0m',openBtn('ITM-01JD6'),mItem('Take over','takeover','ITM-01JD6',null,'the automation pauses')+mItem('Cancel…','ask-cancel','ITM-01JD6',null,'asks first'))+
      kid('ITM-01JD7','Background sync (iOS)','ws-3 Mobile',jd7?chipH('Running'):chipH('Parked','attn'),'',d7btn,(jd7?'':mCopy('sindri workflow answer ITM-01JD7 --grant 1','kid:JD7'))+mItem('Open','open-sheet','ITM-01JD7'),!jd7)+
      kid('ITM-01JF3','doctor views line','ws-3 Mobile',chipH('Queued'),'52m',openBtn('ITM-01JF3'),mItem('Open in Claude Code','open','ITM-01JF3','claude-code')+mItem('Cancel…','ask-cancel','ITM-01JF3',null,'asks first'))+'</div>','<div class="cfoot"><span>… 6 more · 4 done · 2 queued</span><a href="#work" data-act="go" data-id="work">Open all in Work ▸</a></div>');
    meta='<span class="mono">workflow.project@a8c21e7f</span><span>·</span><span>v12</span><span>·</span><b>build</b><span>·</span><span>since 09:14 (3h)</span>';
    subj='<b>Linear APP</b> · PRD-sync.md <span class="note">(linear:doc)</span> · <span class="mono">feature:sync</span>'; ttl='Offline sync';
    tl=[['12:26','ITM-01JD6','checks started · factory worker · <span class="mono">snd-01JD6.1</span>'],['12:26','ITM-01JD6','produce done'],['12:11','ITM-01JD7','parked: no-progress · mirrored here'],['09:14','PRJ-sync','build opened · 9 children']]; tabsC=[41,3,1,14]; decs=[['10-12','PRJ-sync','design imported by you'],['10-11','PRJ-sync','scope map accepted · backtest 0.74'],['10-11','PRJ-sync','packet frozen sha256:3f1c09']];
  } else {
    nodes=rootNodes(r); const isDesk=r.desk, isEsc=r.esc, isYou=r.you;
    const sev=S.sev&&S.sev[a];
    const d={name:r.st,decl:'<b>'+G('g-cur','▣')+r.st+'</b><span class="sep">·</span><span class="mono">'+(r.rec==='triage.alert'?'step.classify':r.rec==='workflow.project'?(r.st==='scoping'?'step.scope.challenge':'desk.import'):'step')+'</span>'+(isDesk||isYou?'<span class="sep">·</span><span class="badge">desk</span>':''),next:r.done?'Done. Nothing is waiting.':r.intake?'Polls Linear every 15m and starts a workflow for each new project.':isEsc?'Gate severity scored 0.91: accept at a terminal and INC-14 stays open, or decline.':isDesk&&r.rec==='triage.alert'?'Pick a severity for '+E(r.short||r.subj)+'. The classifier is unsure between sev2 and sev3.':isYou?'Export the scope map, design in Figma, then import the design output here.':'The challenger critiques the scope map; round 2 of 3.',stat:stat(['<span>commands</span><b>'+(isEsc?'accept (terminal), cancel':isDesk||isYou?'complete, cancel':'takeover, cancel')+'</b>']),hint:''};
    if(isDesk&&r.rec==='triage.alert'&&!r.done) d.acts=['sev1','sev2','sev3'].map((s,i)=>'<button class="btn'+(i===1?' primary':'')+'" data-act="wf-sev" data-id="'+A(a)+'" data-x="'+s+'"'+(i===0?' data-first':'')+'>'+s+'</button>').join('')+'<span class="note" style="margin-left:6px">Desk answer: completes classify</span>';
    else if(isEsc) d.acts=lockBtn('Accept · terminal','sindri workflow answer '+a+' --accept','wf:'+a)+(S.revealed['wf:'+a]?cmdLine('sindri workflow answer '+a+' --accept','wfc'):'');
    else if(isYou) d.acts='<input class="px-in" id="imp" aria-label="Design output path" value="design-output/" style="width:180px"><button class="btn primary" data-act="wf-complete" data-id="'+A(a)+'" data-first>Complete step</button>';
    else if(!r.done&&!r.intake) d.acts='<button class="btn" data-first data-act="wf-takeover" data-id="'+A(a)+'">Take over '+kbd('T')+'</button>';
    exp=expandHtml(d);
    facts=r.rec==='triage.alert'?[factRow('fact:payload','d','<b>'+E(r.short||r.subj)+'</b> · received '+r.since+' · <span class="mono">level error</span>',fb('View ▸','payload')),factRow('fact:severity',r.done?'d':'t',r.done?'<b>sev3</b> · classified':'<span class="note">pending your answer</span>'),factRow('fact:route',r.done?'d':'t',r.done&&r.note?'→ '+r.note:'<span class="note">after classify</span>')]:
      r.rec==='workflow.project'?[factRow('fact:brief','d','<b>'+r.inp+'</b> · <span class="mono">linear:doc/…</span> · updated 6d ago',fb('Open ↗','brief')),factRow('fact:scope-map',r.st==='scoping'?'p':'d',r.st==='scoping'?'draft v2 · challenger round 2':'<b>scope-v3.json</b> · backtest <span class="mono">0.72 / 0.70</span> PASS',fb('View ▸','scope')),factRow('fact:design',r.st==='design'?'p':'t',r.st==='design'?'<span class="note">desk step: import the design output</span>':'<span class="note">after scoping</span>')]:
      [factRow('fact:poll','p','every 15m · last 12:15 · 2 new items')];
    meta='<span class="mono">'+r.rec+(r.hash?'@'+r.hash:'')+'</span>'+(r.v?'<span>·</span><span>v'+r.v+'</span>':'')+'<span>·</span><b>'+r.st+'</b>'+(r.badge?'<span class="mono">'+r.badge+'</span>':'')+'<span>·</span><span>since '+r.since+' ('+r.age+')</span>';
    subj=r.rec==='triage.alert'?'<b>'+E(r.short||r.subj)+'</b> <span class="note">(webhook)</span> · <span class="mono">feature:tabs.layout</span>':r.rec==='workflow.project'?'<b>Linear '+(r.team||'ENG')+'</b> · '+r.inp+' <span class="note">(linear:doc)</span>':'<b>'+E(r.subj)+'</b>';
    sacts='<button class="btn sm" data-act="ext" data-x="'+(r.rec==='triage.alert'?'Sentry':'Linear')+'">Open in '+(r.rec==='triage.alert'?'Sentry':'Linear')+' ↗</button><button class="btn sm" data-act="ctx" data-id="feature:tabs.tab-manager">Open in Context ▸</button>';
    tl=[[r.since.slice(-5),r.a,'opened · '+r.rec]]; tabsC=[tl.length,0,0,4]; ttl=r.subj;
  }
  const tlAll=(S.tl[a]||[]).concat(tl);
  const inner=head({id:a,addr:'workflow/'+a,ttl,meta,subj,sacts,cancelLabel:'Cancel instance…',cancelNote,confirm:conf})+'<div class="sheet-body">'+statesPanel(nodes,exp)+factsPanel(facts)+(children||childrenSec(0,'0','<p class="meta">'+(r.intake?'<span class="mono">'+r.rec+'</span> declares no children':'no child instances yet')+'</p>'))+tabsSec([tabsC[0]+(S.tl[a]||[]).length,tabsC[1],S.req.filter(q=>q.item===a).length,tabsC[3]],S.sheetTab,tlAll,decs,S.req.filter(q=>q.item===a).map(q=>[q.t,a,E(q.text)]),[['feature:sync','the subject\'s product node'],['PRD','cited in 3 sections'],['decision','2 decisions']])+'</div>';
  return sheetShell(SHEET_CLS,'Workflow instance '+a+', '+ttl,inner);
}

/* ───────── Workflows and Work (the calm pass) ───────── */
const WF_COLS={'workflow.project':['intake','studio','scoping','design','build','verify','ship'],'triage.alert':['received','classify','route','done'],'incident.response':['detected','triage','mitigate ⇄ verify','comms','postmortem','closed']};
const REC_DESC={'workflow.project':'Linear projects','triage.alert':'webhooks','incident.response':'','intake.linear':'polling every 15m · LIN-ENG, LIN-APP'};
const roundAge=a=>{ const m=String(a||'').match(/^(?:(\d+)h)?\s*(?:(\d+)m)?$/); if(!m) return String(a||''); const h=+m[1]||0, mi=+m[2]||0; return h?(h+(mi>=30?1:0))+'h':mi+'m'; };
function wfChip(r){ return r.esc?chipH('Escalated'):r.done?chipH('Done'):r.you?chipH('With you'):r.desk?chipH('Decision'):chipH('Running'); }
/* the tag a row carries behind a sheet: the stage (stages are tags, never chips) */
function stageTag(r){
  if(r.done) return ''; if(r.esc||r.desk) return 'classify';
  if(r.rec==='incident.response') return 'mitigate r1/3';
  const b=r.badge&&r.badge!=='desk'?' '+(r.rec==='workflow.project'?r.badge.replace(/^r/,''):r.badge):'';
  return r.st+b;
}
const OTHER_RECIPES=[{n:'intake.linear',aria:'intake.linear, polling every 15 minutes',stage:'polling every 15m',now:'<span class="mono">LIN-ENG</span>, <span class="mono">LIN-APP</span> · last poll 12:15 · 2 new items',tag:'polling every 15m · 2 new items',when:'12:15'},{n:'scoping.project',aria:'scoping.project, standalone',stage:'standalone',now:'no instance open; Start… runs it on its own',tag:'standalone · none open',when:''}];
function wfRowW(r){
  const cx=S.confirm==='cancelrow:'+r.a;
  let now=r.now, a='', extra='';
  if(r.esc){ const cli='sindri workflow answer '+r.a+' --accept'; now='gate.severity 0.91 → INC-14 opened'; a=lockBtn('Accept · terminal',cli,'wfl:'+r.a,false); extra=rx(S.revealed['wfl:'+r.a]?cmdLine(cli,'wfl:'+r.a)+noteWaits:''); }
  else if(r.desk&&!r.done) a='<button class="btn sm" data-act="wf-answer" data-id="'+A(r.a)+'" aria-label="Answer: classify '+A(r.short||r.subj)+'">Answer</button>';
  else if(r.done&&r.note) now='→ <span class="mono">ITM-01JF0</span> queued';
  if(cx) now='<span class="px-confirm inl"><span><b>Cancel '+E(r.a)+'?</b> The running children stop.</span><span class="acts"><button class="btn sm danger" data-act="confirm-yes" data-x="cancelrow:'+A(r.a)+'">Cancel instance</button><button class="btn sm" data-act="confirm-no" data-x="cancelrow:'+A(r.a)+'" data-first>Keep</button></span></span>';
  const idc=r.esc?'<span></span>':'<span class="id at-rest mono">'+E(r.a)+'</span>';
  return '<div class="row wr" data-row="wf:'+A(r.a)+'" tabindex="0" aria-label="'+A(r.esc?r.subj:r.a+' '+r.subj)+'">'+wfChip(r)+idc+'<div class="t"><span class="row-title">'+E(r.subj)+'</span></div><span class="pb">'+barH(r)+'<span class="stage">'+stageName(r,true)+'</span></span><span class="w">'+r.age+'</span><span class="m now">'+now+'</span><div class="a">'+a+'</div>'+extra+'</div>';
}
function wfBandFull(g,rs){
  const all=S.wfAll[g], main=rs.filter(r=>!r.more||all), more=rs.filter(r=>r.more&&!all);
  const meta=(REC_DESC[g]?REC_DESC[g]+' · ':'')+rs.length;
  return '<section class="band" aria-labelledby="g-'+g+'"><div class="section"><div class="sh"><h2 id="g-'+g+'">'+g+'</h2><span class="meta">'+meta+'</span></div><div class="rows" role="list" aria-label="'+g+' instances">'+main.map(wfRowW).join('')+(more.length?'<div class="row more"><a href="#" data-act="wf-more" data-x="'+g+'" aria-label="'+more.length+' more done '+g+' instances: '+more.map(r=>r.a).join(', ')+'; expands the group">… '+more.length+' more done</a></div>':'')+'</div></div></section>';
}
/* one order rule everywhere: most urgent first, then the order the fixture lists them (done last) */
const WF_GROUPS=['incident.response','triage.alert','workflow.project'];
const prjRank=a=>({'PRJ-sync':0,'PRJ-exports':1,'PRJ-tabs':2}[a]??3);
function wfGroup(g){ let rs=S.roots.filter(r=>r.rec===g).map((r,i)=>({r,i})); rs.sort((x,y)=>((x.r.done?1:0)-(y.r.done?1:0))||(g==='workflow.project'?prjRank(x.r.a)-prjRank(y.r.a):0)||x.i-y.i); return rs.map(x=>x.r); }
function wfList(){ // the narrow live list behind a sheet: chip, title, tag, id, when
  let h='';
  WF_GROUPS.forEach(g=>{ const rs=wfGroup(g), vis=rs.filter(r=>!r.more||S.wfAll[g]);
    h+='<section class="band" aria-label="'+g+'"><div class="section"><div class="sh"><h2>'+g+'</h2><span class="meta">'+(REC_DESC[g]?REC_DESC[g]+' · ':'')+rs.length+'</span></div>'+vis.map(r=>{
      const sel=S.sheet&&S.sheet.kind==='wf'&&S.sheet.id===r.a, tag=stageTag(r), cw=wfChip(r).replace(/<[^>]+>/g,'').toLowerCase();
      return '<div class="tr'+(sel?' sel':'')+'" role="row" data-row="wf:'+A(r.a)+'" tabindex="0" aria-label="'+A(r.a)+', '+cw+(tag?', '+A(tag):'')+'"'+(sel?' aria-selected="true"':'')+'>'+wfChip(r)+'<span class="tt"><span class="tx">'+E(r.subj)+'</span>'+(tag?'<span class="tag meta">'+E(tag)+'</span>':'')+(r.esc?'':'<span class="id at-rest mono">'+E(r.a)+'</span>')+'</span><span class="when">'+roundAge(r.age)+'</span></div>'; }).join('')+(rs.length>vis.length?'<div class="tr more" role="row"><span></span><span>… '+(rs.length-vis.length)+' more done</span><span></span></div>':'')+'</div></section>';
  });
  h+='<section class="band" aria-label="Other recipes"><div class="section"><div class="sh"><h2>Other recipes</h2><span class="meta">'+OTHER_RECIPES.length+'</span></div>'+OTHER_RECIPES.map(o=>'<div class="tr" role="row" aria-label="'+A(o.n+', '+o.stage.replace(/ every.*/,''))+'"><span></span><span class="tt"><span class="tx">'+o.n+'</span><span class="tag meta">'+o.tag+'</span></span><span class="when">'+o.when+'</span></div>').join('')+'</div></section>';
  return '<div class="qt" aria-label="Open workflows by recipe">'+h+'</div>';
}
function workflows(){
  const nA=wfNeeds(), nD=nYouRoots(), behind=!!S.sheet;
  const pops=S.pop==='start'?startPop():'';
  const startBtn=(kb)=>'<span class="px-popwrap"><button class="btn primary" data-act="start" aria-label="Start a workflow (Command N)" aria-haspopup="dialog" aria-expanded="'+(S.pop==='start')+'">Start…'+(kb?' '+kbd('⌘N'):'')+'</button>'+pops+'</span>';
  if(behind){
    const sub='<div class="sub"><span><b>'+S.roots.length+'</b> open</span><span>·</span><span class="attn-word"><i aria-hidden="true"></i>'+nA+' need you</span><span>·</span><span><b>'+nD+'</b> with you</span></div>';
    return wrapV('v-workflow-project','<main class="page behind"><div class="pagehead"><div style="display:flex;align-items:baseline;gap:14px"><h1>Workflows</h1>'+sub+'</div><div class="toolbar">'+startBtn(true)+'</div></div>'+banner()+wfList()+'</main>');
  }
  const head='<header class="pagehead"><div class="ph-l"><h1 class="page-title">Workflows</h1><p class="sub"><span>'+S.roots.length+' open</span><span>·</span><span class="attn-word">'+nA+' need you</span><span>·</span><span>'+nD+' with you</span></p></div><div class="toolbar"><button class="btn" aria-haspopup="listbox" aria-expanded="false" aria-label="Filter by recipe" data-act="filter" data-x="recipe">Recipe '+I.chev+'</button><button class="btn" aria-haspopup="listbox" aria-expanded="false" aria-label="Filter by subject" data-act="filter" data-x="subject">Subject '+I.chev+'</button><button class="btn ghost" data-act="keys" aria-label="Keyboard shortcuts">'+kbd('?')+' Shortcuts</button>'+startBtn(true)+'</div></header>';
  let body=''; WF_GROUPS.forEach(g=>{ body+=wfBandFull(g,wfGroup(g)); });
  body+='<section class="band" aria-labelledby="g-other"><div class="section"><div class="sh"><h2 id="g-other">Other recipes</h2><span class="meta">'+OTHER_RECIPES.length+'</span></div><div class="rows" role="list" aria-label="Other recipes">'+OTHER_RECIPES.map(o=>'<div class="row wr other" aria-label="'+A(o.aria)+'"><span></span><span></span><div class="t"><span class="row-title">'+o.n+'</span></div><span class="pb"><span class="stage">'+o.stage+'</span></span><span class="w"></span><span class="m now">'+o.now+'</span></div>').join('')+'</div></div></section>';
  return wrapV('v-workflows','<main class="page">'+head+banner()+body+'</main>');
}
function startPop(){
  const rec=S.forms.startRec||'workflow.item', inputs={'workflow.item':['item'],'workflow.project':['project'],'triage.alert':['payload'],'incident.response':['alert'],'builder.feature':['packet task']}; 
  const ok=S.recipes.filter(r=>r.st==='approved'&&!r.child);
  return '<div class="px-pop" role="dialog" aria-label="Start a workflow"><h3>Start a workflow</h3><p>Approved recipes only. Each one lists the inputs it declares.</p><div class="px-recs" role="radiogroup" aria-label="Recipe">'+ok.map(r=>'<button class="px-rec" role="radio" aria-checked="'+(r.n===rec)+'" data-act="start-rec" data-x="'+r.n+'">'+r.n+'<small>inputs: '+((inputs[r.n]||['input']).join(', '))+'</small></button>').join('')+'</div><label class="px-lbl">'+((inputs[rec]||['input'])[0])+'<input class="px-in" id="startin" placeholder="'+(rec==='workflow.item'?'ITM-… or a Linear id':'a reference')+'" value="'+A(S.note.start||'')+'"></label><div class="px-formrow"><button class="btn primary sm" data-act="start-go">Start</button><button class="btn sm" data-act="pop-close">Cancel</button></div></div>';
}
/* Work */
function workRow(x,maj){
  const sel=S.sheet&&S.sheet.kind==='item'&&S.sheet.id===x.id;
  const chip={esc:chipH('Escalated'),decision:chipH('Decision'),withyou:chipH('With you'),park:chipH('Parked'),running:chipH('Running'),queued:chipH('Queued')}[x.g];
  const lbl={esc:'Needs you now: '+x.id+', escalated gate',decision:'Needs you now: '+x.id+', needs a decision',withyou:'With you: '+x.id,park:'Parked: '+x.id+', '+x.reason,running:'Running: '+x.id,queued:'Queued: '+x.id}[x.g];
  return '<div class="tr'+(sel?' sel':'')+'" role="row" data-row="wk:'+x.id+'" tabindex="0" aria-label="'+A(lbl+(sel?', selected':''))+'"'+(sel?' aria-selected="true"':'')+'>'+chip+'<span class="tt"><span class="tx">'+E(x.title)+'</span>'+tagH(x,maj)+'<span class="id at-rest mono">'+x.id+'</span></span><span class="when">'+roundAge(x.age)+'</span></div>';
}
function work(){
  const f=S.wkFilter.toLowerCase(), pass=x=>!f||(x.id+' '+x.title+' '+x.repo).toLowerCase().includes(f);
  const G_=(g)=>S.items.filter(x=>g.includes(x.g)&&pass(x));
  const KO={park:0,decision:1,esc:2}; /* the order the mockup draws: parked, decision, escalated */
  const groups=[['Needs you',G_(['park','decision','esc']).sort((a,b)=>KO[a.g]-KO[b.g]),'attn'],['With you',G_(['withyou'])],['Running',G_(['running'])],['Queued',G_(['queued'])]];
  let h='';
  groups.forEach(g=>{ if(!g[1].length) return; const maj=majority(g[1]); h+='<section class="band'+(g[2]?' '+g[2]:'')+'" aria-label="'+g[0]+'"><div class="section"><div class="sh"><h2>'+g[0]+'</h2><span class="meta">'+g[1].length+'</span></div>'+g[1].map(x=>workRow(x,maj)).join('')+'</div></section>'; });
  if(!h) h='<section class="band"><div class="section"><div class="tr" role="row" style="display:block"><span class="tt muted">No item matches the filter.</span></div></div></section>';
  const wyc=inG('esc').length+inG('decision').length+inG('park').length;
  const sub='<div class="sub"><span><b>'+openItems().length+'</b> items</span><span>·</span><span class="attn-word"><i aria-hidden="true"></i>'+wyc+' need you</span><span>·</span><span><b>'+inG('withyou').length+'</b> with you</span><span>·</span><span><b>'+inG('running').length+'</b> running</span><span>·</span><span><b>'+inG('queued').length+'</b> queued</span></div>';
  const tools='<div class="tools"><label class="filter" role="search">'+I.search+'<input class="px-fin" id="wkfilter" aria-label="Filter by id, title, repo" placeholder="Filter by id, title, repo" value="'+A(S.wkFilter)+'">'+kbd('/')+'</label><span class="px-popwrap"><button class="btn" data-act="create" aria-haspopup="dialog" aria-expanded="'+(S.pop==='create')+'">Create item '+kbd('C')+'</button>'+(S.pop==='create'?createPop():'')+'</span></div>';
  return wrapV('v-item-sheet','<main class="page'+(S.sheet?' behind':'')+'"><div class="pagehead"><div style="display:flex;align-items:baseline;gap:14px"><h1>Work</h1>'+sub+'</div>'+tools+'</div>'+banner()+'<div class="qt" aria-label="Work items">'+h+'</div></main>');
}
function createPop(){ return '<div class="px-pop" role="dialog" aria-label="Create item"><h3>Create item</h3><label class="px-lbl">Title<input class="px-in" id="newtitle" placeholder="What should an agent do?" value="'+A(S.note.newtitle||'')+'"></label><label class="px-lbl">Repo<select class="px-in" id="newrepo"><option>agentic-workflow</option><option>sindri-app</option></select></label><div class="px-formrow"><button class="btn primary sm" data-act="create-go">Create item</button><button class="btn sm" data-act="pop-close">Cancel</button></div></div>'; }

/* Recipes */
function spark(t,l){ return '<span class="spark" role="img" aria-label="'+l+'">'+t.map(h=>'<i style="height:'+h+'%"></i>').join('')+'</span>'; }
const WIN={'7 days':.23,'30 days':1,'90 days':2.9};
function sc(n){ return Math.max(0,Math.round(n*WIN[S.recipeWin])); }
function recChip(r){ return chipH(r.st==='changed'?'Changed':r.st==='draft'?'Draft':'Approved'); }
const sparkWord=t=>t[t.length-1]>t[0]?'rising':t[t.length-1]<t[0]?'falling':'flat';
/* a spark's bar heights are the weekly values scaled to the biggest week; the label carries the numbers */
const sparkH=v=>{ const m=Math.max.apply(null,v)||1; return v.map(x=>Math.round(100*x/m)); };
const sparkN=(v,what,unit)=>spark(sparkH(v),what+', four weeks, '+sparkWord(v)+': '+v.map(x=>x+(unit||'')).join(' '));
/* the page's two formulas (fixture.md "Recipes: the numbers"): done without you % = dwy ÷ runs; human rate = interventions ÷ runs */
const dwyPct=r=>r.runs?Math.round(100*r.dwy/r.runs)+'%':'—';
const nInt=r=>r.int[0]+r.int[1]+r.int[2];
const humanRate=r=>r.runs?Math.round(100*nInt(r)/r.runs)+'%':'—';
const hwRates=r=>{ const hw=r.hw||r.wk.map(w=>Math.round(w*nInt(r)/r.runs)); return r.wk.map((w,i)=>w?Math.round(100*hw[i]/w):0); };
function recRowFull(r){
  const ch=r.st==='changed', dr=r.st==='draft';
  const dash='<span class="num n dash" role="cell">—</span>';
  const nums=r.runs==null?dash.repeat(5)+'<span class="sp" role="cell"><span class="dash">—</span></span>':'<span class="num n" role="cell">'+sc(r.runs)+'</span><span class="num n" role="cell">'+sc(r.fail||0)+'</span><span class="num n" role="cell">'+dwyPct(r)+'</span><span class="num n" role="cell">'+r.med+'</span><span class="num n" role="cell">'+r.tok+'</span><span class="sp" role="cell">'+sparkN(r.wk,'runs per week')+'</span>';
  const last=dr?'<span class="last" role="cell"><span class="meta">never approved</span></span>':'<span class="last" role="cell"><span class="num">'+r.last[0]+'</span><span class="num">'+r.last[1]+'</span></span>';
  const cli='sindri recipe approve '+r.n, key='rc:'+r.n;
  const act='<span class="act" role="cell">'+(ch?lockBtn('Re-approve · terminal',cli,key,true):'')+'</span>';
  const ex=S.revealed[key]?rx(cmdLine(cli,key)+noteWaits):'';
  return '<div class="row rrow rcols'+(dr?' draft':'')+'" tabindex="0" data-row="rc:'+r.n+'" role="row" aria-label="Recipe '+r.n+', '+(ch?'changed since approval':r.st)+'">'+recChip(r).replace('class="chip','role="cell" class="chip')+'<span class="row-title" role="cell"><a href="#recipe/'+r.n+'" data-act="open-sheet" data-kind="recipe" data-id="'+r.n+'">'+r.n+'</a>'+(r.child?'<span class="tag meta">child only</span>':'')+'</span>'+nums+last+act+ex+'</div>';
}
function recRowQ(r){
  const sel=S.sheet&&S.sheet.kind==='recipe'&&S.sheet.id===r.n, dr=r.st==='draft';
  return '<div class="row bl'+(sel?' sel':'')+'" role="row" data-row="rc:'+r.n+'" tabindex="0" aria-label="Recipe '+r.n+', '+r.st+'"'+(sel?' aria-selected="true"':'')+'>'+recChip(r)+'<span class="row-title">'+r.n+'</span><span class="meta">'+(dr?'no runs yet':'open '+(r.open||0)+' · '+(r.runs==null?'no runs yet':'runs '+sc(r.runs)))+'</span></div>';
}
function recipes(){
  const rs=S.recipes, tot=rs.reduce((a,r)=>a+(r.runs?sc(r.runs):0),0), dwyTot=rs.reduce((a,r)=>a+(r.runs?sc(r.dwy):0),0), totPct=Math.round(100*rs.reduce((a,r)=>a+(r.runs?r.dwy:0),0)/rs.reduce((a,r)=>a+(r.runs||0),0));
  const winBtn='<span class="px-popwrap"><button class="btn" data-act="win" aria-haspopup="listbox" aria-expanded="'+(S.menu==='win')+'" aria-label="Window: '+S.recipeWin+'">'+S.recipeWin+' '+I.chev+'</button>'+(S.menu==='win'?'<div class="menu" role="listbox" style="left:0;right:auto;width:150px">'+Object.keys(WIN).map(w=>'<button class="mi" role="option" aria-selected="'+(w===S.recipeWin)+'" data-act="win-set" data-x="'+w+'">'+w+'</button>').join('')+'</div>':'')+'</span>';
  const sub='<div class="sub">'+winBtn+'<span>·</span><span><b>'+tot+'</b> runs</span><span>·</span><span><b>'+totPct+'%</b> done without you</span></div>';
  const behind=!!S.sheet;
  const acts='<div class="r">'+(behind?'':'<div class="hints" aria-label="Keyboard shortcuts"><span>'+kbd('J')+kbd('K')+' Move</span><span>'+kbd('↵')+' Open</span><span>'+kbd('?')+' Shortcuts</span></div>')+'<span class="px-popwrap"><button class="btn" data-act="validate" aria-haspopup="dialog">Validate file…</button>'+(S.pop==='validate'?valPop():'')+'</span><span class="px-popwrap"><button class="btn" data-act="import" aria-haspopup="dialog">Import</button>'+(S.pop==='import'?impPop():'')+'</span></div>';
  const head='<div class="pagehead"><div style="display:flex;align-items:baseline;gap:14px"><h1 class="page-title">Recipes <span class="count">'+rs.length+'</span></h1>'+sub+'</div>'+acts+'</div>';
  if(behind) return wrapV('v-recipe-inspector','<main class="page behind">'+head+banner()+'<section class="band" aria-label="Recipes, '+S.recipeWin+'">'+rs.map(recRowQ).join('')+'</section></main>');
  return wrapV('v-recipes','<main class="page">'+head+banner()+'<section class="band rtable" role="table" aria-label="Recipes, '+S.recipeWin+'"><div class="rhead rcols meta" role="row"><span role="columnheader">Status</span><span role="columnheader">Recipe</span><span class="n" role="columnheader">Runs</span><span class="n" role="columnheader">Failed</span><span class="n" role="columnheader">Done without you</span><span class="n" role="columnheader">Median</span><span class="n" role="columnheader">Tokens/run (p50)</span><span class="n" role="columnheader">4 weeks</span><span role="columnheader">Last change</span><span class="sr" role="columnheader">Action</span></div>'+rs.map(recRowFull).join('')+'<p class="meta footnote">Runs, Failed, Done without you, Median and Tokens/run cover the last '+S.recipeWin+' · Failed = ended in a terminal failure state (cancelled runs show in the inspector) · Done without you = runs finished with no park answered, no escalation decided and no takeover, ÷ runs; the page total is the run-weighted mean, '+dwyTot+' of '+tot+' · Tokens/run is the p50 · 4 weeks = runs per week, oldest first</p></section></main>');
}
function valPop(){ const r=S.forms.val; return '<div class="px-pop" role="dialog" aria-label="Validate file"><h3>Validate file</h3><p>Runs the validator in-process. Nothing is registered.</p><label class="px-lbl">File<input class="px-in" id="valin" value="'+A(S.note.val||'recipes/tabs.layout.yaml')+'"></label>'+(r?'<div class="px-res '+(r.ok?'ok':'bad')+'" role="status">'+(r.ok?G('h-ok','✓')+'valid · '+r.msg:G('h-fail','✕')+r.msg)+'</div>':'')+'<div class="px-formrow"><button class="btn primary sm" data-act="val-go">Validate</button><button class="btn sm" data-act="pop-close">Close</button></div></div>'; }
function impPop(){ return '<div class="px-pop" role="dialog" aria-label="Import recipe"><h3>Import</h3><p>Registers a file as a draft. A draft never runs until it is approved at a terminal.</p><label class="px-lbl">File<input class="px-in" id="impin" value="'+A(S.note.imp||'recipes/release.notes.yaml')+'"></label><div class="px-formrow"><button class="btn primary sm" data-act="imp-go">Import</button><button class="btn sm" data-act="pop-close">Cancel</button></div></div>'; }

/* ───────── Agents and System (the calm pass) ───────── */
function meter(k,cap){ const p=Math.round(k/cap*100); return '<span class="meter tok" role="img" aria-label="'+k+'k of '+cap+'k tokens"><span class="n">'+k+'k</span><span class="trk"><span class="fil" style="width:'+p+'%"></span></span><span class="n dash">'+cap+'k</span></span>'; }
const agMore=(id,label,inner)=>{ const m=S.menu==='agm:'+id; return '<span class="mwrap"><button class="btn more" data-act="menu" data-id="agm:'+A(id)+'" aria-label="'+A(label)+'" aria-haspopup="menu" aria-expanded="'+m+'">'+I.chev+'</button>'+(m?'<div class="menu" role="menu" aria-label="'+A(label)+'">'+inner+'</div>':'')+'</span>'; };
function agRow(x){
  const id=x.id, you=x.g==='withyou', wait=(x.g==='park'||x.g==='esc'), term=x.term;
  const chip=you?chipH('With you'):wait?chipH(x.g==='park'?'Parked':'Escalated'):chipH('Running');
  const tag=wait?(x.g==='park'?x.reason:'gate '+x.score):(x.agent||'').replace(' in Warp','');
  const idl=wait?id:x.sess+' · '+id;
  const ttl='<span class="id at-rest mono">'+idl+'</span><span class="row-title"><span class="ttl">'+E(x.title)+'</span><span class="tag">'+E(tag)+'</span></span>';
  const aria=(you?'With you':wait?(x.g==='park'?'Parked':'Escalated'):'Running')+': '+(wait?id+', no session':(x.sess||'')+' on '+id);
  const open='<div class="row ag" data-row="ag:'+id+'" tabindex="0" aria-label="';
  if(S.confirm==='cancel:'+id) return open+A(aria)+', cancel asked">'+chip+ttl+'<div class="confirm" role="alertdialog" aria-label="Cancel '+id+'?"><span>Cancel <b class="mono">'+id+'</b>? '+(x.step==='checks'?'Its checks stop.':'Its session stops.')+'</span><button class="btn danger" data-act="confirm-yes" data-x="cancel:'+id+'">Cancel item</button><button class="btn" data-act="confirm-no" data-x="cancel:'+id+'" data-first title="Esc keeps it">Keep</button></div></div>';
  let mid='<span></span>', w='<span class="num w">'+x.age+'</span>', acts='', extra='';
  if(wait){
    const cli=x.g==='park'?'sindri workflow answer '+id+' --grant 1':'sindri workflow answer '+id+' --accept', k='ag:'+id;
    const first=x.g==='park'?(x.reason==='needs-answer'?'<button class="btn primary" data-act="open-sheet" data-id="'+id+'" data-x="answer">Answer</button>':lockBtn('Grant rounds · terminal',cli,k)):lockBtn('Accept · terminal',cli,k);
    acts=first+agMore(id,'More for '+id,(x.g==='esc'?mItem('Reject','reject',id):'')+mCap('Open in')+mOpenIn(x)+mCap('Copy command')+mCopy(cli,k)+mCancel(id));
    extra=S.revealed[k]?rx(cmdLine(cli,k)+noteWaits):'';
  } else {
    mid=meter(x.tok||0,200);
    const att=S.attached[id], attBtn=att?'<span class="px-att" role="status">'+G('g-live','●')+'Attached in Warp</span>':'<button class="btn'+(you?' primary':'')+'" data-act="attach" data-id="'+id+'" aria-label="Attach to '+x.sess+'">'+I.warp+'Attach</button>';
    const cap=mCap((x.tok||0)+'k of 200k tokens'+(x.round?' · round '+x.round:''));
    if(you) acts=attBtn+'<button class="btn" data-act="handback" data-id="'+id+'">Hand back</button>';
    else if(term) acts=attBtn+agMore(id,'More for '+x.sess,cap+mItem('File a request','req-open',id,null,'a note for its next turn','R')+mItem('Take over','takeover',id,null,'yours until you hand back','T')+mItem('Cancel…','ask-cancel',id,null,'asks first','X'));
    else acts='<button class="btn" data-act="open-sheet" data-id="'+id+'" aria-label="Open instance '+id+'">Open instance</button>'+agMore(id,'More for '+x.sess,cap+mItem('Open instance','open-sheet',id)+mItem('Cancel…','ask-cancel',id,null,'asks first','X'));
  }
  const reqf=S.forms['req:'+id]?rx('<div class="px-confirm inl"><input class="px-in" id="reqin" aria-label="Request text" placeholder="A note for the session\'s next turn" value="'+A(S.note['req:'+id]||'')+'"><span class="acts"><button class="btn sm primary" data-act="req-send" data-id="'+id+'">File request</button><button class="btn sm" data-act="req-open" data-id="'+id+'">Cancel</button></span></div>'):'';
  return open+A(aria)+'">'+chip+ttl+mid+w+'<div class="acts">'+acts+'</div>'+extra+reqf+'</div>';
}
function agents(){
  const you=inG('withyou'), lv=liveSessions(), wait=inG('park').concat(inG('esc'));
  const sub='<div class="sub"><span><b>'+lv.length+'</b> live</span><span>·</span><span><b>'+you.length+'</b> with you</span><span>·</span><span class="attn-word"><i aria-hidden="true"></i>'+wait.length+' need you</span><span>·</span><span><b>$4.12</b> today</span></div>';
  const head='<div class="pagehead"><div style="display:flex;align-items:baseline;gap:14px"><h1 class="page-title">Agents</h1>'+sub+'</div><div class="r"><div class="hints" aria-label="Keyboard shortcuts"><span>'+kbd('J')+kbd('K')+' Move</span><span>'+kbd('↵')+' Details</span><span>'+kbd('O')+' Attach</span><span>'+kbd('?')+' Shortcuts</span></div><button class="btn" data-act="budgets">Budgets</button></div></div>';
  const groups={}; const ORD=['ITM-01JA1','ITM-01JD9','ITM-01JE7','ITM-01JE9','ITM-01JD6']; you.concat(lv).sort((a,b)=>ORD.indexOf(a.id)-ORD.indexOf(b.id)).forEach(x=>{ (groups[x.grp||'workflow.item › produce']=groups[x.grp||'workflow.item › produce']||[]).push(x); });
  let h='';
  [['workflow.item › produce','step.produce · agent Step'],['workflow.item › checks','step.checks · tool Step, no terminal: no attach, no takeover']].forEach((g,i)=>{ const rs=groups[g[0]]||[]; if(!rs.length) return;
    h+='<section class="band" aria-label="'+g[0]+'"><div class="section"><div class="sh"><h2>'+g[0]+'</h2><span class="meta">'+g[1]+'</span></div>'+rs.map(agRow).join('')+(i===0?'<p class="meta attach-note">'+ATTACH_RULE+'</p>':'')+'</div></section>'; });
  if(!h) h='<section class="band"><div class="section"><div class="sh"><h2>Nothing running</h2><span class="meta">'+inG('queued').length+' queued; the next starts on its own.</span></div></div></section>';
  const model='<div class="row ag" data-row="ag:model" tabindex="0" aria-label="Model run: challenger on PRJ-tabs">'+chipH('Model')+'<span class="id at-rest mono">PRJ-tabs</span><span class="row-title"><span class="ttl">Tab manager v2</span><span class="tag">challenger</span></span>'+meter(84,600)+'<span class="num w">46m</span><div class="acts"><button class="btn" data-act="open-sheet" data-kind="wf" data-id="PRJ-tabs" aria-label="Open instance PRJ-tabs">Open instance</button>'+agMore('model','More for the PRJ-tabs model run',mCap('84k of 600k tokens · round 2/3')+mItem('Open instance','open-sheet','PRJ-tabs'))+'</div></div>';
  h+='<section class="band" aria-label="scoping.project › challenge"><div class="section"><div class="sh"><h2>scoping.project › challenge</h2><span class="meta">step.scope.challenge · model call, no session: no attach, no takeover</span></div>'+model+'</div></section>';
  if(wait.length) h+='<section class="band attn" aria-label="Needs you"><div class="section"><div class="sh"><h2>Needs you</h2><span class="count-attn">'+wait.length+'</span></div>'+wait.map(agRow).join('')+'</div></section>';
  const bud=(k,v,cap,w,extra)=>'<span class="k">'+k+'</span><span class="meter" role="img" aria-label="$'+v.toFixed(2)+' / $'+cap.toFixed(2)+(k==='Cursor'?', cursor not found on this host':'')+'"><span class="trk"><span class="fil" style="width:'+w+'%"></span><span class="mk" style="left:80%"></span><span class="mk h" style="right:0"></span></span></span><span class="v">$'+v.toFixed(2)+' / $'+(extra?cap:cap.toFixed(2))+(extra||'')+'</span>';
  const rowsBA=[['Claude Code',96,88,'9%','48m','290k','$1.70'],['Codex',41,39,'2%','11m','40k','$0.22'],['Cursor',3,2,'33%','1h 05m','180k','$0.98'],['challenger',7,7,'0%','14m','110k','$0.41']].map(r=>'<div class="row ba"><span class="row-title">'+r[0]+'</span>'+r.slice(1).map(v=>'<span class="num n">'+v+'</span>').join('')+'</div>').join('');
  const by='<div class="section" aria-labelledby="by-h"><div class="sh"><h2 id="by-h">By agent</h2><button class="btn" aria-haspopup="listbox" aria-expanded="false" aria-label="Window: 30 days" data-act="win-ag">30 days '+I.chev+'</button></div><div role="table" aria-label="By agent, 30 days"><div class="ba bahead meta" role="row"><span>Agent</span><span class="n">Sessions</span><span class="n">Done</span><span class="n">Taken over</span><span class="n">Median</span><span class="n">Tokens/session</span><span class="n">$/session</span></div>'+rowsBA+'</div></div>';
  const bg='<div class="section" id="budget" aria-labelledby="bg-h" tabindex="-1"><div class="sh"><h2 id="bg-h">Budget</h2><span class="meta">today</span></div><div class="bud">'+bud('Claude Code',3.10,8,39)+bud('Codex',1.02,4,26)+bud('Cursor',0,2,0)+bud('total',4.12,14,29,'<small>soft 80%</small>')+'<span class="key"><span><i aria-hidden="true">│</i> soft warning at 80%</span><span><i class="h" aria-hidden="true">│</i> hard stop at 100%: new sessions refused (SND-LAUNCH-010)</span></span></div></div>';
  h+='<section class="band" aria-label="By agent and budget"><div class="two">'+by+bg+'</div></section>';
  return wrapV('v-agents'+(S.sheet?' narrowish':''),'<main class="page">'+head+banner()+h+'</main>');
}
/* ── System ── */
const ENG=[['ITM-01JE7','workflow.item@3f1c09','ok','Running','produce r2/3','activity <span class="mono">step.produce</span> (async, 6m)','timeout 1d 23h','0','412 ev'],['ITM-01JD9','workflow.item@3f1c09','ok','Running','produce r1/3','activity <span class="mono">step.produce</span> (async, 1m)','timeout 1d 23h','0','36 ev'],['ITM-01JE9','workflow.item@3f1c09','ok','Running','checks','activity <span class="mono">step.checks</span> (2m)','produce timeout 12:30','1 · exit 1','88 ev'],['ITM-01JD6','workflow.item@3f1c09','ok','Running','checks','activity <span class="mono">step.checks</span> (0m)','produce timeout 12:56','0','24 ev'],['ITM-01JC8','workflow.item@3f1c09','attn','Parked','no-progress','signal <span class="mono">answer</span> (grant)','none','','640 ev'],['ITM-01JB4','workflow.item@3f1c09','attn','Escalated','gate','signal <span class="mono">answer</span> (accept/reject)','escalate timeout fired','','301 ev'],['PRJ-tabs','workflow.project@a8c2','','Child','scoping','child <span class="mono">PRJ-tabs/scoping/1</span>','parks in 1h 14m','0','57 ev'],['INC-14','incident.response@5d9','ok','Running','mitigate r1/3','child <span class="mono">ITM-01JG2</span> · activity','escalates in 36m','0','120 ev']];
function histRows(id){
  if(id==='ITM-01JE9') return [['12:26:10','ActivityTaskScheduled','step.checks','attempt 2 · retry after exit 1 ("checks runner: ECONNRESET") · backoff 10 s',''],['12:26:00','ActivityTaskFailed','step.checks','attempt 1 · exit 1',''],['12:25:50','ActivityTaskStarted','step.checks','worker sindri-48211',''],['12:25:49','TimerStarted','produce-timeout','fires 12:30','ledger ↔ <a href="#" data-act="ext" data-x="ledger">4231</a> step.round-started'],['12:24:01','WorkflowExecutionUpdateAccepted','','takeover? no · complete (run 1.1, outcome done)','ledger ↔ <a href="#" data-act="ext" data-x="ledger">4228</a> step.completed']];
  const e=ENG.find(r=>r[0]===id)||ENG[0]; const nm=e[5].match(/step\.\w+/); const st=nm?nm[0]:'answer';
  return [['12:26:02','ActivityTaskStarted',st,'worker sindri-48211',''],['12:25:40','TimerStarted',e[6].replace('timeout ','')||'timer','scheduled','ledger ↔ <a href="#" data-act="ext" data-x="ledger">'+(4200+id.length*7)+'</a> step.round-started'],['12:24:11','ActivityTaskScheduled',st,'attempt 1',''],['12:24:10','WorkflowTaskCompleted','','decided: next '+e[4],''],['12:20:00','WorkflowExecutionStarted','','recipe '+e[1],'ledger ↔ <a href="#" data-act="ext" data-x="ledger">4180</a> instance.started']];
}
function sysGraph(){
  let g=TPL.sysgraph;
  if(S.svc.index.state==='ok') g=g.replace('<tspan class="fail">✕</tspan> code index','<tspan class="ok">✓</tspan> code index').replace(/<text class="el m" x="436" y="122"><tspan class="fail">✕<\/tspan> 03:00 failed<\/text>/,'<text class="el m" x="468" y="122">12:27 built</text>').replace('code index, failed at 03:00.','code index, ok.');
  if(S.sync.onyx) g=g.replace('<tspan class="deg">◐</tspan> Onyx 3000','<tspan class="ok">✓</tspan> Onyx 3000').replace(/<text class="el m" x="140" y="122">2h behind<\/text>/,'').replace('Onyx 3000, degraded, 2h behind.','Onyx 3000, ok.');
  return g.replace(/<a href="#" class="gn" aria-label="([^"]*?)\. Open its doctor check">/g,(m,l)=>'<a href="#" class="gn" data-act="doctor" data-id="'+A(l.split(',')[0].trim())+'" aria-label="'+l+'. Open its doctor check">');
}
function impact(){
  const L=[]; const ix=S.svc.index.state!=='ok';
  const jr=S.jobs.index&&!S.jobs.index.done;
  if(ix) L.push('<div class="imp"><h3>'+chipH('Failed')+'code index <span class="dim">nightly 03:00</span></h3><ul><li><span>context: symbols lens stale since 10-12</span></li><li><span>scoping: the <code>code</code> source reads the old index</span></li><li><span>builder.feature: <span class="mono">input.context.retrieve</span> degraded (cited results only)</span></li></ul><div class="note">nothing parks; 2 open instances run degraded</div>'+(S.jobs.index?jobLines('index'):'')+'<div class="acts"><button class="btn" disabled title="Available with API P6 · sindri services run index">Run again</button><span class="pending">Available with API P6</span></div><div class="cmd"><span class="prompt" aria-hidden="true">$</span><code>sindri services run index</code><button data-act="copy" data-cli="sindri services run index" data-x="svcrun-sys" aria-label="Copy command: sindri services run index">'+(S.copied==='svcrun-sys'?'Copied':'Copy')+'</button></div></div>');
  if(!S.sync.onyx){ const jr2=S.jobs.onyx&&!S.jobs.onyx.done; L.push('<div class="imp"><h3>'+chipH('Degraded')+'Onyx sync <span class="dim">2h behind</span></h3><ul><li><span>intent lens: Slack and Linear comments may be stale; the native Linear adapter is unaffected</span></li></ul>'+(S.jobs.onyx?jobLines('onyx'):'')+'<div class="acts"><button class="btn primary" data-act="sync-onyx"'+(jr2?' disabled':'')+'>'+(jr2?'Syncing…':'Sync now')+'</button></div></div>'); }
  L.push('<div class="imp"><h3>'+chipH('Absent')+'cursor <span class="dim">not found</span></h3><ul><li><span>launch target hidden in every Open ▾ menu</span></li><li><span>no run affected</span></li></ul></div>');
  return {html:L.join(''),n:L.length};
}
function system(){
  const ix=S.svc.index.state!=='ok', im=impact(), sel=S.sysSel||'ITM-01JE9';
  const sum=healthSentence().replace(/<[^>]+>/g,'')+' '+S.roots.length+' instances open · '+liveSessions().length+' activities running.';
  const head='<div class="pagehead"><div><h1 class="page-title">System</h1><p class="pagesum">'+sum+'</p></div></div>';
  const eng=ENG.map(r=>'<div class="en'+(r[0]===sel?' sel':'')+'" data-row="en:'+r[0]+'" tabindex="0"'+(r[0]===sel?' aria-current="true"':'')+'><span class="id at-rest">'+r[0]+'</span><span class="mono">'+r[1]+'</span><span class="st">'+chipH(r[3])+r[4]+'</span><span class="wait">'+r[5]+'</span><span class="tim">'+r[6]+'</span><span class="rt">'+(r[7]===''?'<span class="null">—</span>':r[7])+'</span><span class="hist"><a href="#" data-act="hist" data-id="'+r[0]+'">'+r[8]+'</a></span></div>').join('');
  const hrows=histRows(sel), tot=(ENG.find(r=>r[0]===sel)||ENG[0])[8].split(' ')[0];
  const hist=hrows.map(h=>'<div class="hr"><span class="ts">'+h[0]+'</span><span class="ev">'+h[1]+'</span><span class="tg">'+h[2]+'</span><span class="dt">'+h[3]+'</span><span class="led">'+h[4]+'</span></div>').join('');
  const svcOpen=S.open.svc;
  const svcRows=[['ledger.db','ok','wal · head 4236'],['views','ok','4236 (0 behind)'],['engine','ok','Temporal dev 7233'],['launcher','ok','Warp, claude, codex'],['context','ok','map v13'],['index',S.svc.index.state==='ok'?'ok':'failed',S.svc.index.state==='ok'?'built 12:27':'nightly 03:00 · SND-INDEX-012']];
  const svc='<section class="band last svc" aria-labelledby="svc-h"><div class="section"><div class="sh"><button class="disc" data-act="svc-toggle" aria-expanded="'+!!svcOpen+'" aria-label="'+(svcOpen?'Collapse':'Expand')+' the services table and the process, views and profile strip">'+(svcOpen?'▾':'▸')+'</button><h2 id="svc-h">Services</h2><span class="sum">'+chipH('OK')+'<span class="meta">'+(ix?5:6)+'</span>'+(ix?chipH('Failed')+'<span class="meta">1 (index)</span>':'')+'</span><span class="meta">process <span class="mono">sindri-48211</span> · views <b>'+(S.conn==='normal'?'2s ago':'stale')+'</b> · profile <b>'+(S.profile.changed?'changed</b> since approval, 2 keys':'approved</b>')+'</span></div>'+(svcOpen?svcRows.map(r=>'<div class="en" style="grid-template-columns:140px 100px 1fr auto"><span class="id keep">'+r[0]+'</span><span class="st">'+chipH(r[1]==='ok'?'OK':'Failed')+'</span><span class="mono" style="color:var(--ink-muted)">'+r[2]+'</span>'+(r[1]==='failed'?'<button class="btn sm" disabled title="Available with API P6 · sindri services run index">Run again</button>':'<span></span>')+'</div>').join(''):'')+'</div></section>';
  return wrapV('v-system','<main class="page">'+head+banner()+'<div class="bands"><section class="band" aria-label="Dependencies and impact"><div class="cols"><div class="section" aria-labelledby="dep-h"><div class="sh"><h2 id="dep-h">Dependencies</h2><span class="meta">what depends on what · health on the edge</span></div><div class="graphbody">'+sysGraph()+'<p class="gnote">Open a node for its doctor check and fix. The graph is drawn from deps.yaml and the profile\'s sources.</p></div></div><div class="section impact" aria-labelledby="imp-h"><div class="sh"><h2 id="imp-h">Impact</h2><span class="meta">of the failures now · '+im.n+'</span></div>'+im.html+'</div></div></section>'+
   '<section class="band" aria-labelledby="eng-h"><div class="section"><div class="sh"><h2 id="eng-h">Engine</h2><span class="meta"><span class="mono">Temporal dev 7233 · namespace sindri</span></span></div><div class="worker"><span>worker <span class="mono">1</span> · <span class="mono">sindri-48211</span></span><span>·</span><span>task queue <span class="mono">sindri</span></span><span>·</span>'+chipH('Polling')+'<span>·</span><span>activities <span class="mono">'+liveSessions().length+'</span> running · <span class="mono">0</span> backlog</span><span>·</span><span>last heartbeat <span class="mono">1s</span></span><span class="r">mirror lag <span class="mono">0 events (0 ms)</span></span></div><div class="en-h"><span>Instance</span><span>Recipe@hash</span><span>State</span><span>Waiting on</span><span>Timers</span><span>Retries</span><span style="text-align:right">History</span></div>'+eng+'<div class="en more"><span></span><span style="color:var(--ink-subtle)">… 4 more open</span></div><div class="pend-line"><span>pending</span><span>updates <span class="mono">0</span> · signals <span class="mono">0</span> · stuck &gt; 5 min: <span class="mono">0</span> · <span title="Temporal: a workflow that restarted its own history to stay small">continue-as-new</span>: <span class="mono">0</span></span><span class="r"><span>dev server up <span class="mono">3d 4h</span> · temporal.db <span class="mono">48 MB</span></span><a class="btn sm" href="#" data-act="ext" data-x="Temporal UI" aria-label="Open Temporal UI, opens the dev server\'s loopback UI">Open Temporal UI ↗</a></span></div></div></section>'+
   '<section class="band" aria-labelledby="hist-h"><div class="section"><div class="sh"><h2 id="hist-h"><span class="id keep">'+sel+'</span> history</h2><span class="meta">last '+hrows.length+' of '+tot+' · newest first · each row names the ledger sequence it mirrored into</span></div>'+hist+'<div class="hr more"><span>… '+(parseInt(tot)-5)+' earlier events</span><span><a class="btn sm" href="#" data-act="ext" data-x="Temporal UI" aria-label="Full history, opens in the Temporal UI">Full history ↗</a></span></div></div></section></div>'+svc+'</main>');
}

/* ───────── Context ───────── */
const CN={
 'int-eng482':['ENG-482','Linear issue','record · synced 2m ago','"Restore panes per window"'],
 'int-prd':['PRD-tabs.md §3','Linear doc section','record · synced 2m ago','section 3 · pane restore'],
 'int-slack':['#tabs-design 09-30','Slack thread','record · 2h behind','thread of 14 messages'],
 'int-eng301':['ENG-301','Linear issue','record · <span class="tag">⚠ orphaned</span> its node left the map in v12','"Per-workspace pane restore"'],
 'int-decision':['decision 10-02','decision','proposed · from ENG-482','"panes restore per window, not per workspace"'],
 'prod-layout':['feature:layout','feature','confirmed · map v13 · confidence <span class="mono">0.99</span>','PRD-tabs §2'],
 'prod-split':['feature:layout.split-pane','feature · part of <span class="mono">feature:layout</span>','confirmed · map v13 · confidence <span class="mono">0.96</span>','ENG-482 · PRD-tabs §3'],
 'prod-anchor':['feature:tabs.tab-manager','feature · part of <span class="mono">feature:layout</span>','confirmed · map v13 · confidence <span class="mono">0.97</span>','ENG-482 · PRD-tabs §3 · #tabs-design 09-30 · decision 10-02 <span class="tag">⚠ 1 conflict</span>'],
 'prod-pane':['thing:pane-layout','thing','confirmed · map v13 · confidence <span class="mono">0.95</span>','PRD-tabs §3'],
 'prod-restore':['action:tab-manager.restore-layout','action · part of <span class="mono">feature:tabs.tab-manager</span>','confirmed · map v13 · confidence <span class="mono">0.94</span>','ENG-482'],
 'prod-flow':['flow:open-tab-split-resize','flow','confirmed · map v13 · confidence <span class="mono">0.91</span>','#tabs-design 09-30'],
 'prod-splitv':['action:split-pane.split-vertical','action','confirmed · map v13 · confidence <span class="mono">0.93</span>','PRD-tabs §3'],
 'prod-cmd':['surface:command/split-vertical','surface','confirmed · map v13 · confidence <span class="mono">0.92</span>','PRD-tabs §3'],
 'prod-session':['feature:session.restore','feature · dependency','model-suggested · confidence <span class="mono">0.74</span>','no intent cites it'],
 'code-splitpane':['src/layout/SplitPane.ts','file · map v13','implements <span class="mono">action:split-pane.split-vertical</span>','symbols setOrientation, split'],
 'code-manager':['src/tabs/Manager.ts','file · map v13','implements <span class="mono">action:tab-manager.restore-layout</span>','symbol restoreLayout'],
 'code-test':['tests/tabs/restore.test.ts','test · map v13','tests <span class="mono">action:tab-manager.restore-layout</span>','1 test file'],
 'code-panestore':['src/layout/paneStore.ts','file · map v13','writes and reads <span class="mono">thing:pane-layout</span>','symbols write, read'],
 'code-session':['src/session/restore.ts','file · <span class="tag">◇ suggested, unconfirmed</span>','model-suggested · confidence <span class="mono">0.74</span>','the file calls Manager.restoreLayout'],
};
const CEDGES=[['int-decision','prod-anchor','decides'],['int-prd','prod-anchor','mentions'],['int-slack','prod-flow','mentions'],['int-eng482','int-decision','decides'],['prod-layout','prod-split','part-of'],['prod-layout','prod-anchor','part-of'],['prod-split','prod-pane','writes'],['prod-anchor','prod-pane','reads'],['prod-anchor','prod-restore','part-of'],['prod-restore','prod-flow','step-of'],['prod-splitv','prod-pane','writes'],['prod-splitv','prod-cmd','on'],['code-splitpane','prod-splitv','implements'],['code-manager','prod-restore','implements · tests'],['code-test','prod-restore','tests'],['code-session','prod-session','implements (model-suggested 0.74)']];
function nodeCard(){
  const id=S.ctxNode, n=CN[id]||CN['prod-anchor']; const edges=CEDGES.filter(e=>e[0]===id||e[1]===id);
  const other=e=>e[0]===id?e[1]:e[0];
  let cas=edges.filter(e=>!/reads|writes/.test(e[2])), rw=edges.filter(e=>/reads|writes/.test(e[2]));
  if(id==='prod-anchor') cas=cas.filter(e=>e[0]==='prod-anchor');
  const li=e=>'<li><span class="d">d1</span><button class="key px-link" data-act="node" data-id="'+other(e)+'">'+E(CN[other(e)][0])+'</button><span class="via">'+e[2]+'</span></li>';
  const extra=id==='prod-anchor'?'<li><span class="d">d1</span><button class="key px-link" data-act="node" data-id="prod-flow">flow:open-tab-split-resize</button><span class="via">step-of</span></li><li><span class="d">d2</span><button class="key px-link" data-act="node" data-id="prod-session">feature:session.restore</button><span class="via">dep · model-suggested</span></li>':'';
  const mid='ncm:'+id, nm=S.menu===mid;
  const menu=nm?'<div class="menu" role="menu" aria-label="More for the node">'+mItem('Path to…','ctx-stub','', 'Path to…')+mItem('Query','ctx-stub','','Query')+mItem('Edit','ctx-stub','','Edit','writes a map_override row')+'</div>':'';
  return '<div class="section node-card" aria-labelledby="node-h"><div class="sh"><h2 id="node-h">Node</h2><span class="mono" style="color:var(--ink)">'+(id==='prod-anchor'?'▣ ':'')+E(n[0])+'</span></div><div class="nc"><dl class="kv"><dt>kind</dt><dd>'+n[1]+'</dd><dt>provenance</dt><dd>'+n[2]+'</dd><dt>evidence</dt><dd>'+n[3]+'</dd><dt>counts</dt><dd>'+(id==='prod-anchor'?'own scope <span class="mono">7</span> · affects <span class="mono">3</span> at depth ≤ 2 · <span class="mono">2</span> files · <span class="mono">4</span> symbols · <span class="mono">1</span> test':'<span class="mono">'+edges.length+'</span> neighbours · own scope <span class="mono">1</span>')+'</dd></dl><h3>cascade · '+(cas.length+(extra?2:0))+' at depth ≤ 2</h3><ul class="cas">'+(cas.map(li).join('')+extra||'<li><span class="d"></span><span class="key">none</span></li>')+'</ul><h3>reads · writes</h3><ul class="cas">'+(rw.map(li).join('')||'<li><span class="d"></span><span class="key">none</span></li>')+'</ul><div class="acts"><span class="split"><button class="btn main" data-act="node-sheet" data-id="'+id+'">Neighbors</button>'+chevBtn(mid,'Path to, Query, Edit',{solid:true})+menu+'</span></div></div></div>';
}
function mrRow(chip,cls,key,meta,aria,acts,row){ return '<div class="mr"'+(row?' data-row="'+A(row)+'" tabindex="0"':'')+' aria-label="'+A(aria)+'">'+chipH(chip,cls)+'<div class="t"><span class="keyt">'+key+'</span><div class="meta">'+meta+'</div></div><span class="split">'+acts+'</span></div>'; }
function mrBlock(){
  const c=S.ctx, p=c.props[0], o=c.orphans[0]; let h='';
  const detMeta='rename 12 · move 9 · implements 10, from the file map and symbol index';
  h+=mrRow('Deterministic','',c.det?c.det+' proposals':'0 proposals · '+c.detDone+' accepted 12:26',detMeta,c.det?'Deterministic proposals: '+c.det+', rename 12, move 9, implements 10':'Deterministic proposals: 0 pending, '+c.detDone+' accepted at 12:26','<button class="btn sm main" data-act="ctx-stub" data-x="Review deterministic proposals"'+(c.det?'':' disabled')+'>Review</button>'+chevBtn('mrd','More',{solid:true,sm:true})+menuBox('mrd','More for deterministic proposals',mItem('Accept all deterministic','accept-det','')),'');
  if(S.toast) h+='<div class="toast" role="status"><span class="g h-ok" aria-hidden="true">✓</span><span>'+S.toast.n+' deterministic proposals accepted · '+S.toast.n+' <span class="mono">map_override</span> rows written</span><button class="btn sm" data-act="undo-det">Undo</button><span class="dim">undo stays open for 10 s</span></div>';
  if(p){ const f=S.forms['mr:'+p.id];
    const acts=f?'<input class="px-in" id="mrin" aria-label="'+(f==='rename'?'New key':'New parent')+'" value="'+A(p.key)+'" style="width:210px"><button class="btn sm primary" data-act="mr-do" data-id="'+p.id+'" data-x="'+f+'-ok">'+(f==='rename'?'Rename':'Move')+'</button><button class="btn sm" data-act="mr-do" data-id="'+p.id+'" data-x="cancel">Cancel</button>':
      '<button class="btn sm main" data-act="mr-do" data-id="'+p.id+'" data-x="accept">Accept</button>'+chevBtn('mrm:'+p.id,'Reject, Rename, Move, Skip',{solid:true,sm:true})+menuBox('mrm:'+p.id,'More for '+p.file,mItem('Reject','mr-do',p.id,'reject')+mItem('Rename…','mr-do',p.id,'rename')+mItem('Move…','mr-do',p.id,'move')+mItem('Skip','mr-do',p.id,'skip','','S'));
    h+=mrRow('Model','',E(p.file)+'<span class="arr">→</span>'+E(p.key),'implements · '+p.conf+' · impact '+p.impact+' · '+E(p.reason),'Model proposal: implements, '+p.file+' to '+p.key+', '+p.conf,acts,'mr:'+p.id);
    if(c.props.length>1) h+='<div class="mr more">'+(c.props.length-1)+' more model proposals, by impact</div>'; }
  else h+='<div class="mr more">No model proposals left.</div>';
  if(o){ const f=S.forms['or:'+o.id];
    const acts=f?'<input class="px-in" id="orin" aria-label="Repoint to" placeholder="feature:…" value="feature:session.restore" style="width:210px"><button class="btn sm primary" data-act="or-do" data-id="'+o.id+'" data-x="repoint">Repoint</button><button class="btn sm" data-act="or-do" data-id="'+o.id+'" data-x="cancel">Cancel</button>':
      '<button class="btn sm main" data-act="or-do" data-id="'+o.id+'" data-x="repoint-form">Repoint…</button>'+chevBtn('orm:'+o.id,'Drop',{solid:true,sm:true})+menuBox('orm:'+o.id,'More for '+o.id,mItem('Drop','or-do',o.id,'drop'));
    h+=mrRow('Orphaned','attn','<span class="id keep">'+o.id+'</span> '+E(o.title),'was '+o.to+' · since map '+o.since,'Orphaned link: '+o.id,acts,'or:'+o.id);
    if(c.orphans.length>1) h+='<div class="mr more">'+(c.orphans.length-1)+' more orphaned links</div>'; }
  else h+='<div class="mr more">No orphaned links.</div>';
  return h;
}
function ctxTotal(){ return S.ctx.det+S.ctx.props.length; }
function lensBand(i,key,title,meta){
  const alt=i===1?' alt':'';
  if(!S.lens[key]) return '<section class="section lens-band'+alt+' collapsed" aria-labelledby="b'+i+'-h"><div class="sh"><button class="disc" data-act="lens" data-x="'+key+'" aria-expanded="false" aria-label="Expand the '+key+' band">▸</button><h2 id="b'+i+'-h">'+title+'</h2><span class="meta">'+meta+'</span></div></section>';
  let svg=TPL.bands[i];
  svg=svg.replace('class="node anchor"','class="node"');
  svg=svg.replace(new RegExp('(data-node="'+S.ctxNode+'"[^>]*><rect class="node)'),'$1 sel');
  if(S.ctx.sugg==='accepted') svg=svg.replace('class="node sugg"','class="node"').replace('◇ suggested, unconfirmed','confirmed');
  const conf=i===0?'<button class="btn sm conflict" data-act="conflict" aria-label="Conflict with PRD section 3.2: open both quotes side by side"><span class="w" aria-hidden="true">⚠</span>conflict with PRD §3.2 ▸</button>':'';
  return '<section class="section lens-band'+alt+'" aria-labelledby="b'+i+'-h"><div class="sh"><h2 id="b'+i+'-h">'+title+'</h2><span class="meta">'+meta+'</span></div><div class="bandbody">'+svg+conf+'</div></section>';
}
const FM=[ // file map rows
 {d:0,dir:1,n:'src/',keys:'',cnt:''},{d:1,dir:1,n:'layout/',keys:'feature:layout',cnt:'ENG 4 · PRD 2'},
 {d:2,n:'SplitPane.ts',keys:'feature:layout.split-pane <i>·</i> .orientation',cnt:'ENG-482 · PRD-tabs §3'},
 {d:2,n:'paneStore.ts',keys:'thing:pane-layout',cnt:'PRD-tabs §3'},
 {d:2,n:'Resize.ts',keys:'feature:layout.split-pane.resize',cnt:'<span class="tag" style="color:var(--attn)">No intent</span>'},
 {d:1,dir:1,n:'tabs/',keys:'',cnt:''},
 {d:2,n:'Manager.ts',keys:'feature:tabs.tab-manager <i>·</i> action:…restore-layout',cnt:'ENG-482 · #tabs-design',anchor:1},
 {d:2,n:'Strip.tsx',keys:'surface:screen/tab-strip',cnt:'PRD-tabs §2'},
 {d:1,dir:1,n:'session/',keys:'',cnt:''},
 {d:2,n:'restore.ts',sugg:1},
 {d:1,dir:1,n:'billing/',unm:1},
 {d:0,n:'tests/tabs/restore.test.ts',tests:1},
];
function fileMapBand(){
  const only=S.fmOnlyUnmapped; let rows='';
  FM.forEach(r=>{
    if(only&&!r.unm&&!r.sugg) return;
    const dsc=r.dir?'<button class="disc" data-act="fm-toggle" data-x="'+r.n+'" aria-expanded="'+(r.unm?!!S.open.billing:true)+'" aria-label="'+(r.unm?(S.open.billing?'Collapse':'Expand')+' billing/, 14 files':'Collapse '+r.n)+'">'+(r.unm?(S.open.billing?'▾':'▸'):'▾')+'</button>':'<i class="nodisc" aria-hidden="true"></i>';
    let keys=r.keys||'', cnt=r.cnt||'', acts='<span></span>', cls='fm'+(r.dir?' dir':'')+(r.anchor?' anchor-row':'')+(r.tests?' tests':'');
    if(r.sugg){ const st=S.ctx.sugg; cls+=(st==='pending'?' sugg':'');
      keys=st==='pending'?'<span class="dim">◇</span> feature:session.restore <span class="dim">(suggested 0.74)</span>':st==='accepted'?'feature:session.restore <span class="dim">(confirmed)</span>':'<span class="dim">░ unmapped</span> <span class="dim">(rejected)</span>'; cnt=st==='pending'?'no intent cites it':'';
      acts=st==='pending'?'<span class="acts"><span class="split"><button class="btn sm main" data-act="fm-sugg" data-x="accept" title="Writes one map_override row">Accept</button>'+chevBtn('fms','Reject',{solid:true,sm:true})+menuBox('fms','More for restore.ts',mItem('Reject','fm-sugg','p1','reject'))+'</span></span>':'<span></span>'; }
    if(r.unm){ cls+=' unmapped'; keys='<span class="dim">░ unmapped</span> · 14 files'; acts='<span class="acts"><button class="btn sm" data-act="fm-propose">Propose features</button></span>'; }
    if(r.tests) keys='<span class="dim">tests →</span> action:tab-manager.restore-layout';
    rows+='<div class="'+cls+'" data-row="fm:'+r.n+'" tabindex="0" '+(r.anchor?'aria-current="true"':'')+'><span class="tree" style="--d:'+r.d+'">'+dsc+'<span class="name">'+r.n+'</span></span><span class="keys">'+(S.fmNoFeat?'':keys)+'</span><span class="cnt">'+(S.fmNoCnt?'':cnt)+'</span>'+acts+'</div>';
    if(r.unm&&S.open.billing) ['invoice.ts','ledger.ts','taxes.ts','export.ts'].forEach(f=>{ rows+='<div class="fm"><span class="tree" style="--d:2"><i class="nodisc" aria-hidden="true"></i><span class="name">'+f+'</span></span><span class="keys"><span class="dim">░ unmapped</span></span><span class="cnt"></span><span></span></div>'; });
  });
  const chk=(k,l,on)=>'<label class="lens'+(on?'':' off')+'"><input type="checkbox" data-act="fm-opt" data-x="'+k+'"'+(on?' checked':'')+'>'+l+'</label>';
  return '<section class="band" aria-labelledby="fm-h"><div class="section"><div class="sh"><h2 id="fm-h">File map</h2><span class="meta"><span class="mono">agentic-workflow @ 7c3e1a2</span> · <span class="mono">map v13</span></span><div class="show" role="group" aria-label="Show"><span>show</span>'+chk('feat','features',!S.fmNoFeat)+chk('cnt','intent counts',!S.fmNoCnt)+chk('unm','unmapped only',!!S.fmOnlyUnmapped)+'</div></div><div class="fm-h"><span>path</span><span>implements</span><span>intent</span><span></span></div>'+rows+'<p class="footnote">Each Accept or Reject is one <span class="mono">map_override</span> row; the next map build reads them.</p></div></section>';
}
function collapsedBand(i,title,meta){ return '<section class="band collapsed" aria-labelledby="c'+i+'-h"><div class="section"><div class="sh"><button class="disc" data-act="fform" data-x="graph" aria-expanded="false" aria-label="Expand the '+(i?'product graph':'intent')+' band">▸</button><h2 id="c'+i+'-h">'+title+'</h2><span class="meta">'+meta+'</span></div></div></section>'; }
function context(){
  const ix=S.svc.index.state!=='ok', tot=ctxTotal(), tree=S.filesForm==='tree', acc=!S.ctx.det&&!!S.ctx.detDone;
  const sub='<div class="sub"><span>map <b>v13</b> current</span><span>·</span><span>draft <b>v14</b> ('+(tot+(acc?S.ctx.detDone:0))+' proposals'+(acc?', '+S.ctx.detDone+' accepted':'')+')</span><span>·</span><span>code index '+(ix?'<span class="x">✕</span> <b>03:00 failed</b>':'<span class="h-ok" aria-label="ok">✓</span> <b>12:27</b>')+'</span><span>·</span><span>intent <b>'+(S.sync.onyx&&S.sync.slack?'in sync':'2h behind')+'</b></span></div>';
  const bd=S.jobs.build, bj=bd&&!bd.done;
  const acts='<div class="acts"><div class="actcol"><button class="btn" data-act="build-draft" aria-describedby="build-what"'+(bj?' disabled':'')+'>'+(bj?'Building…':'Build draft')+'</button><small id="build-what">keeps the '+tot+' pending proposals</small></div>'+lockBtn('Promote · terminal','sindri context promote --draft 14','ctxp')+'</div>';
  const head='<div class="pagehead"><div style="display:flex;align-items:baseline;gap:14px"><h1 class="page-title">Context</h1>'+sub+'</div>'+acts+'</div>'+(S.revealed.ctxp?'<div class="px-note" style="margin:-4px 0 6px">Waits for the terminal: nothing changes here until you run it there. '+cmdLine('sindri context promote --draft 14','ctxpc')+'</div>':'')+(bd?'<div style="margin:0 0 6px">'+jobLines('build')+'</div>':'');
  const lens=(k,l)=>'<label class="lens'+(S.lens[k]?'':' off')+'"><input type="checkbox" data-act="lens" data-x="'+k+'"'+(S.lens[k]?' checked':'')+'>'+l+'</label>';
  const lensrow='<div class="lensrow"><label class="anchor"><kbd>⌘/</kbd><input id="anchorin" value="'+A(S.anchorText||CN[S.ctxNode][0])+'" aria-label="Anchor: a node key, a file or a record id" spellcheck="false"><span class="ph">node · file · record</span></label><button class="btn" aria-haspopup="listbox" aria-label="Depth: '+S.depth+'" data-act="depth">depth <span class="mono">'+S.depth+'</span>'+I.chev+'</button><div class="lenses" role="group" aria-label="Lenses"><span>lenses</span>'+lens('intent','intent')+lens('product','product')+lens('files','files')+'<label class="lens disabled" title="index not built (CTX phase 9)"><input type="checkbox" disabled aria-describedby="sym-why">symbols <span class="why" id="sym-why">· index not built</span></label>'+lens('draft','draft')+'</div><div class="view" role="group" aria-label="Files lens form: graph or tree"><button data-act="fform" data-x="graph" aria-pressed="'+!tree+'">graph</button><button data-act="fform" data-x="tree" aria-pressed="'+tree+'">tree</button></div></div>';
  const intentMeta='5 records · 2 decisions · 1 conflict', prodMeta='own scope 7 · affects 3 at depth ≤ 2', codeMeta='map v13 · 6 files · 11 symbols · index not built · 1 feature with no code';
  const intentT='Intent · Linear · Slack · Onyx', prodT=S.lens.draft?'Product graph · draft map v14 · '+tot+' proposals pending':'Product graph · features · surfaces · actions · things · flows', codeT='Code · files · symbols';
  if(tree) return wrapV('v-context-filemap v-context','<main class="page">'+head+banner()+lensrow+'<div class="bands">'+fileMapBand()+collapsedBand(0,intentT,intentMeta)+collapsedBand(1,prodT,prodMeta)+'</div></main>');
  const stack='<div class="stack">'+lensBand(0,'intent',intentT,intentMeta)+lensBand(1,'product',prodT,prodMeta)+lensBand(2,'files',codeT,codeMeta)+TPL.xedges+(S.pop==='conflict'?'<div class="px-pop" role="dialog" aria-label="Conflict: both quotes" style="left:30px;top:70px;right:auto;width:540px"><h3>Conflict on feature:tabs.tab-manager</h3><div class="px-quotes"><div><b>decision 10-02 (ENG-482)</b>"panes restore per window, not per workspace"</div><div><b>PRD-tabs.md §3.2</b>"panes restore per workspace when a session reopens"</div></div><div class="px-formrow"><button class="btn sm" data-act="pop-close">Close</button></div></div>':'')+'</div>';
  const cov='<section class="band" aria-labelledby="cov-h"><div class="section"><div class="sh"><h2 id="cov-h">Coverage</h2><span class="meta">of the anchor\'s scope, 7 nodes</span><button class="btn sm act" data-act="open-gaps">Open gaps</button></div><div class="covrow">'+[['intent',5,'cited'],['product',7,'confirmed'],['code',6,'implemented']].map(c=>'<div class="cell"><span class="k">'+c[0]+'</span><span class="v"><span class="segs" aria-hidden="true">'+[0,1,2,3,4,5,6].map(i=>'<i'+(i<c[1]?' class="on"':'')+'></i>').join('')+'</span><span class="mono">'+c[1]+'/7</span> '+c[2]+'</span></div>').join('')+'</div><div class="gaps"><span class="k">gaps</span><span class="mono">surface:dialog/restore-layout</span><span>has a PRD mention but no code</span><span>·</span><span class="mono">feature:session.restore</span><span>code only, no intent</span></div></div></section>';
  const src=(deg,nm,n,age)=>'<div class="src">'+chipH(deg?'Degraded':'OK')+'<span class="nm">'+nm+'</span><span class="n">'+n+'</span><span class="age">'+age+'</span></div>';
  const sl=S.sync.slack, on=S.sync.onyx, sj=S.jobs.sync&&!S.jobs.sync.done;
  const sources='<section class="band" aria-labelledby="src-h"><div class="section"><div class="sh"><h2 id="src-h">Sources</h2><span class="meta">5 · intent '+(sl&&on?'in sync':'2h behind')+'</span><button class="btn sm act" data-act="sync-all"'+(sj?' disabled':'')+'>Sync all</button></div><div class="list">'+src(0,'linear','1,204 issues · 2 projects','synced 2m ago')+src(0,'linear:doc','38 docs · 412 sections','synced 2m ago')+src(!sl,'slack','6 channels · 3,118 messages',sl?'synced just now':'2h behind')+src(!on,'onyx','96 decisions · 14 threads',on?'synced just now':'2h behind')+src(0,'git','agentic-workflow @ <span class="mono">7c3e1a2</span> · 1,204 files','12:21')+'</div>'+(S.jobs.sync?'<div style="padding:6px 16px">'+jobLines('sync')+'</div>':'')+'</div></section>';
  const duo='<section class="band" aria-label="Map review and node"><div class="duo"><div class="section" aria-labelledby="mr-h"><div class="sh"><h2 id="mr-h">Map review</h2><span class="meta"><b>'+tot+'</b> pending · draft v14 · by impact</span><button class="btn sm act" data-act="accept-det"'+(S.ctx.det?'':' disabled title="Nothing deterministic left to accept"')+'>Accept all deterministic</button></div><div class="list">'+mrBlock()+'</div><p class="footnote">Each decision writes one <span class="mono">map_override</span> row.</p></div>'+nodeCard()+'</div></section>';
  return wrapV('v-context','<main class="page">'+head+banner()+lensrow+stack+'<div class="bands">'+cov+duo+sources+'</div></main>');
}
/* edges between the bands: laid out from the node boxes (ported from the mockup) */
function drawCtx(){
  const stack=$('.stack'); if(!stack) return; const Sx=stack.getBoundingClientRect(); const z=parseFloat(getComputedStyle($('#app')).zoom)||1;
  const box=el=>{ const r=el.getBoundingClientRect(); return {l:(r.left-Sx.left)/z,r:(r.right-Sx.left)/z,t:(r.top-Sx.top)/z,b:(r.bottom-Sx.top)/z,cx:((r.left+r.right)/2-Sx.left)/z}; };
  const node=id=>stack.querySelector('[data-node="'+id+'"]'); const ov=stack.querySelector('.xedges'); if(!ov) return;
  const W=Math.round(Sx.width/z), H=Math.round(Sx.height/z); ov.setAttribute('width',W); ov.setAttribute('height',H); ov.setAttribute('viewBox','0 0 '+W+' '+H);
  ov.querySelectorAll('path[data-from]').forEach(p=>{
    const a=node(p.dataset.from), b=node(p.dataset.to); if(!a||!b||!a.closest('.lens-band')||!b.closest('.lens-band')){ p.removeAttribute('d'); const lb=ov.querySelector('text[data-edge="'+p.dataset.edge+'"]'); lb&&lb.setAttribute('visibility','hidden'); return; }
    const lb0=ov.querySelector('text[data-edge="'+p.dataset.edge+'"]'); lb0&&lb0.removeAttribute('visibility');
    const A_=box(a), B_=box(b), down=p.dataset.dir!=='up'; const y1=down?A_.b:A_.t, y2=down?B_.t:B_.b; const ol=Math.max(A_.l,B_.l), or=Math.min(A_.r,B_.r); let x,d;
    if(p.dataset.at&&or-ol>=24){ x=ol+(or-ol)*parseFloat(p.dataset.at); d='M'+x+' '+y1+'V'+y2; }
    else if(A_.cx>=B_.l+8&&A_.cx<=B_.r-8){ x=A_.cx; d='M'+x+' '+y1+'V'+y2; }
    else if(or-ol>=24){ x=(ol+or)/2; d='M'+x+' '+y1+'V'+y2; }
    else { const ga=box(a.closest('.lens-band')), gb=box(b.closest('.lens-band')); const my=down?(ga.b+gb.t)/2:(gb.b+ga.t)/2; x=B_.cx; d='M'+A_.cx+' '+y1+'V'+my+'H'+x+'V'+y2; }
    p.setAttribute('d',d);
    const lab=ov.querySelector('text[data-edge="'+p.dataset.edge+'"]'); if(lab){ const up=box((down?a:b).closest('.lens-band')), lo=box((down?b:a).closest('.lens-band')); lab.setAttribute('x',x+6); lab.setAttribute('y',(up.b+lo.t)/2+4); }
  });
  const c=stack.querySelector('.conflict'), dn=node('int-decision');
  if(c&&dn){ const D=dn.getBoundingClientRect(), P=c.offsetParent.getBoundingClientRect(); c.style.left=((D.left-P.left)/z+12)+'px'; c.style.top=((D.bottom-P.top)/z-c.offsetHeight-6)+'px'; }
}

/* ───────── recipe inspector ───────── */
const VERS=[['v7','10-02','3f1c09a2'],['v6','09-26','a1d20be7'],['v5','09-19','5be0a931'],['v4','09-12','c7710d4f'],['v3','09-03','0e5ab812'],['v2','08-27','98d3c1fe'],['v1','08-20','2b6f7a05']];
function bandNode(name,g,sub,o){ o=o||{}; return '<div class="'+(o.hop?'hop'+(o.lp?' lp':''):'node')+'">'+(o.hop?(o.lp?'<span class="loop" role="img" aria-label="loop">⇄</span>':'<span class="link" aria-hidden="true"></span>')+'<div class="node">':'')+'<span class="nm">'+name+(g?' '+g:'')+(o.small?' <small>'+o.small+'</small>':'')+'</span>'+(sub?'<span class="sub">'+sub+'</span>':'')+(o.hop?'</div>':'')+'</div>'; }
function sheetRecipe(r){
  const isItem=r.n==='workflow.item', ch=r.st==='changed', dr=r.st==='draft';
  const seed=rng(r.n.length*97+r.n.charCodeAt(2)); const rr=(a,b)=>Math.round(a+seed()*(b-a));
  const OPN=['ITM-01JB4','ITM-01JC8','ITM-01JA1','ITM-01JE7','ITM-01JE9','ITM-01JD9'].map(byId).filter(x=>x&&x.g!=='cancelled');
  const parked=OPN.filter(x=>x.g==='park').length, esc=OPN.filter(x=>x.g==='esc').length, you=OPN.filter(x=>x.g==='withyou').length, prog=OPN.filter(x=>x.g==='running'&&x.step==='produce').length, progC=OPN.filter(x=>x.g==='running'&&x.step==='checks').length;
  let band='';
  if(isItem){
    band=bandNode('intake',G('g-done','●','done'))+bandNode('produce',prog?G('g-prog','◐'+(prog>1?' '+prog:''),prog+' in progress'):'',(parked?G('g-park','▌')+parked+' parked':'')+(parked&&you?'</span><span class="sub">':'')+(you?G('g-you','◎')+you+' with you':''),{hop:1})+bandNode('checks',progC?G('g-prog','◐'+(progC>1?' '+progC:''),progC+' in progress'):'','',{hop:1,lp:1})+bandNode('gate.verify.judge','',esc?G('g-esc','▲')+esc+' escalated':'',{hop:1})+bandNode('review',G('g-todo','○','not started'),'',{hop:1,small:'desk'})+bandNode('done','','',{hop:1});
  } else band=r.states.map((s,i)=>bandNode(s,i===0?G('g-done','●','done'):(r.open&&i===Math.min(1,r.states.length-1)?G('g-prog','◐',r.open+' in progress'):''),'',{hop:i>0})).join('');
  const cols=isItem?['produce','checks','gate.verify.judge','review (desk)']:r.states.slice(1,5);
  const med=isItem?['52m','9m','—','3h 20m <small>(your answer)</small>']:cols.map(()=>rr(3,58)+'m'), parks=isItem?['14 <small>no-progress</small>','2 <small>budget</small>','—','—']:cols.map((c,i)=>i===0?rr(1,9)+' <small>no-progress</small>':'—'), escs=isItem?['—','—','11 <small>below 0.80</small>','—']:cols.map(()=>'—'), tk=isItem?['6','1','—','—']:cols.map((c,i)=>i===0?String(rr(0,5)):'—'), gr=isItem?['—','—','11 of 108 <small>(10%)</small>','—']:cols.map(()=>'—'), lp=isItem?['avg 1.6 of 3','—','','']:cols.map((c,i)=>i===0?'avg 1.3 of 3':'');
  const v=x=>x==='—'||x===''?'<span class="v dash">'+(x||'')+'</span>':'<span class="v">'+x+'</span>';
  const row=(k,a)=>'<span class="k">'+k+'</span>'+a.map(v).join('')+'<span></span>'.repeat(Math.max(0,4-a.length));
  const ps='<div class="ps" role="table" aria-label="Per-state numbers, '+S.recipeWin+'"><span class="h">per state</span>'+cols.map(c=>'<span class="h nm">'+c.replace(' (desk)',' <small style="font-family:var(--sans);color:var(--ink-subtle)">(desk)</small>')+'</span>').join('')+row('median',med)+row('parks',parks)+row('escalations',escs)+row('takeovers',tk)+row('gate refusals',gr)+row('loops',lp)+'</div>';
  const runs=r.runs!=null?sc(r.runs):0, dn=r.done!=null?sc(r.done):0, fl=r.fail!=null?sc(r.fail):0, ca=r.canc!=null?sc(r.canc):0, dwy=r.dwy!=null?sc(r.dwy):0, rest=Math.max(0,dn-dwy), tkN=r.after?sc(r.after[1]):(r.int?Math.round(rest*r.int[2]/Math.max(1,nInt(r))):0), pkN=r.after?sc(r.after[0]):Math.max(0,rest-tkN);
  const oc=(k,n,w,lo)=>'<span class="k">'+k+'</span><span class="num n">'+n+'</span><span class="trk" role="img" aria-label="'+n+' of '+runs+', '+k+'"><span class="fil'+(lo?' lo':'')+'" style="width:'+Math.min(100,Math.round(n/(runs||1)*100))+'%"></span></span>';
  const kvr=(k,v)=>'<span class="k">'+k+'</span><span class="v">'+v+'</span>';
  const outcomes='<div class="section" aria-labelledby="oc-h"><div class="sh"><h2 id="oc-h">Outcomes</h2><span class="meta">'+(S.recipeWin==='30 days'?'30 d':S.recipeWin.split(' ')[0]+' d')+(r.wk?' · runs 4 wk '+sparkN(r.wk,'runs per week'):'')+'</span></div><div class="outc">'+oc('done without you',dwy,0)+oc('done after a park',pkN,0)+oc('done after takeover',tkN,0)+oc('failed',fl,0,1)+oc('cancelled',ca,0,1)+'</div></div>';
  const human='<div class="section" aria-labelledby="ht-h"><div class="sh"><h2 id="ht-h">Human time</h2></div><div class="kvl">'+kvr('parks answered','<b>'+(r.int?sc(r.int[0]):0)+'</b> · median <b>2h 10m</b>')+kvr('escalations decided','<b>'+(r.int?sc(r.int[1]):0)+'</b> · median <b>42m</b>')+kvr('takeovers','<b>'+(r.int?sc(r.int[2]):0)+'</b> · median <b>25m</b>')+kvr('human rate',r.runs?'<b>'+humanRate(r)+'</b> · '+sc(nInt(r))+' ÷ '+runs+' runs, 4 wk '+sparkN(hwRates(r),'human rate per week','%'):'<b class="dash">—</b>')+'</div></div>';
  const cost='<div class="section" aria-labelledby="co-h"><div class="sh"><h2 id="co-h">Cost</h2></div><div class="kvl">'+kvr('tokens/run p50',(r.tok?'<b>'+r.tok+'</b>':'<b class="dash">—</b>'))+kvr('tokens/run p90',(isItem||r.tok?'<b>'+(isItem?'710k':'2.1×')+'</b>':'<b class="dash">—</b>'))+kvr('$/run p50','<b>'+(isItem?'$1.84':'$'+(rr(2,40)/10).toFixed(2))+'</b>')+kvr('30 d total','<b>'+(isItem?'$214':'$'+rr(10,180))+'</b>')+kvr('budget hits','<b>'+(isItem?2:0)+'</b>')+'</div></div>';
  const opn=isItem?OPN:[];
  const opBtn=(id,label,act,cli)=>{ const mid='op:'+id, o=S.menu===mid; return '<button class="btn" data-act="menu" data-id="'+A(mid)+'" aria-label="Open '+A(label)+'" aria-haspopup="menu" aria-expanded="'+o+'">Open '+I.chev+'</button>'+(o?'<div class="menu" role="menu" aria-label="Open '+A(label)+'">'+act+'</div>':''); };
  const opRow=x=>{ const k='op:'+x.id, wait=x.g==='park'||x.g==='esc';
    const chip=wait?chipH(x.g==='park'?'Parked':'Escalated'):x.g==='withyou'?chipH('With you'):chipH('Running');
    const tag=x.g==='park'?x.reason:x.g==='esc'?'gate '+x.score:(x.step||'');
    const act=x.g==='withyou'?'<button class="btn" data-act="handback" data-id="'+x.id+'">Hand back</button>':x.g==='park'?lockBtn('Grant rounds · terminal','sindri workflow answer '+x.id+' --grant 1',k):x.g==='esc'?lockBtn('Accept · terminal','sindri workflow answer '+x.id+' --accept',k):'<span class="mwrap">'+opBtn(x.id,x.id,mItem('Open instance','open-sheet',x.id)+mOpenIn(x))+'</span>';
    const cli=x.g==='park'?'sindri workflow answer '+x.id+' --grant 1':'sindri workflow answer '+x.id+' --accept';
    return '<div class="op" role="row" data-row="'+k+'" tabindex="0">'+chip+'<span class="row-title">'+E(x.title)+'<span class="tag">'+E(tag)+'</span></span><span class="num w">'+x.age+'</span><span class="act">'+act+'</span>'+(S.revealed[k]&&wait?rx(cmdLine(cli,k)+noteWaits):'')+'</div>'; };
  const rootRows=S.roots.filter(q=>q.rec===r.n&&!q.done);
  const openRows=isItem?opn.map(opRow).join(''):rootRows.map(q=>'<div class="op" role="row" data-row="op:'+A(q.a)+'" tabindex="0">'+chipH('Running')+'<span class="row-title">'+E(q.subj)+'<span class="tag">'+q.st+(q.badge?' '+q.badge:'')+'</span></span><span class="num w">'+q.age+'</span><span class="act"><button class="btn" data-act="open-sheet" data-kind="wf" data-id="'+A(q.a)+'" aria-label="Open '+A(q.a)+'">Open</button></span></div>').join('');
  const nOpen=isItem?opn.length:rootRows.length;
  const ev=S.evals, ecases=['01 happy-path','02 checks-fail-once','03 budget-park'];
  const evBody=ev?(ev.done?'12/12':ev.n+'/12'):'12/12';
  const running=ev&&!ev.done;
  const evals='<section class="band" aria-labelledby="ev-h"><div class="section ev"><div class="sh"><h2 id="ev-h">Evals</h2><span class="meta">'+(running?'<span class="g" aria-hidden="true">…</span> ':'<span class="g h-ok" aria-hidden="true">✓</span> ')+evBody.replace('/',' of ')+(running?'':' pass')+'</span><span class="meta">'+(running?'running':'last run '+(ev?'12:27':'today'))+' · recorded against '+(S.recipeVer||'v7')+' · 3 cases recorded from real runs (<span class="mono">--record</span>)</span></div><div class="cases">'+ecases.map((c,i)=>'<span><span class="g h-ok" aria-hidden="true">'+(running&&ev.n<=i?'…':'✓')+'</span>'+c+'</span>').join('')+'<span class="dash">… 9 more</span></div><p class="meta" style="margin-top:12px"><b style="font-weight:500;color:var(--ink-muted)">blocks</b> 7 pinned: <span class="mono">step.produce</span> v3 · <span class="mono">gate.verify.judge</span> v2 · … &nbsp;·&nbsp; <b style="font-weight:500;color:var(--ink-muted)">definition</b> states '+(isItem?5:r.states.length)+' · transitions 8 · effects 3</p></div></section>';
  const vm=S.menu==='vers';
  const vr=S.forms.recval;
  const acts='<div class="sh-acts"><button class="btn primary" data-act="evals"'+(ev&&!ev.done?' disabled':'')+'>'+(ev&&!ev.done?'Running evals…':'Run evals '+kbd('E'))+'</button><button class="btn" data-act="rec-validate">Validate</button><button class="btn is-focused" data-first data-act="rec-open-file">Open file '+I.ext+'</button><span class="px-popwrap"><button class="btn" data-act="vers" aria-haspopup="menu" aria-expanded="'+vm+'">Versions <span class="count">'+VERS.length+'</span> '+I.chev+'</button>'+(vm?'<div class="menu" role="menu" style="left:0;right:auto;width:260px">'+VERS.map((x,i)=>'<button class="mi" role="menuitemradio" aria-checked="'+((S.recipeVer||'v7')===x[0])+'" data-act="ver-set" data-x="'+x[0]+'">'+x[0]+' · '+x[2]+'<small>approved '+x[1]+(i===0?' · current':'')+'</small></button>').join('')+'</div>':'')+'</span><div class="win"><button class="btn" data-act="win" aria-haspopup="listbox" aria-expanded="'+(S.menu==='win')+'" aria-label="Window: '+S.recipeWin+'">'+S.recipeWin+' '+I.chev+'</button>'+(S.menu==='win'?'<div class="menu" role="listbox" style="left:0;right:auto;width:150px">'+Object.keys(WIN).map(w=>'<button class="mi" role="option" aria-selected="'+(w===S.recipeWin)+'" data-act="win-set" data-x="'+w+'">'+w+'</button>').join('')+'</div>':'')+'<span><b>'+runs+'</b> runs · <b>'+dn+'</b> done · <b>'+fl+'</b> failed · <b>'+ca+'</b> cancelled</span></div></div>'+(vr?'<div class="px-res '+(vr.ok?'ok':'bad')+'" role="status" style="margin-top:6px">'+(vr.ok?G('h-ok','✓')+'valid · '+vr.msg:G('h-fail','✕')+vr.msg)+'</div>':'')+(S.recipeVer&&S.recipeVer!=='v7'?'<div class="px-res" role="status" style="margin-top:6px">Numbers are scoped to runs under '+S.recipeVer+'.</div>':'');
  const inner='<div class="sheet-head"><div class="crumb"><button class="btn ghost sm sheetback" data-act="sheet-back">◂ Back</button><span class="mono">recipe/'+r.n+'</span><div class="r">'+kbd('Esc')+'<button class="icon-btn" data-act="close-sheet" aria-label="Close inspector">'+I.close+'</button></div></div><div class="sh-title">'+r.n+' <span class="m">@ <b class="mono">'+r.hash+'</b></span><span class="m"><b>'+r.last[1]+'</b></span><span class="m">schema <b>1</b></span><span class="m">'+(ch?'<b>changed since approval</b>':dr?'<b>draft</b>, never approved':'approved <b>10-02</b> by you')+'</span></div>'+acts+'</div>'+
   '<div class="sheet-body"><section class="band" aria-labelledby="st-h"><div class="section"><div class="sh"><h2 id="st-h">States</h2><span class="meta">open instances on them</span></div><div class="states-band" aria-label="States band">'+band+'</div>'+ps+'</div></section><section class="band" aria-label="Outcomes, human time and cost"><div class="cols three">'+outcomes+human+cost+'</div></section><section class="band" aria-labelledby="op-h"><div class="section"><div class="sh"><h2 id="op-h">Open</h2><span class="meta">'+nOpen+'</span></div>'+(openRows||'<div class="op"><span class="meta">No open instances.</span></div>')+'</div></section>'+evals+'</div>';
  return '<div class="shw v-recipe-inspector"><aside class="sheet" aria-label="Recipe inspector '+r.n+'" role="complementary"><div class="sheet-grip" role="separator" aria-orientation="vertical" aria-label="Resize sheet" aria-valuemin="420" aria-valuemax="760" aria-valuenow="'+S.sheetW+'" tabindex="0" data-grip></div>'+inner+'</aside></div>';
}

/* doctor check sheet (System) */
const DOC={
 'code index':{st:'failed',gl:G('h-fail','✕'),code:'SND-INDEX-012',why:'graphify timed out at the 03:00 nightly build',impact:['context: symbols lens stale since 10-12','scoping: the code source reads the old index','builder.feature: input.context.retrieve degraded'],fix:'run-index',fixL:'Run again',cli:'sindri services run index'},
 'Onyx 3000':{st:'degraded',gl:G('h-deg','◐'),code:'SND-SYNC-004',why:'last sync 2h ago; the next poll is paused after two timeouts',impact:['intent lens: Slack and Linear comments may be stale'],fix:'sync-onyx',fixL:'Sync now',cli:'sindri sources sync onyx'},
 'cursor':{st:'absent',gl:G('h-abs','○'),code:'SND-LAUNCH-003',why:'cursor-agent is not on the login PATH',impact:['launch target hidden in every Open menu','no run affected'],cli:'brew install --cask cursor'},
};
function sheetDoctor(name){
  const d=DOC[name]||{st:'ok',gl:G('h-ok','✓'),code:'',why:'The last check passed 12:25.',impact:['no failing consumer'],cli:''};
  const fixed=(name==='code index'&&S.svc.index.state==='ok')||(name==='Onyx 3000'&&S.sync.onyx); const st=fixed?'ok':d.st, gl=fixed?G('h-ok','✓'):d.gl;
  const fr=(k,sum)=>'<div class="fact"><span class="key">'+k+'</span><span class="sum">'+sum+'</span><div class="acts"></div></div>';
  const sec=(cls,title,meta,inner)=>'<div class="sb'+(cls?' '+cls:'')+'"><section class="section"><div class="sh"><h2>'+title+'</h2>'+(meta?'<span class="meta">'+meta+'</span>':'')+'</div>'+inner+'</section></div>';
  const inner=head({id:'sys:'+name,addr:'system/'+E(name.toLowerCase().replace(/\s+/g,'-')),ttl:name,meta:gl+' '+st+(d.code&&!fixed?' · <span class="mono">'+d.code+'</span>':''),subj:'doctor check · <b>'+E(name)+'</b>',sacts:'<button class="btn sm" data-act="close-sheet">Back to the graph</button>',cancelLabel:'Re-run the check…',cancelNote:'runs the doctor check only; changes nothing'})+
  '<div class="sheet-body">'+sec('','Check','from the doctor',fr('status',fixed?'ok · last check 12:27':E(st)+' · '+E(d.why))+(d.code?fr('code','<span class="mono">'+d.code+'</span>'):''))+sec('alt','Impact','transitive consumers',d.impact.map(i=>fr('consumer',E(i))).join(''))+sec('','Fix','','<div class="fixrow">'+(d.fix&&!fixed?(d.fix==='run-index'?'<button class="btn" disabled data-first title="Available with API P6 · sindri services run index">'+d.fixL+'</button><span class="pending">Available with API P6</span>':'<button class="btn primary" data-act="'+d.fix+'" data-first>'+d.fixL+'</button>'):'')+(d.cli&&!fixed?cmdLine(d.cli,'doc'):'<span class="meta">Nothing to fix.</span>')+'</div>')+'</div>';
  return sheetShell(SHEET_CLS,'Doctor check '+name,inner);
}

function sheetNode(id){
  const n=CN[id]||CN['prod-anchor'];
  const inner=head({id:'node:'+id,addr:'node/'+E(n[0]),ttl:n[0],meta:n[1],subj:'node · <span class="mono">'+E(n[0])+'</span> · map v13',sacts:'<button class="btn sm" data-act="close-sheet">Back to the stack</button>',cancelLabel:'Edit node…',cancelNote:'writes a map_override row'})+'<div class="sheet-body"><div class="sb">'+nodeCard()+'</div></div>';
  return sheetShell(SHEET_CLS+' v-context','Node '+n[0],inner);
}

/* ───────── render ───────── */
function sheetHtml(){
  const s=S.sheet; if(!s) return '';
  if(s.kind==='item'){ const x=byId(s.id); return x?sheetItem(x):''; }
  if(s.kind==='wf'){ const r=rootBy(s.id); return r?sheetWf(r):''; }
  if(s.kind==='recipe'){ const r=recBy(s.id); return r?sheetRecipe(r):''; }
  if(s.kind==='node') return sheetNode(s.id);
  if(s.kind==='doctor') return sheetDoctor(s.id);
  return '';
}
const LASTDEF={home:{kind:'item',id:'ITM-01JC8'},workflows:{kind:'wf',id:'INC-14'},work:{kind:'item',id:'ITM-01JE7'},recipes:{kind:'recipe',id:'workflow.item'}};
function edgeTab(){
  if(S.sheet) return ''; const l=S.last||LASTDEF[S.route]; if(!l||!LASTDEF[S.route]&&!S.last) return '';
  if(['context','system'].includes(S.route)) return '';
  return '<button class="sheet-tab" data-act="reopen" aria-label="Detail sheet, closed. Last opened: '+A(l.id)+'">'+kbd('↵')+' Detail · '+E(l.id)+'</button>';
}
function pageHtml(){ return {home,workflows,work,recipes,context,agents,system}[S.route](); }
function viewHtml(){
  if(S.conn==='mismatch') return topbar()+screenMismatch();
  if(S.conn==='notrunning') return topbar()+screenNotRunning();
  if(S.conn==='connecting') return topbar()+screenConnecting();
  return topbar()+pageHtml()+sheetHtml()+edgeTab()+'<div class="caption'+(S.sheet&&S.sheet.kind==='recipe'?' left':'')+'">Prototype, revision 8 · not the app</div>';
}
function sigOf(a){ const row=a.closest('[data-row]'); return {row:row&&row.dataset.row,act:a.dataset.act,id:a.dataset.id,x:a.dataset.x,idAttr:a.id,tag:a.tagName,first:a.hasAttribute('data-first'),sheet:!!a.closest('.sheet'),cls:a.className&&String(a.className).split(' ')[0]}; }
function refocus(sig){
  if(!sig) return; let el=null;
  if(sig.idAttr) el=document.getElementById(sig.idAttr);
  if(!el&&sig.act){ const c=$$('[data-act="'+sig.act+'"]').filter(b=>(b.dataset.id||'')===(sig.id||'')&&(b.dataset.x||'')===(sig.x||'')&&!!b.closest('.sheet')===sig.sheet); el=c.find(b=>(b.closest('[data-row]')||{dataset:{}}).dataset.row===sig.row)||c[0]; }
  if(!el&&sig.row) el=$('[data-row="'+CSS.escape(sig.row)+'"]');
  if(el) el.focus({preventScroll:true});
}
function render(opts){
  opts=opts||{};
  const a=document.activeElement, sig=(a&&a!==document.body&&a.closest&&a.closest('#app'))?sigOf(a):null;
  const sy=window.scrollY, sb=$('.sheet-body'), sbt=sb?sb.scrollTop:0;
  const root=document.documentElement; root.classList.toggle('rm',!!S.rm); root.classList.toggle('big',S.ts>=1.2); root.style.setProperty('--ts',S.ts); root.style.setProperty('--sheetw',S.sheetW+'px');
  $('#app').innerHTML=viewHtml();
  const nb=$('.sheet-body'); if(nb&&!opts.fresh) nb.scrollTop=sbt;
  if(!opts.fresh) window.scrollTo(0,sy);
  if(!opts.nofocus) refocus(sig);
  if(S.route==='context') drawCtx();
  titleSync(); syncProto();
  const vp=$('.sheet-grip'); 
}
function nav(hash,replace){
  const cur=location.hash.replace(/^#/,'');
  if(cur===hash) return;
  try{ history[replace?'replaceState':'pushState']({route:S.route},'','#'+hash); }catch(e){ location.hash=hash; }
  try{ localStorage.setItem('sindri.proto.last',S.route); }catch(e){}
}
const hashFor=(k,id)=>k==='wf'?'workflow/'+encodeURIComponent(id):k==='item'?'item/'+id:k==='recipe'?'recipe/'+id:k==='node'?'node/'+encodeURIComponent((CN[id]||[id])[0]):k==='doctor'?'system/'+encodeURIComponent(id):'';
function parseHash(h){
  h=h.replace(/^#/,''); if(!h) return {};
  const i=h.indexOf('/'), a=i<0?h:h.slice(0,i), rest=i<0?'':decodeURIComponent(h.slice(i+1));
  if(PAGES.includes(a)&&!rest) return {route:a};
  if(a==='context'&&rest==='files') return {route:'context',tree:true};
  if(a==='workflow') return {route:'workflows',sheet:{kind:'wf',id:rest}};
  if(a==='item') return {route:'work',sheet:{kind:'item',id:rest}};
  if(a==='recipe') return {route:'recipes',sheet:{kind:'recipe',id:rest}};
  if(a==='node'){ const id=Object.keys(CN).find(k=>CN[k][0]===rest)||(CN[rest]?rest:'prod-anchor'); return {route:'context',sheet:{kind:'node',id}}; }
  if(a==='session'){ const x=S.items.find(q=>q.sess===rest); return x?{route:'agents',sheet:{kind:'item',id:x.id}}:{route:'agents',sheet:{kind:'wf',id:'PRJ-tabs'}}; }
  if(a==='system') return {route:'system',sheet:{kind:'doctor',id:rest}};
  return {route:'home'};
}
function applyHash(h,keepRoute,boot){
  const p=parseHash(h); S.menu=null; S.pop=null; S.confirm=null;
  if(p.route&&!p.sheet){ S.route=p.route; S.sheet=null; S.stack=[]; if(p.route==='context') S.filesForm=p.tree?'tree':'graph'; }
  else if(p.sheet){ if(boot||!keepRoute) S.route=p.route; else S.route=keepRoute; S.sheet=p.sheet; S.stack=[]; S.last=p.sheet; S.sheetTab='timeline'; }
  else if(boot){ S.route='home'; }
  render({fresh:true,nofocus:false});
  try{ localStorage.setItem('sindri.proto.last',S.route); }catch(e){}
}
window.addEventListener('popstate',e=>applyHash(location.hash,e.state&&e.state.route));
function go(r,tree){
  S.sheet=null; S.stack=[]; S.menu=null; S.pop=null; S.confirm=null; S.route=r; if(r==='context') S.filesForm=tree?'tree':'graph';
  nav(r==='context'&&tree?'context/files':r); render({fresh:true,nofocus:true}); window.scrollTo(0,0);
  live(PAGE_LABEL[r]); 
}
function openSheet(kind,id,o){
  o=o||{}; const a=document.activeElement; const row=a&&a.closest&&a.closest('[data-row]');
  if(!S.sheet||o.fresh) S.sheetReturn=row?row.dataset.row:(S.sheetReturn||null);
  if(S.sheet&&o.push) S.stack.push(S.sheet); else if(!o.push) S.stack=[];
  S.sheet={kind,id}; S.last={kind,id}; S.sheetTab='timeline'; S.menu=null; S.confirm=null; S.forms={}; 
  nav(hashFor(kind,id)); render({nofocus:true,fresh:true});
  const first=$('.sheet [data-first]')||$('.sheet .sheet-body button:not([disabled])')||$('.sheet .icon-btn');
  if(first) first.focus({preventScroll:true});
  const ta=o.answer&&$('#ans'); if(ta) ta.focus();
  live('Opened '+(kind==='item'?'item ':kind==='wf'?'workflow ':kind==='recipe'?'recipe ':'')+id);
}
function closeSheet(){
  if(!S.sheet) return; const ret=S.sheetReturn; S.sheet=null; S.stack=[]; S.menu=null; S.confirm=null; S.forms={};
  nav(S.route); render({nofocus:true});
  let el=ret&&$('[data-row="'+CSS.escape(ret)+'"]'); if(!el) el=$('[data-row]'); if(el) el.focus({preventScroll:true});
  live('Sheet closed');
}
function sheetBack(){ if(S.stack.length){ const p=S.stack.pop(); S.sheet=p; S.last=p; nav(hashFor(p.kind,p.id)); render({nofocus:true,fresh:true}); const f=$('.sheet [data-first]'); f&&f.focus(); } else closeSheet(); }
/* ───────── actions ───────── */
function focusAfterConfirm(id){
  const sel=['agm:','ny:','run:','q:','sub:','ch:'].map(p=>'[data-act="menu"][data-id="'+p+id+'"]').concat(['[data-act="ask-cancel"][data-id="'+id+'"]']);
  let el=null; for(const s of sel){ el=$(s); if(el) break; }
  if(!el){ const row=$$('[data-row]').find(r=>r.textContent.includes(id)); el=row?(row.querySelector('[data-act="menu"]')||row):null; }
  if(!el) el=$('.sheet [data-first]')||$('.sheet button'); if(el) el.focus();
}
function seenKey(k){ if(k) S.seen.add(k); }
function rowOf(el){ const r=el.closest('[data-row]'); return r?r.dataset.row:null; }
function kindOfId(id){ return byId(id)?'item':rootBy(id)?'wf':recBy(id)?'recipe':'item'; }
function startJob(key,lines,done,label){
  S.jobs[key]={lines:[],done:false}; live(label+' started'); render();
  let i=0; const tick=()=>{ if(!S.jobs[key]) return; S.jobs[key].lines.push(lines[i++]); if(i>=lines.length){ S.jobs[key].done=true; done&&done(); render(); return; } render(); setTimeout(tick,420); }; setTimeout(tick,300);
}
function toast(msg){ const l=$('#layer'); const d=document.createElement('div'); d.className='px-toast'; d.setAttribute('role','status'); d.textContent=msg; l.appendChild(d); setTimeout(()=>d.remove(),4000); }
function cancelItem(id){ const x=byId(id); if(!x) return; x.g='cancelled'; delete S.launch[id]; S.confirm=null; S.menu=null; if(S.sheet&&S.sheet.id===id){ S.sheet=null; S.stack=[]; nav(S.route); } render(); live(id+' cancelled. '+openItems().length+' items open'); }
function cancelRoot(a){ const i=S.roots.findIndex(r=>r.a===a); if(i<0) return; S.roots.splice(i,1); S.confirm=null; S.menu=null; if(S.sheet&&S.sheet.id===a){ S.sheet=null; S.stack=[]; nav(S.route); } render(); live(a+' cancelled. '+S.roots.length+' workflows open'); }
function doTakeover(id){ const x=byId(id); if(!x||x.g!=='running') return; x.g='withyou'; x.since=CLOCK; x.age='0m'; x.att=!!S.attached[id]; pushTL(id,'taken over by you · the automation is paused'); seenKey(id); S.menu=null; render(); live(id+' taken over: now with you. The automation is paused'); }
function doHandback(id){ const x=byId(id); if(!x||x.g!=='withyou') return; x.g='running'; x.att=false; delete S.attached[id]; x.since=CLOCK; x.age='0m'; pushTL(id,'handed back · running again'); render(); live(id+' handed back: running'); }
function doAttach(id){ if(id==='INC-14'){ S.attached['INC-14']=true; pushTL('INC-14','attached in Warp'); render(); live('Attached in Warp'); return; } const x=byId(id); if(!x) return; S.attached[id]=true; pushTL(id,'attached in Warp · '+ATTACH_RULE); S.menu=null; render(); live('Attached in Warp. '+ATTACH_RULE); }
function doLaunch(id,tgt){ const x=byId(id); if(!x) return; if(tgt==='cursor'){ live('Cursor: not found on this host'); return; } x.tgt=tgt||x.tgt; S.launch[id]={tgt:x.tgt,at:CLOCK}; seenKey(id); seenKey(x.req); S.menu=null; pushTL(id,'sent to Warp · '+tgtLabel(x.tgt)); render(); live('Sent to Warp: '+tgtLabel(x.tgt)+' for '+id); }
function doDecide(id,how){ const x=byId(id); if(!x||x.g!=='decision') return; const req=x.req; x.g='queued'; x.age='0m'; x.note=how==='accept'?'decision accepted: '+decText(x):'decision declined'; S.seen.add(req); pushTL(id,x.note); render(); live(req+' cleared: '+(how==='accept'?'accepted. Recorded now; the engine acts on it when the workflow runs.':'declined.')+' '+needs().length+' need you'); }
function doReject(id){ const x=byId(id); if(!x||x.g!=='esc') return; x.g='running'; x.step='produce'; x.round='1/3'; x.age='0m'; x.since=CLOCK; pushTL(id,'rejected by you · produce re-runs with the findings'); render(); live(id+' rejected: produce re-runs. '+needs().length+' need you'); }
function doAnswer(id){ const x=byId(id), t=(S.note['ans:'+id]||'').trim(); if(!t){ live('Write an answer first'); const ta=$('#ans'); ta&&ta.focus(); return; } x.g='running'; x.age='0m'; x.since=CLOCK; pushTL(id,'answered by you: '+E(t)); delete S.note['ans:'+id]; render(); live(id+' cleared: answer recorded. '+needs().length+' need you'); }
function doCopy(cli,key){ try{ navigator.clipboard&&navigator.clipboard.writeText(cli).catch(()=>{}); }catch(e){} S.copied=key; render(); live('Copied: '+cli); setTimeout(()=>{ if(S.copied===key){ S.copied=null; render(); } },1500); }
function startRebuild(){ S.conn='rebuilding'; S.rebuildPct=12; render(); live('Rebuilding views'); const t=setInterval(()=>{ if(S.conn!=='rebuilding'){ clearInterval(t); return; } S.rebuildPct=Math.min(100,S.rebuildPct+22); if(S.rebuildPct>=100){ clearInterval(t); S.conn='normal'; live('Views rebuilt. Back to normal'); } render(); },600); }
function openStart(){ S.pop=S.pop==='start'?null:'start'; S.forms.startRec=S.forms.startRec||'workflow.item'; render(); if(S.pop){ const i=$('#startin'); i&&i.focus(); } }
function mrDo(id,how){
  const c=S.ctx, i=c.props.findIndex(p=>p.id===id), p=c.props[i]; if(!p) return;
  if(how==='rename'||how==='move'){ S.forms['mr:'+id]=how; render(); const inp=$('#mrin'); inp&&inp.focus(); return; }
  if(how==='cancel'){ delete S.forms['mr:'+id]; render(); return; }
  if(how==='skip'){ c.props.push(c.props.splice(i,1)[0]); render(); live('Skipped '+p.file+'. '+ctxTotal()+' proposals'); return; }
  const val=($('#mrin')||{}).value; delete S.forms['mr:'+id]; c.props.splice(i,1); if(id==='p1') c.sugg=how==='reject'?'rejected':'accepted';
  render(); live((how==='accept'?'Accepted ':how==='reject'?'Rejected ':how==='rename-ok'?'Renamed to '+val+': ':'Moved: ')+p.file+'. One map_override row. '+ctxTotal()+' proposals left');
}
const ACTS={
  go(t,e){ e.preventDefault(); go(t.dataset.id); },
  palette(){ openPalette(); },
  keys(){ openKeys(); },
  refresh(){ S.conn='normal'; render(); live('Refreshed. Updated 2s ago'); },
  rebuild(){ startRebuild(); },
  copy(t){ doCopy(t.dataset.cli,t.dataset.x); },
  lock(t){ const k=t.dataset.x; S.revealed[k]=true; seenKey(k.replace(/^ny:/,'')); render(); const c=$('.cmd[data-x="'+CSS.escape(k)+'"]')||$('.cmd'); c&&c.focus(); live('Needs a typed confirmation at a terminal. Command shown; nothing else changes'); },
  'open-sheet'(t,e){ e&&e.preventDefault(); const id=t.dataset.id, k=t.dataset.kind||kindOfId(id); openSheet(k,id,{push:!!S.sheet&&k!=='recipe',answer:t.dataset.x==='answer'}); },
  open(t){ doLaunch(t.dataset.id,t.dataset.x); },
  menu(t){ const id=t.dataset.id; S.menu=S.menu===id?null:id; S.pop=null; const sig=sigOf(t); render({nofocus:true}); refocus(sig); if(S.menu){ const f=$('.menu .mi:not([disabled])'); f&&f.focus(); } },
  'ask-cancel'(t){ S.confirm='cancel:'+t.dataset.id; S.confirmRet=null; S.menu=null; render({nofocus:true}); const k=$('[data-act="confirm-no"][data-x="'+CSS.escape(S.confirm)+'"]'); k&&k.focus(); live('Cancel '+t.dataset.id+'? Keep is selected'); },
  'ask-cancel-sub'(t){ const id=t.dataset.id; S.confirm=(S.sheet&&S.sheet.kind==='wf'?'cancelsub:':'cancelsub:')+id; S.menu=null; render({nofocus:true}); const k=$('.sheet [data-act="confirm-no"]'); k&&k.focus(); live('Cancel '+id+'? Keep is selected'); },
  'confirm-yes'(t){ const k=t.dataset.x, id=k.split(':')[1]; if(k.startsWith('cancelsub:')||k.startsWith('cancelrow:')){ if(byId(id)) cancelItem(id); else cancelRoot(id); } else cancelItem(id); },
  'confirm-no'(t){ const k=t.dataset.x; S.confirm=null; render({nofocus:true}); focusAfterConfirm(k.split(':')[1]); live('Kept. Nothing changed'); },
  decide(t){ doDecide(t.dataset.id,t.dataset.x); },
  reject(t){ doReject(t.dataset.id); },
  resume(t){ const x=byId(t.dataset.id); x.g='running'; x.age='0m'; render(); live(x.id+' resumed. '+needs().length+' need you'); },
  attach(t){ doAttach(t.dataset.id); },
  takeover(t){ doTakeover(t.dataset.id); },
  'wf-takeover'(t){ const id=t.dataset.id; pushTL(id,'taken over by you'); render(); live(id+': takeover recorded. The automation pauses at once'); },
  handback(t){ doHandback(t.dataset.id); },
  'wf-answer'(t){ openSheet('wf',t.dataset.id,{fresh:true}); },
  'wf-sev'(t){ const r=rootBy(t.dataset.id); r.st='route'; r.badge=''; r.desk=false; r.attn=false; r.now='routed as '+t.dataset.x; r.cells=['d','d','p','t']; pushTL(r.a,'severity '+t.dataset.x+' decided by you'); render(); live(r.a+' answered: '+t.dataset.x+'. '+needs().length+' need you'); },
  'wf-complete'(t){ const r=rootBy(t.dataset.id); r.st='build'; r.badge='0/9'; r.you=false; r.desk=false; r.now='build opened · 9 children queued'; r.cells=['d','d','d','d','p 0/9','t','t']; pushTL(r.a,'design output imported by you · build opened'); render(); live(r.a+': design imported, build opened'); },
  'wf-more'(t,e){ e.preventDefault(); S.wfAll[t.dataset.x]=true; render(); },
  'wf-showall'(){ live('All recipe groups shown'); },
  'tl-more'(t,e){ e.preventDefault(); const k=t.dataset.x; if(S.more[k]){ live('The full timeline is its own page; not part of this prototype'); return; } S.more[k]=true; render({nofocus:true}); live('Older events shown'); },
  'ny-all'(){ S.nyAll=!S.nyAll; render(); live(S.nyAll?'All needs-you rows shown':'Needs-you rows folded'); },
  'seen-all'(){ const ks=needs().filter(n=>!S.seen.has(n.key)).map(n=>n.key); ks.forEach(k=>S.seen.add(k)); S.undoSeen=ks.length||needs().length; S.undoKeys=ks; clearTimeout(S.undoT); S.undoT=setTimeout(()=>{ S.undoSeen=null; render(); },10000); render(); live('All marked seen · Undo available for 10 seconds'); },
  'undo-seen'(){ (S.undoKeys||[]).forEach(k=>S.seen.delete(k)); S.undoSeen=null; render(); live('Marked seen undone'); },
  'run-index'(){ if(S.jobs.index&&!S.jobs.index.done) return; startJob('index',['$ sindri services run index','graphify: scanning agentic-workflow @ 7c3e1a2 · 1,204 files','embed nomic-embed · batch 1/4','embed nomic-embed · batch 2/4','embed nomic-embed · batch 3/4','embed nomic-embed · batch 4/4','index built 12:27 · 11 symbols files · 0 timeouts','ok: service index'],()=>{ S.svc.index.state='ok'; delete S.jobs.index; toast('service index: ok · built 12:27'); live('Service index ok. '+needs().length+' need you'); },'Run again for service index'); },
  'sync-onyx'(){ startJob('onyx',['$ sindri sources sync onyx','onyx: 96 decisions · 14 threads','onyx: caught up, 0 behind','ok: onyx synced 12:27'],()=>{ S.sync.onyx=true; delete S.jobs.onyx; toast('onyx synced · 0 behind'); live('Onyx synced'); },'Sync now'); },
  'sync-all'(){ startJob('sync',['$ sindri sources sync --all','slack: 6 channels · caught up','onyx: caught up','ok: intent in sync 12:27'],()=>{ S.sync.onyx=true; S.sync.slack=true; delete S.jobs.sync; toast('sources synced'); live('Sources synced: intent in sync'); },'Sync all'); },
  start(){ openStart(); },
  'start-rec'(t){ S.forms.startRec=t.dataset.x; render({nofocus:true}); const b=$('.px-rec[aria-checked="true"]'); b&&b.focus(); },
  'start-go'(){ const v=($('#startin')||{}).value||'', rec=S.forms.startRec||'workflow.item'; S.pop=null; S.note.start=''; const n=S.roots.length+1; S.roots.unshift({a:'NEW-'+n,rec:rec==='incident.response'?'incident.response':rec==='triage.alert'?'triage.alert':'workflow.project',subj:v||'New '+rec,short:v||'New '+rec,inp:v||'input',st:'intake',age:'0m',since:CLOCK,now:'starting · '+rec,cells:['p','t','t','t'],team:'ENG'}); render(); live('Started '+rec+' · '+(v||'new')+'. '+S.roots.length+' workflows open'); },
  'pop-close'(){ S.pop=null; render(); },
  create(){ S.pop=S.pop==='create'?null:'create'; render(); const i=$('#newtitle'); i&&i.focus(); },
  'create-go'(){ const t=($('#newtitle')||{}).value.trim(), repo=($('#newrepo')||{}).value; if(!t){ live('Write a title first'); $('#newtitle').focus(); return; } const id='ITM-01N'+String(S.items.length+10); S.items.push(it({id,title:t,repo,src:'created here',g:'queued',age:'0m'})); S.pop=null; S.note.newtitle=''; render(); live('Created '+id+': '+t+'. '+openItems().length+' items'); },
  filter(t){ live('Filter by '+t.dataset.x+': every row is shown in this prototype'); },
  win(t){ S.menu=S.menu==='win'?null:'win'; render(); },
  'win-set'(t){ S.recipeWin=t.dataset.x; S.menu=null; render(); live('Window: '+S.recipeWin); },
  'win-ag'(){ live('Window: 30 days'); },
  validate(){ S.pop=S.pop==='validate'?null:'validate'; render(); const i=$('#valin'); i&&i.focus(); },
  import(){ S.pop=S.pop==='import'?null:'import'; render(); const i=$('#impin'); i&&i.focus(); },
  'val-go'(){ const f=($('#valin')||{}).value||''; S.note.val=f; S.forms.val=/\.ya?ml$/.test(f)?{ok:true,msg:'5 states · 8 transitions · 3 effects · 7 pinned blocks'}:{ok:false,msg:'SND-RECIPE-001 '+f+': not a recipe file (expected .yaml)'}; render(); live(S.forms.val.ok?'Valid':'Invalid: '+S.forms.val.msg); },
  'imp-go'(){ const f=($('#impin')||{}).value||'x.yaml'; const n=f.split('/').pop().replace(/\.ya?ml$/,''); S.recipes.push({n,st:'draft',open:0,last:['never approved'],hash:'f00d'+n.length+'bad',states:['start','done']}); S.pop=null; render(); live('Imported '+n+' as a draft. '+S.recipes.length+' recipes'); },
  evals(){ if(S.evals&&!S.evals.done) return; S.evals={n:0,done:false}; render(); live('Run evals started'); const t=setInterval(()=>{ S.evals.n++; if(S.evals.n>=12){ S.evals.done=true; clearInterval(t); live('Evals: 12 of 12 passed'); } render(); },220); },
  'rec-validate'(){ S.forms.recval={ok:true,msg:S.sheet.id+' · definition checks pass'}; render(); live('Valid'); },
  'rec-open-file'(){ toast('Opened recipes/'+S.sheet.id+'.yaml in your editor'); live('Opened the recipe file'); },
  vers(){ S.menu=S.menu==='vers'?null:'vers'; render(); const f=$('.menu .mi'); f&&f.focus(); },
  'ver-set'(t){ S.recipeVer=t.dataset.x; S.menu=null; render(); live('Numbers scoped to '+t.dataset.x); },
  lens(t){ const k=t.dataset.x; S.lens[k]=S.lens[k]?0:1; render({nofocus:false}); live(k+' lens '+(S.lens[k]?'on':'off')); },
  fform(t){ S.filesForm=t.dataset.x; nav(S.filesForm==='tree'?'context/files':'context'); render({nofocus:true}); const b=$('.view [aria-pressed="true"]'); b&&b.focus(); live('Files lens: '+S.filesForm+(S.filesForm==='tree'?'. The file map':'')); },
  node(t){ S.ctxNode=t.dataset.id; S.anchorText=null; render({nofocus:true}); const b=$('#node-h'); live('Node card: '+CN[S.ctxNode][0]); },
  'node-sheet'(t){ openSheet('node',t.dataset.id); },
  'accept-det'(){ const c=S.ctx; if(!c.det) return; c.detDone=c.det; S.toast={n:c.det}; c.det=0; clearTimeout(S.toastT); S.toastT=setTimeout(()=>{ S.toast=null; render(); },10000); render(); live(S.toast.n+' deterministic proposals accepted. 31 map_override rows written. Undo is available for 10 seconds. '+ctxTotal()+' proposals left'); },
  'undo-det'(){ const c=S.ctx; c.det=S.toast?S.toast.n:31; S.toast=null; clearTimeout(S.toastT); render(); live('Undone: '+c.det+' deterministic proposals restored. '+ctxTotal()+' proposals'); },
  'mr-do'(t){ mrDo(t.dataset.id,t.dataset.x); },
  'or-do'(t){ const id=t.dataset.id, how=t.dataset.x, c=S.ctx; if(how==='repoint-form'){ S.forms['or:'+id]=1; render(); const i=$('#orin'); i&&i.focus(); return; } if(how==='cancel'){ delete S.forms['or:'+id]; render(); return; } const i=c.orphans.findIndex(o=>o.id===id); const o=c.orphans[i]; c.orphans.splice(i,1); delete S.forms['or:'+id]; render(); live((how==='drop'?'Dropped ':'Repointed ')+o.id+'. '+c.orphans.length+' orphaned links left'); },
  'fm-sugg'(t){ mrDo('p1',t.dataset.x); },
  'fm-toggle'(t){ if(t.dataset.x==='billing/'){ S.open.billing=!S.open.billing; render(); } },
  'fm-opt'(t){ const k=t.dataset.x; if(k==='feat') S.fmNoFeat=!S.fmNoFeat; if(k==='cnt') S.fmNoCnt=!S.fmNoCnt; if(k==='unm') S.fmOnlyUnmapped=!S.fmOnlyUnmapped; render(); },
  'fm-propose'(){ toast('Proposed 3 features for billing/ as model proposals'); live('Proposed 3 features for billing'); },
  'ctx-stub'(t){ toast(t.dataset.x+' is not wired in this prototype'); live(t.dataset.x+': not wired in this prototype'); },
  conflict(){ S.pop=S.pop==='conflict'?null:'conflict'; render(); },
  'open-gaps'(){ live('Review queue filtered to the 2 gaps'); toast('Review queue filtered to the 2 gaps'); },
  depth(){ S.depth=S.depth%3+1; render(); live('Depth '+S.depth); },
  'build-draft'(){ const n=ctxTotal(); startJob('build',['building map v14 from map v13 and the override rows','kept '+n+' pending proposals','ok: draft v14 rebuilt 12:27'],()=>{ toast('Draft v14 rebuilt · '+n+' proposals kept'); live('Draft v14 rebuilt, '+n+' proposals kept'); },'Build draft'); },
  ext(t,e){ e&&e.preventDefault(); toast('Opened in '+(t.dataset.x||'the browser')+' (not wired in this prototype)'); live('Opened '+(t.dataset.x||'the link')); },
  ctx(t){ S.ctxNode=Object.keys(CN).find(k=>CN[k][0]===t.dataset.id)||'prod-anchor'; go('context'); },
  doctor(t,e){ e.preventDefault(); const n=t.dataset.id; openSheet('doctor',n.replace(/ \d+$/,'').replace(/^ENGINE.*/,'code index')==='code index'||/code index/i.test(n)?'code index':/Onyx/.test(n)?'Onyx 3000':/cursor/i.test(n)?'cursor':n); },
  hist(t,e){ e.preventDefault(); S.sysSel=t.dataset.id; render(); live('History of '+t.dataset.id); },
  'svc-toggle'(){ S.open.svc=!S.open.svc; render({nofocus:false}); },
  budgets(){ const b=$('#budget'); b&&(b.scrollIntoView({block:'center'}),b.focus({preventScroll:true})); live('Budgets: $4.12 of $14.00 today'); },
  stab(t){ S.sheetTab=t.dataset.x; render({nofocus:false}); },
  fact(t){ toast(t.dataset.x+' opened (not wired in this prototype)'); live(t.dataset.x+' opened'); },
  'req-form'(t){ const k='req:'+t.dataset.id; S.forms[k]=!S.forms[k]; render({nofocus:true}); const i=$('#reqin'); i&&i.focus(); },
  'req-open'(t){ const k='req:'+t.dataset.id; S.forms[k]=!S.forms[k]; S.menu=null; render({nofocus:true}); const i=$('#reqin'); i&&i.focus(); },
  'req-send'(t){ const id=t.dataset.id, v=(($('#reqin')||{}).value||'').trim(); if(!v){ live('Write the request first'); $('#reqin')&&$('#reqin').focus(); return; } S.req.push({item:id,text:v,t:CLOCK}); pushTL(id,'request filed: '+E(v)); delete S.forms['req:'+id]; delete S.note['req:'+id]; render(); live('Request filed for '+id+': '+v+'. A request row was recorded'); toast('Request filed for '+id); },
  'send-answer'(t){ doAnswer(t.dataset.id); },
  'close-sheet'(){ closeSheet(); },
  'sheet-back'(){ sheetBack(); },
  reopen(){ const l=S.last||LASTDEF[S.route]; if(l) openSheet(l.kind,l.id,{fresh:true}); },
  conflict2(){},
};
document.addEventListener('click',e=>{
  const t=e.target.closest('[data-act]');
  if(!t){ const nd=e.target.closest('[data-node]'); if(nd){ ACTS.node({dataset:{id:nd.dataset.node}}); return; }
    let ch=false; if(S.menu&&!e.target.closest('.menu')){ S.menu=null; ch=true; } if(S.pop&&!e.target.closest('.px-pop')&&!e.target.closest('.px-popwrap')){ S.pop=null; ch=true; } if(ch) render(); return; }
  if(t.hasAttribute('disabled')||t.getAttribute('aria-disabled')==='true') return;
  const a=t.dataset.act; if(S.menu&&a!=='menu'&&!t.closest('.menu')&&a!=='vers'&&a!=='win'){ S.menu=null; }
  const f=ACTS[a]; if(f) f(t,e);
});
document.addEventListener('dblclick',e=>{});
/* rows: click opens the sheet on rows that are not buttons */
document.addEventListener('click',e=>{
  if(e.target.closest('button,a,input,select,textarea,.menu,.px-pop,[data-act],.px-confirm,.cmd')) return;
  const row=e.target.closest('[data-row]'); if(!row) return; rowEnter(row);
});
function rowEnter(row){
  const k=row.dataset.row, i=k.indexOf(':'), p=k.slice(0,i), id=k.slice(i+1);
  S.sheetReturn=k;
  if(p==='ny'){ const n=needs().find(q=>q.key===id); if(!n) return; if(n.kind==='fail') openSheet('doctor','code index',{fresh:true}); else if(n.kind==='profile') openSheet('doctor','profile',{fresh:true}); else openSheet('item',n.it.id,{fresh:true}); }
  else if(p==='wf') openSheet('wf',id,{fresh:true});
  else if(['run','q','wk','op'].includes(p)) openSheet(byId(id)?'item':'wf',id,{fresh:true});
  else if(p==='ag') openSheet(id==='model'?'wf':'item',id==='model'?'PRJ-tabs':id,{fresh:true});
  else if(p==='rc') openSheet('recipe',id,{fresh:true});
  else if(p==='en'){ S.sysSel=id; render(); live('History of '+id); }
  else if(p==='fm'){ openSheet('node','prod-anchor',{fresh:true}); }
}
document.addEventListener('input',e=>{
  const t=e.target;
  if(t.id==='ans') S.note['ans:'+(S.sheet&&S.sheet.id)]=t.value;
  if(t.id==='reqin'){ const id=Object.keys(S.forms).find(k=>k.startsWith('req:')&&S.forms[k]); if(id) S.note[id]=t.value; }
  if(t.id==='startin') S.note.start=t.value; if(t.id==='newtitle') S.note.newtitle=t.value; if(t.id==='valin') S.note.val=t.value; if(t.id==='impin') S.note.imp=t.value;
  if(t.id==='wkfilter'){ S.wkFilter=t.value; const pos=t.selectionStart; render({nofocus:true}); const n=$('#wkfilter'); n.focus(); n.setSelectionRange(pos,pos); }
  if(t.id==='anchorin') S.anchorText=t.value;
});

/* ───────── palette ───────── */
let PAL={q:'',sel:0,items:[],ret:null};
function fuzzy(q,s){ q=q.toLowerCase(); s=s.toLowerCase(); if(!q) return 1; if(s.includes(q)) return 100-s.indexOf(q); let i=0,sc=0,last=-1; for(const c of s){ if(c===q[i]){ sc+=(last===-1||i===0)?2:1; last=i; i++; if(i===q.length) return sc; } } return 0; }
function palEntries(){
  const L=[]; const add=(sec,label,sub,keys,run,dis)=>L.push({sec,label,sub,keys,run,disabled:dis});
  const r=S.route;
  // actions for the current page
  if(r==='home'){ add('Actions','Mark all seen','Home · needs you','⇧A',()=>ACTS['seen-all']()); if(needs().length>nyCap()) add('Actions',S.nyAll?'Show fewer needs-you rows':'Show all '+needs().length+' needs-you rows','Home','',()=>ACTS['ny-all']());
    inG('decision').forEach(x=>{ add('Actions','Accept '+x.req,'record the decision · '+x.id,'',()=>doDecide(x.id,'accept')); add('Actions','Decline '+x.req,x.id,'',()=>doDecide(x.id,'decline')); });
    inG('esc').forEach(x=>add('Actions','Reject '+x.id,'escalated gate · produce re-runs','',()=>doReject(x.id)));
    inG('park').forEach(x=>add('Actions','Copy: sindri workflow answer '+x.id+' --grant 1','protected · needs a terminal','Y',()=>doCopy('sindri workflow answer '+x.id+' --grant 1','pal'))); }
  if(r==='workflows') add('Actions','Start a workflow…','approved recipes','⌘N',()=>{ go('workflows'); openStart(); });
  if(r==='work') add('Actions','Create item','Work','C',()=>{ S.pop='create'; render(); $('#newtitle')&&$('#newtitle').focus(); });
  if(r==='recipes'){ add('Actions','Validate file…','Recipes','',()=>ACTS.validate()); add('Actions','Import recipe','Recipes','',()=>ACTS.import()); if(S.sheet&&S.sheet.kind==='recipe') add('Actions','Run evals for '+S.sheet.id,'Recipes','E',()=>ACTS.evals()); }
  if(r==='context'){ add('Actions','Accept all deterministic','Context · '+S.ctx.det+' proposals','',()=>ACTS['accept-det'](),!S.ctx.det); add('Actions','Build draft','Context','',()=>ACTS['build-draft']()); add('Actions','Files lens: '+(S.filesForm==='tree'?'graph':'tree'),'Context','',()=>ACTS.fform({dataset:{x:S.filesForm==='tree'?'graph':'tree'}})); add('Actions','Sync all sources','Context','',()=>ACTS['sync-all']()); }
  if(r==='agents') add('Actions','Budgets','Agents','',()=>ACTS.budgets());
  if(r==='system'){  if(!S.sync.onyx) add('Actions','Sync now: Onyx','System · 2h behind','',()=>ACTS['sync-onyx']()); }
  if(S.svc.index.state!=='ok') add('Actions','Run index job (dev)','simulation · Run again waits for API P6','',()=>ACTS['run-index']());
  if(S.conn!=='stalled') add('Actions','Simulate Stalled views (dev)','simulation · the controls panel has it too','',()=>{ S.conn='stalled'; S.menu=null; S.pop=null; render(); live('Stalled: views are stale'); });
  add('Actions','Shortcuts','keys of this page','?',()=>openKeys());
  // go to
  [['Needs you','home · G N',()=>goNeeds()],['Home','G H',()=>go('home')],['Workflows','G W',()=>go('workflows')],['Work','G Q',()=>go('work')],['Recipes','G R',()=>go('recipes')],['Context','G C',()=>go('context')],['File map','Context · tree',()=>go('context',true)],['Agents','G A',()=>go('agents')],['System','G .',()=>go('system')]].forEach(x=>add('Go to',x[0],x[1],'',x[2]));
  // deep links
  S.roots.forEach(x=>add('Deep links','workflow/'+x.a,x.subj,'',()=>{ go('workflows'); openSheet('wf',x.a,{fresh:true}); }));
  openItems().forEach(x=>add('Deep links','item/'+x.id,x.title,'',()=>{ go('work'); openSheet('item',x.id,{fresh:true}); }));
  S.recipes.forEach(x=>add('Deep links','recipe/'+x.n,x.st,'',()=>{ go('recipes'); openSheet('recipe',x.n,{fresh:true}); }));
  liveSessions().concat(inG('withyou')).forEach(x=>add('Deep links','session/'+x.sess,x.id+' · '+x.title,'',()=>{ go('agents'); openSheet('item',x.id,{fresh:true}); }));
  Object.keys(CN).forEach(k=>add('Deep links','node/'+CN[k][0],CN[k][1].replace(/<[^>]+>/g,''),'',()=>{ go('context'); openSheet('node',k,{fresh:true}); }));
  // sindri
  if(S.conn==='stalled') add('Sindri','Rebuild views','views are stalled','',()=>startRebuild());
  add('Sindri','Stop Sindri…','asks first','',()=>openStop());
  add('Sindri','Health: System','G .','',()=>go('system'));
  return L;
}
function palFilter(){
  const all=palEntries(); const q=PAL.q.trim();
  if(!q) return all.filter(x=>x.sec!=='Deep links').concat(all.filter(x=>x.sec==='Deep links').slice(0,6));
  return all.map(x=>({x,s:Math.max(fuzzy(q,x.label),fuzzy(q,x.sub||'')*.6)})).filter(o=>o.s>0).sort((a,b)=>b.s-a.s).map(o=>o.x).slice(0,14);
}
function palRender(){
  PAL.items=palFilter(); if(PAL.sel>=PAL.items.length) PAL.sel=Math.max(0,PAL.items.length-1);
  let h='',last='';
  PAL.items.forEach((x,i)=>{ if(!PAL.q&&x.sec!==last){ h+='<div class="px-palsec" role="presentation">'+x.sec+'</div>'; last=x.sec; }
    h+='<div class="px-palit'+(i===PAL.sel?' on':'')+(x.disabled?' dis':'')+'" role="option" id="po'+i+'" aria-selected="'+(i===PAL.sel)+'" data-pi="'+i+'"><span class="g" aria-hidden="true">'+(x.sec==='Go to'?'▸':x.sec==='Actions'?'↵':x.sec==='Sindri'?'◇':'#')+'</span><span>'+E(x.label)+'</span><span class="s">'+E(x.sub||'')+'</span>'+(x.keys?'<span class="k">'+x.keys.split(' ').map(kbd).join('')+'</span>':'<span class="k"></span>')+'</div>'; });
  if(!PAL.items.length) h='<div class="px-palempty">No command matches “'+E(PAL.q)+'”.</div>';
  $('#palist').innerHTML=h; const inp=$('#palIn'); if(inp) inp.setAttribute('aria-activedescendant',PAL.items.length?'po'+PAL.sel:'');
  const on=$('.px-palit.on'); on&&on.scrollIntoView({block:'nearest'});
}
function openPalette(){
  if(S.palette) return; S.palette=true; PAL={q:'',sel:0,items:[],ret:document.activeElement}; S.menu=null;
  $('#layer').insertAdjacentHTML('beforeend','<div class="px-scrim" id="palScrim"><div class="px-pal" role="dialog" aria-modal="true" aria-label="Command palette"><div class="px-palin">'+I.search+'<input id="palIn" role="combobox" aria-expanded="true" aria-controls="palist" aria-autocomplete="list" placeholder="Search actions, pages and ids" autocomplete="off" spellcheck="false"></div><div class="px-palist" id="palist" role="listbox" aria-label="Commands"></div><div class="px-palfoot"><span>'+kbd('↑')+kbd('↓')+' Move</span><span>'+kbd('↵')+' Run</span><span>'+kbd('Esc')+' Close</span></div></div></div>');
  const inp=$('#palIn'); inp.focus(); palRender();
  inp.addEventListener('input',()=>{ PAL.q=inp.value; PAL.sel=0; palRender(); });
  $('#palist').addEventListener('click',e=>{ const r=e.target.closest('[data-pi]'); if(r) runPal(+r.dataset.pi); });
  $('#palScrim').addEventListener('mousedown',e=>{ if(e.target.id==='palScrim') closePalette(); });
}
function closePalette(){ const el=$('#palScrim'); if(el) el.remove(); S.palette=false; const r=PAL.ret; if(r&&document.contains(r)&&r.focus) r.focus({preventScroll:true}); else { const row=$('[data-row]'); row&&row.focus(); } }
function runPal(i){ const x=PAL.items[i]; if(!x) return; if(x.disabled){ live(x.label+': not available now'); return; } PAL.ret=null; const el=$('#palScrim'); if(el) el.remove(); S.palette=false; x.run(); }
function goNeeds(){ S.nyAll=needs().length>nyCap(); go('home'); const f=$('[data-row^="ny:"]'); f&&f.focus(); window.scrollTo(0,0); live(needs().length+' need you'); }
/* ───────── dialogs ───────── */
const KEYS={
 home:[['G N','Needs you'],['J K','Move between rows'],['↵','Open the row\'s sheet'],['O','Open or attach'],['A','Answer'],['⇧A','Mark all seen'],['Y','Copy the row\'s command'],['X','Cancel, then confirm']],
 workflows:[['↵','Open the instance'],['A','Answer a desk step'],['B','Hand back'],['X','Cancel, then confirm'],['⌘N','Start a workflow']],
 work:[['↵','Open the item'],['/','Filter'],['C','Create item'],['T','Take over'],['B','Hand back']],
 recipes:[['↵','Open the inspector'],['E','Run evals (inspector)'],['J K','Move']],
 context:[['⌘/','Focus the anchor'],['↵','Open the node'],['A','Accept (file map)'],['X','Reject (file map)'],['S','Skip a proposal']],
 agents:[['↵','Details'],['O','Attach'],['T','Take over'],['B','Hand back'],['X','Cancel, then confirm']],
 system:[['↵','Doctor check or history'],['E','Engine'],['J K','Move']]};
const KEYS_ALL=[['⌘K','Command palette'],['G H W Q R C A .','Go to a page (chord)'],['?','This list'],['Esc','Close the top layer']];
function openKeys(){ S.dialog='keys'; S.dialogRet=document.activeElement; S.menu=null; renderDialog(); const b=$('#keysClose'); b&&b.focus(); }
function openStop(){ S.dialog='stop'; S.dialogRet=document.activeElement; renderDialog(); const b=$('#dlgNo'); b&&b.focus(); }
function renderDialog(){
  let el=$('#dlgScrim'); if(el) el.remove(); if(!S.dialog) return;
  let inner='';
  if(S.dialog==='keys') inner='<h2 id="dtitle">Shortcuts · '+PAGE_LABEL[S.route]+'</h2><div class="px-keys">'+(KEYS[S.route]||[]).concat(KEYS_ALL).map(k=>'<div><span>'+k[0].split(' ').map(kbd).join('')+'</span><span>'+k[1]+'</span></div>').join('')+'</div><div class="px-formrow"><button class="btn" id="keysClose" data-dlg="close">Close <kbd>Esc</kbd></button></div>';
  else inner='<h2 id="dtitle">Stop Sindri?</h2><p>Running sessions keep going in tmux. The app shows Not running until you start <span class="mono">sindri</span> again.</p><div class="px-formrow"><button class="btn danger" id="dlgYes" data-dlg="stop">Stop Sindri</button><button class="btn" id="dlgNo" data-dlg="close">Keep running</button></div>';
  $('#layer').insertAdjacentHTML('beforeend','<div class="px-scrim" id="dlgScrim" data-dlg="bg"><div class="px-dlg" role="dialog" aria-modal="true" aria-labelledby="dtitle">'+inner+'</div></div>');
}
function closeDialog(){ S.dialog=null; const el=$('#dlgScrim'); if(el) el.remove(); const r=S.dialogRet; if(r&&document.contains(r)&&r.focus) r.focus({preventScroll:true}); }
document.addEventListener('click',e=>{ const d=e.target.closest('[data-dlg]'); if(!d) return; const k=d.dataset.dlg; if(k==='bg'&&e.target!==d) return; if(k==='stop'){ closeDialog(); S.conn='notrunning'; S.sheet=null; render(); live('Sindri stopped'); } else closeDialog(); });
/* ───────── keys ───────── */
let chord=false,chordT=null;
function setChord(on){ chord=on; clearTimeout(chordT); const c=$('#chord'); c&&c.classList.toggle('on',on); if(on) chordT=setTimeout(()=>setChord(false),1000); }
function typing(el){ return el&&(el.tagName==='INPUT'||el.tagName==='TEXTAREA'||el.tagName==='SELECT'||el.isContentEditable); }
function moveRow(d){ const rows=$$('#app [data-row]').filter(r=>r.offsetParent!==null&&!r.closest('.sheet')); if(!rows.length) return; const a=document.activeElement; const cur=a&&a.closest?a.closest('[data-row]'):null; let i=cur?rows.indexOf(cur):-1; i=i<0?(d>0?0:rows.length-1):Math.max(0,Math.min(rows.length-1,i+d)); rows[i].focus(); rows[i].scrollIntoView({block:'nearest'}); }
function focusedItem(){ const row=document.activeElement&&document.activeElement.closest&&document.activeElement.closest('[data-row]'); const sh=S.sheet&&S.sheet.kind==='item'?byId(S.sheet.id):null; if(!row) return sh; const k=row.dataset.row, i=k.indexOf(':'), p=k.slice(0,i), id=k.slice(i+1); if(p==='ny'){ const n=needs().find(q=>q.key===id); return n&&n.it; } if(['run','q','wk','ag','op'].includes(p)) return byId(id); return sh; }
document.addEventListener('keydown',e=>{
  const k=e.key;
  if((e.metaKey||e.ctrlKey)&&k.toLowerCase()==='k'){ e.preventDefault(); S.palette?closePalette():openPalette(); return; }
  if(S.palette){
    if(k==='Escape'){ e.preventDefault(); closePalette(); }
    else if(k==='ArrowDown'){ e.preventDefault(); PAL.sel=Math.min(PAL.items.length-1,PAL.sel+1); palRender(); }
    else if(k==='ArrowUp'){ e.preventDefault(); PAL.sel=Math.max(0,PAL.sel-1); palRender(); }
    else if(k==='Enter'){ e.preventDefault(); runPal(PAL.sel); }
    return;
  }
  if(S.dialog){ if(k==='Escape'){ e.preventDefault(); closeDialog(); } else if(k==='Tab'){ const fs=$$('.px-dlg button'); if(fs.length){ e.preventDefault(); const i=fs.indexOf(document.activeElement); fs[(i+(e.shiftKey?-1:1)+fs.length)%fs.length].focus(); } } return; }
  if((e.metaKey||e.ctrlKey)&&k.toLowerCase()==='n'){ e.preventDefault(); go('workflows'); openStart(); return; }
  if((e.metaKey||e.ctrlKey)&&k.toLowerCase()==='z'){ if(S.toast){ e.preventDefault(); ACTS['undo-det'](); } else if(S.undoSeen){ e.preventDefault(); ACTS['undo-seen'](); } return; }
  if((e.metaKey||e.ctrlKey)&&k==='Enter'){ const ta=document.activeElement; if(ta&&ta.id==='ans'&&S.sheet){ e.preventDefault(); doAnswer(S.sheet.id); } return; }
  if(k==='Escape'){
    if(S.confirm){ e.preventDefault(); const kk=S.confirm; S.confirm=null; render({nofocus:true}); focusAfterConfirm(kk.split(':')[1]); live('Kept. Nothing changed'); return; }
    if(S.menu){ e.preventDefault(); const id=S.menu; S.menu=null; render({nofocus:true}); const el=$('[data-act="menu"][data-id="'+id+'"]')||$('[data-act="vers"]')||$('[data-act="win"]'); el&&el.focus(); return; }
    if(S.pop){ e.preventDefault(); S.pop=null; render({nofocus:true}); const el=$('[data-act="start"],[data-act="create"],[data-act="validate"],[data-act="import"],[data-act="conflict"]'); el&&el.focus(); return; }
    if(S.sheet){ e.preventDefault(); closeSheet(); return; }
    return;
  }
  const a=document.activeElement;
  if(typing(a)) return;
  if(['notrunning','mismatch','connecting'].includes(S.conn)) return;
  if(e.metaKey||e.ctrlKey||e.altKey) return;
  if(chord){
    const m={n:'needs',h:'home',w:'workflows',q:'work',r:'recipes',c:'context',a:'agents','.':'system',s:'system',d:'home'}; const d=m[k.toLowerCase()]; setChord(false);
    if(d){ e.preventDefault(); if(d==='needs') goNeeds(); else { go(d); const f=(d==='system'&&$('[data-act="sync-onyx"]'))||$('[data-row]'); f&&f.focus({preventScroll:true}); } }
    return;
  }
  if(S.menu&&(k==='ArrowDown'||k==='ArrowUp')){ e.preventDefault(); const its=$$('.menu .mi:not([disabled])'); const i=its.indexOf(a); const n=its[(i+(k==='ArrowDown'?1:-1)+its.length)%its.length]; n&&n.focus(); return; }
  if(a&&a.dataset&&a.dataset.grip!==undefined&&(k==='ArrowLeft'||k==='ArrowRight')){ e.preventDefault(); S.sheetW=Math.max(420,Math.min(760,S.sheetW+(k==='ArrowLeft'?20:-20))); render(); const g=$('.sheet-grip'); g&&g.focus(); return; }
  const x=focusedItem();
  switch(true){
    case (k==='g'||k==='G')&&!e.shiftKey: e.preventDefault(); setChord(true); break;
    case k==='j'||k==='ArrowDown': if(!a.closest('.menu')&&!a.closest('.sheet')){ e.preventDefault(); moveRow(1); } break;
    case k==='k'||k==='ArrowUp': if(!a.closest('.menu')&&!a.closest('.sheet')){ e.preventDefault(); moveRow(-1); } break;
    case k==='Enter': { const row=a&&a.closest&&a.closest('[data-row]'); if(row&&a===row&&!a.closest('.sheet')){ e.preventDefault(); rowEnter(row); } break; }
    case k==='?': e.preventDefault(); openKeys(); break;
    case k==='A'&&e.shiftKey: e.preventDefault(); ACTS['seen-all'](); break;
    case k==='a': { e.preventDefault(); const row=a&&a.closest&&a.closest('[data-row]'); if(x&&x.g==='park'&&(x.reason==='needs-answer'||x.reason==='no-progress')) openSheet('item',x.id,{fresh:true,answer:true}); else if(row&&row.dataset.row.startsWith('wf:')&&rootBy(row.dataset.row.slice(3)).desk) openSheet('wf',row.dataset.row.slice(3),{fresh:true}); else if(x&&x.g==='decision'&&S.sheet) doDecide(x.id,'accept'); else if(x&&x.g==='withyou') doHandback(x.id); else live('Open the row first (Enter), or use its button'); break; }
    case k==='o': { e.preventDefault(); if(x&&x.g==='running'&&x.term) doAttach(x.id); else if(x&&x.g!=='cancelled') doLaunch(x.id,x.tgt); break; }
    case k==='t': { e.preventDefault(); if(x&&x.g==='running') doTakeover(x.id); break; }
    case k==='b': { e.preventDefault(); if(x&&x.g==='withyou') doHandback(x.id); break; }
    case k==='x': { e.preventDefault(); const wr=a&&a.closest&&a.closest('[data-row^="wf:"]'); if(wr&&!a.closest('.sheet')){ const rid=wr.dataset.row.slice(3); S.confirm='cancelrow:'+rid; render({nofocus:true}); const kp=$('[data-act="confirm-no"][data-x="cancelrow:'+CSS.escape(rid)+'"]'); kp&&kp.focus(); live('Cancel '+rid+'? Keep is selected'); break; } if(x&&!['cancelled'].includes(x.g)){ S.confirm=(S.sheet?'cancelsub:':'cancel:')+x.id; render({nofocus:true}); const kp=$('[data-act="confirm-no"]'); kp&&kp.focus(); } break; }
    case k==='y': { e.preventDefault(); const c=$('.cmd',a&&a.closest&&a.closest('[data-row]')||document); if(c) doCopy(c.dataset.cli,c.dataset.x); else live('This row has no command to copy'); break; }
    case k==='c': { if(S.route==='work'){ e.preventDefault(); ACTS.create(); } break; }
    case k==='/': { if(S.route==='work'){ e.preventDefault(); const f=$('#wkfilter'); f&&f.focus(); } break; }
    case k==='e': { if(S.sheet&&S.sheet.kind==='recipe'){ e.preventDefault(); ACTS.evals(); } else if(S.route==='system'){ const t=$('#eng-h'); t&&t.scrollIntoView(); live('Engine'); } break; }
    case k==='s': { if(S.route==='context'&&S.ctx.props.length){ e.preventDefault(); mrDo(S.ctx.props[0].id,'skip'); } break; }
    case k==='r': { if(x&&x.g==='running'&&x.term){ e.preventDefault(); ACTS['req-form']({dataset:{id:x.id}}); } break; }
  }
});
/* resizing the sheet by dragging its left edge (420 to 760) */
document.addEventListener('pointerdown',e=>{
  const g=e.target.closest('[data-grip]'); if(!g) return; e.preventDefault(); g.setPointerCapture(e.pointerId);
  const mv=ev=>{ const z=parseFloat(getComputedStyle($('#app')).zoom)||1; const w=Math.round((window.innerWidth-ev.clientX)/z); S.sheetW=Math.max(420,Math.min(760,w)); document.documentElement.style.setProperty('--sheetw',S.sheetW+'px'); };
  const up=()=>{ g.removeEventListener('pointermove',mv); g.removeEventListener('pointerup',up); render(); const n=$('.sheet-grip'); n&&n.focus(); live('Sheet width '+S.sheetW+' pixels'); };
  g.addEventListener('pointermove',mv); g.addEventListener('pointerup',up);
});
/* seen after one second of focus */
document.addEventListener('focusin',e=>{ const row=e.target.closest&&e.target.closest('[data-row]'); if(row){ if(S.focusKey!==row.dataset.row){ S.focusKey=row.dataset.row; S.focusSince=Date.now(); } } });
setInterval(()=>{ const k=S.focusKey; if(!k||!k.startsWith('ny:')) return; const a=document.activeElement; const row=a&&a.closest&&a.closest('[data-row]'); if(!row||row.dataset.row!==k) return; const n=needs().find(q=>q.key===k.slice(3)); if(n&&!S.seen.has(n.key)&&Date.now()-S.focusSince>=1000){ S.seen.add(n.key); render(); } },300);
/* ───────── prototype controls ───────── */
const CONNS=[['normal','Normal'],['notrunning','Not running'],['connecting','Connecting'],['mismatch','Version mismatch'],['stale','Stale'],['rebuilding','Rebuilding'],['stalled','Stalled']];
$('#connStates').innerHTML=CONNS.map(c=>'<button data-conn="'+c[0]+'" aria-pressed="false">'+c[1]+'</button>').join('');
$('#connStates').addEventListener('click',e=>{ const b=e.target.closest('[data-conn]'); if(!b) return; S.conn=b.dataset.conn; S.menu=null; S.pop=null; if(['notrunning','mismatch','connecting'].includes(S.conn)) S.sheet=null; S.rebuildPct=28; live('State: '+b.textContent); render(); if(S.conn==='rebuilding') { const t=setInterval(()=>{ if(S.conn!=='rebuilding'){ clearInterval(t); return; } S.rebuildPct=Math.min(96,S.rebuildPct+3); render({nofocus:true}); },900); } });
$('#optRm').addEventListener('change',e=>{ S.rm=e.target.checked; render(); live('Reduced motion '+(S.rm?'on':'off')); });
$('#optScale').addEventListener('input',e=>{ S.ts=+e.target.value; $('#scaleVal').textContent=S.ts.toFixed(1); render(); });
$('#optLarge').addEventListener('change',e=>{ S.large=e.target.checked; const c=S.conn; load(S.large); S.conn=c; nav(S.route,true); render({fresh:true}); live(S.large?'Large fixture: '+needs().length+' need you':'Normal fixture'); });
$('#optReset').addEventListener('click',()=>{ S.conn='normal'; S.large=false; S.ts=1; S.rm=matchMedia('(prefers-reduced-motion: reduce)').matches; S.sheetW=760; S.last=null; load(false); $('#scaleVal').textContent='1.0'; history.replaceState({route:'home'},'','#home'); S.route='home'; render({fresh:true}); live('Fixture reset: '+needs().length+' need you'); });
$('#protoToggle').addEventListener('click',()=>{ const p=$('#proto'); const c=p.classList.toggle('collapsed'); $('#protoToggle').setAttribute('aria-expanded',!c); });
['click','keydown','pointerdown'].forEach(t=>$('#proto').addEventListener(t,e=>e.stopPropagation()));
function syncProto(){ $$('#connStates [data-conn]').forEach(b=>b.setAttribute('aria-pressed',b.dataset.conn===S.conn)); $('#optRm').checked=S.rm; $('#optLarge').checked=S.large; $('#optScale').value=S.ts; }
/* ───────── boot ───────── */
(function boot(){
  let h=location.hash.replace(/^#/,'');
  if(!h){ try{ h=localStorage.getItem('sindri.proto.last')||'home'; }catch(e){ h='home'; } history.replaceState({route:h},'','#'+h); }
  applyHash('#'+h,null,true);
  document.fonts&&document.fonts.ready&&document.fonts.ready.then(()=>{ if(S.route==='context') drawCtx(); });
  window.addEventListener('resize',()=>{ if(S.route==='context') drawCtx(); });
  setTimeout(()=>live(needs().length+' now need you'),400);
})();

