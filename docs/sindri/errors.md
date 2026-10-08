# Sindri error codes

Generated from `sindri/src/errors.ts` by `cd sindri && npm run gen`. Do not edit by hand.

| Code | Meaning | Fix |
|---|---|---|
| `SND-CLI-001` | Unknown command. | sindri help |
| `SND-CLI-002` | Invalid arguments for this command. | Run `sindri help` and check the command's flags. |
| `SND-CLI-900` | Unexpected internal error (a bug). | rerun with SINDRI_DEBUG=1 and report the output |
| `SND-ITEM-404` | No such item in the ledger. | Run `sindri observe` to list items. |
| `SND-LEDGER-001` | The ledger was written by a newer sindri. | Upgrade sindri (`scripts/install-sindri.sh` from the latest main), then rerun. |
| `SND-LOCK-001` | Another sindri run holds the lock. | wait a moment and rerun; `sindri doctor` shows the holder |
| `SND-LOCK-003` | This run's fencing epoch is stale; another run took over. | Nothing to do; the newer run continues. Check `sindri doctor` if this repeats. |
| `SND-PROFILE-001` | The profile is invalid. | Run `sindri profile validate` and fix each listed key. |
| `SND-PROFILE-002` | No profile was found. | Run `sindri profile init`, pass --profile &lt;dir&gt;, or set AW_PROFILE_DIR. |
| `SND-PROFILE-003` | No such profile key. | See docs/sindri/profile.md for the keys. |
| `SND-PROFILE-004` | No such repo in the profile. | Use a name listed under repos in profile.yaml. |
| `SND-PROFILE-005` | The profile was written for a newer sindri. | Upgrade sindri (`scripts/install-sindri.sh` from the latest main). |
| `SND-PROFILE-006` | The hash doesn't match the current profile. | Run `sindri profile approve` to see the current hash and diff, then approve that hash. |
| `SND-PROFILE-007` | A profile already exists there. | Edit it, or pass --force to overwrite it. |
| `SND-PROFILE-008` | This repo has no plan files for --ring0. | Run --ring0 from a repo with docs/superpowers/plans, or run `sindri profile init` without it. |
| `SND-PROFILE-009` | Not inside a git repo. | cd into the repo first. |
| `SND-PROFILE-010` | Approving a profile needs an interactive terminal. | run `sindri profile approve <hash>` yourself, in a terminal |
| `SND-PROFILE-011` | The approval was not confirmed. | rerun and type the first 6 characters of the hash |
| `SND-SCRUB-001` | A profile scrub pattern does not compile. | Fix the regex at the named scrub.extraPatterns index, then run `sindri profile validate`. |
| `SND-SCRUB-002` | Staged changes contain likely secrets. | remove them (use a secret pointer or an env var); for a false positive, commit with --no-verify and say why |
| `SND-SCRUB-003` | A different pre-commit hook is already installed. | Add `sindri scrub --staged \|\| exit 1` to that hook by hand. |
| `SND-SCRUB-004` | `scrub --staged` or `--install-pre-commit` ran outside a git repo. | cd into the repo first, or pass --repo PATH to --install-pre-commit. |
| `SND-TRACKER-001` | The tracker's source is missing (for plan-file: the repo path or plan dir). | Restore that directory, or fix `path` in repos/&lt;name&gt;.yaml and run `sindri profile approve`. |
| `SND-TRACKER-404` | The tracker has no such item. | Check the id with `sindri observe`. |
| `SND-TRACKER-405` | This tracker can't write. | The plan-file tracker is read-only; edit the plan file. |
