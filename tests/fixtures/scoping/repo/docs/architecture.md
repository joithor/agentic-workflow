# Architecture

- `src/notes`: the note model, its service and routes. A note belongs to one team.
- `src/sharing`: public links. A link holds an opaque token; `GET /s/:token` resolves it without a login.
- `src/export`: renders a team's notes as Markdown or JSON for download.
- `src/digest`: the weekly email digest of changed notes, sent by the nightly job to subscribed members.
- `src/roles`: `owner`, `editor`, `viewer`; `guard` checks a role on every route.
- `src/audit`: an append-only audit log and the report over it.
- `src/config/flags.ts`: feature flags, read once at start.
- `src/jobs/nightly.ts`: runs the digest and the trash purge.
