# jobs: notes for the ui lane

Open `jobs.html` (reached from System's Background jobs button). URL params: `?state=<populated|reading|empty|norunning|unreadable|failed>`, `&open=<job id>`, `&annot=1`. Data is the shape of `GET /sindri/jobs` and `/jobs/{id}`; the plain kind names are proposals. Build spec: `specs/jobs.md`.
