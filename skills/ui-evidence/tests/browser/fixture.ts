import http from "node:http";

const PAGE = `<!doctype html><html><body>
<nav style="display:none"><span>Schedule</span><span>Draft banner</span></nav>
<h1>Schedule</h1>
<p id="banner" style="display:none">Draft banner</p>
<a data-testid="go-b" href="#" onclick="setTimeout(()=>history.pushState({},'','/b'),300);return false">Go B</a>
<input data-testid="note-input" aria-label="Note" />
<button data-testid="save-renamed" onclick="document.getElementById('out').textContent='Saved!'">Save</button>
<button data-testid="noop" onclick="void 0">Do nothing</button>
<button data-testid="hidden-btn" style="display:none" onclick="document.getElementById('out').textContent='Saved!'">Hidden save</button>
<button onclick="document.getElementById('out').textContent='Twin'">Twin</button><button onclick="document.getElementById('out').textContent='Twin'">Twin</button>
<input aria-label="Dup" /><input aria-label="Dup" />
<p>Pick me</p>
<div data-testid="hscroll" style="width:200px;height:60px;overflow:auto"><div style="width:1000px;height:300px">wide</div></div>
<button data-testid="row-open" onclick="document.getElementById('out').textContent='Opened 0'">Row A</button>
<button data-testid="row-open" onclick="document.getElementById('out').textContent='Opened 1'">Row B</button>
<div id="out"></div>
</body></html>`;

export async function startFixture(): Promise<{ host: string; close: () => Promise<void> }> {
  const server = http.createServer((_req, res) => { res.setHeader("content-type", "text/html"); res.end(PAGE); });
  await new Promise<void>((r) => server.listen(0, "127.0.0.1", r));
  const { port } = server.address() as { port: number };
  return { host: `http://127.0.0.1:${port}`, close: () => new Promise((r) => server.close(() => r())) };
}
