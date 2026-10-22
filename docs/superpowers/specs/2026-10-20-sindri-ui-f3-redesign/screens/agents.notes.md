# agents: notes for the ui lane

Open `agents.html`. Top bar: search (`/`). Under it: the three filters, key hints and a "View as" switch (prototype only) for every state. URL params for screenshots: `?state=<populated|reading|empty|nomatch|problems|stale|unavailable|unreadable>`, `&open=<session ref>` (opens the sheet), `&closed=1` (no sheet), `&ended=1` (opens "Recently ended"), `&annot=1`.

Data is the shape of `GET /sindri/sessions` and `GET /sindri/sessions/{ref}`. Names come from the sample world (aw-notes); the box, panes, pids and session ids are mockup-authored. Five live sessions (one asking), three recently ended (one with no resume command).

The build spec is `specs/agents.md`. Gaps (Annotations on): allowance used and a free-text activity line are not served; "Watch the agent" is disabled (`focus.why = not-built`).
