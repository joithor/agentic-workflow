# system: notes for the ui lane

Open `system.html`. Bar: Overview and Services tabs, the Background jobs button, a "View as" switch (prototype only). URL params: `?state=<healthy|degraded|profile|rebuilding|stalled|stale|down|mismatch|unreadable|connecting>`, `&tab=services`, `&annot=1`. Data is the shape of `GET /sindri/status` and `GET /sindri/services`; pid, hashes and times are mockup-authored, service names are real, the "what it does" words are a proposal (gap). Build spec: `specs/system.md`. Jobs is its own page, `jobs.html`.
