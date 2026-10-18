Adds `--json` to print the results as one JSON array.

| Acceptance | Evidence |
|---|---|
| `tally --json a.txt` prints `[{"file":"a.txt","count":N}]` | `test/format.test.js`, `ci/check.sh` at @PROVEN@ |

Not proven: the empty-file case.
