# Sindri error codes

Generated from `sindri/src/errors.ts` by `cd sindri && npm run gen`. Do not edit by hand.

| Code | Meaning | Fix |
|---|---|---|
| `SND-CLI-001` | Unknown command. | sindri help |
| `SND-CLI-900` | Unexpected internal error (a bug). | rerun with SINDRI_DEBUG=1 and report the output |
| `SND-LEDGER-001` | The ledger was written by a newer sindri. | Upgrade sindri (`scripts/install-sindri.sh` from the latest main), then rerun. |
| `SND-LOCK-003` | This run's fencing epoch is stale; another run took over. | Nothing to do; the newer run continues. Check `sindri doctor` if this repeats. |
| `SND-SCRUB-001` | A profile scrub pattern does not compile. | Fix the regex at the named scrub.extraPatterns index, then run `sindri profile validate`. |
