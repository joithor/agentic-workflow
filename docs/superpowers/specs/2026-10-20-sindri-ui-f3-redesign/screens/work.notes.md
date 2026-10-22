# work: notes for the ui lane

Open `work.html`. Top bar: filter box (`/`) and the open count. Bar: All or Running, the repo chip, key hints, a "View as" switch (prototype only), New item. URL params: `?state=<populated|loading|empty|nomatch|rebuilding|stalled|stale|down>`, `&open=<item id>` (opens the sheet), `&closed=1` (opens the Closed fold), `&filter=running`, `&dialog=create` with `&error=title`, `&annot=1`.

Data is the shape of `GET /sindri/queue`, `GET /sindri/items/{id}` and `/items/{id}/timeline`. Item titles and ids are the board's runs; the repos (aw-notes, aw-notes-api), the three queued items and the history lines are mockup-authored. The build spec is `specs/work.md`.
