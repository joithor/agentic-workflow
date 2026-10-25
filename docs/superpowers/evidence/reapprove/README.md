# Re-approve outdated workflows (#896)

The real app, not a test host: `sindri serve` in a guarded temp home (`scripts/sindri-ui-fixture.sh up`) and `sindri-ui`
(a debug build of this branch at 479c9972d) on an Xvfb display at 1440x900, driven with XTEST events by
`scripts/sindri-ui-capture.py`. Seed: `workflow.dev-timeout` registered at v3 and approved as the owner, then v4 (a changed
description, so a new hash) registered and left waiting.

| Frame | State |
|---|---|
| `before-outdated-settings.png` | Settings, step 3 "Approve the workflows" is **Needs you** with "1 workflow has a newer version waiting for your approval"; the open row is headed "Re-approve all updated workflows" and Dev Timeout reads `approved v3, v4 available`, above the approve-all plan |
| `recipes-page.png` | Workflow types: Dev Timeout v3 Approved, v4 Not approved, and the "Approve new workflows" button |
| `recipes-approve-new-landing.png` | that button pressed: Settings opens scrolled to step 3's open row, not the top of the page |
| `after-reapproved.png` | after the one approve-all (the owner's typed `approve-all <digest6>`, entered at the CLI): the 27 shipped workflows in the plan are approved (27 of 28) |

The 28th is the seed's own `dev-timeout` v4. It was registered by hand from a copy, so it is not a shipped workflow and not in
the approve-all plan, and its line still reads `approved v3, v4 available` in the last frame. On a real install the outdated
workflows are shipped ones and the same approve-all covers them.
