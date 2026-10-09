# Sindri error codes

Generated from `sindri/src/errors.ts` by `cd sindri && npm run gen`. Do not edit by hand.

| Code | Meaning | Fix |
|---|---|---|
| `SND-CLI-001` | Unknown command. | sindri help |
| `SND-CLI-002` | Invalid arguments for this command. | Run `sindri help` and check the command's flags. |
| `SND-CLI-900` | Unexpected internal error (a bug). | rerun with SINDRI_DEBUG=1 and report the output |
| `SND-INDEX-001` | The heavy-job lock is busy. | wait for the holder to finish; `sindri doctor` shows it |
| `SND-INDEX-002` | The repo path is not a git repo. | check repos/&lt;name&gt;.yaml path, then sindri profile approve |
| `SND-INDEX-003` | The index input is larger than index.maxTotalMB. | add generated or vendored paths to index.denyPaths, or raise index.maxTotalMB |
| `SND-INDEX-005` | The embedding URL is not loopback. | set index.embeddings.url to http://127.0.0.1:11434 (or disable embeddings) |
| `SND-INDEX-006` | The local embedding server failed. | sindri index setup (starts Ollama checks and pulls the model) |
| `SND-INDEX-007` | No network sandbox is available for graphify. | macOS: sandbox-exec ships with the OS; Linux: install bubblewrap (bwrap), or set index.graph: none |
| `SND-INDEX-008` | graphify is missing, failed or wrote no usable graph. | if graphify or its sandbox is missing: sindri index setup; if graph.json is over the cap: raise index.graphMaxMB (max 512); then sindri index build |
| `SND-INDEX-404` | No index has been built for this repo. | sindri index build --repo &lt;name&gt; |
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
| `SND-PROFILE-012` | No profile has been approved yet. | sindri profile approve |
| `SND-PROFILE-013` | That repo name is already used for another path or file. | pass --name &lt;another name&gt; |
| `SND-PROFILE-014` | That repo name is not valid. | use lowercase letters, digits and dashes (max 39) |
| `SND-SCOPE-001` | No allowed provider can run scoping. | add anthropic to providers.allowed, then sindri profile approve |
| `SND-SCOPE-002` | A model job failed, timed out or returned junk. | rerun; the message carries the CLI's own error. If it repeats, run `claude -p hello` to check the CLI and its login |
| `SND-SCOPE-004` | The model's answer didn't match the required shape. | rerun; the next round gets the reasons. Persistent: try another models.scoping |
| `SND-SCOPE-005` | The run's token budget is used up. | raise scope.maxTokensPerRun (or maxTokensPerBacktest) in the profile, then sindri profile approve |
| `SND-SCOPE-010` | Linear rejected the token. | check sources.linear.token points at a valid read-only Linear API key |
| `SND-SCOPE-011` | Linear could not be read. | check the project URL and your network, then rerun; the message carries Linear's own error text |
| `SND-SCOPE-020` | The brief file can't be read. | check the path you passed to sindri scope |
| `SND-SCOPE-021` | No output directory for the scope map. | pass --out DIR or set sources.notesDir |
| `SND-SCOPE-022` | That section isn't in the document. | check the heading number (## 13. …) |
| `SND-SCOPE-024` | Linear isn't configured as a source. | add sources.linear.token (a secret pointer), then sindri profile approve |
| `SND-SCOPE-025` | Refusing to write a scope map into a git worktree. | use --sources file,code, or write outside the repo |
| `SND-SCRUB-001` | A profile scrub pattern does not compile. | Fix the regex at the named scrub.extraPatterns index, then run `sindri profile validate`. |
| `SND-SCRUB-002` | Staged changes contain likely secrets. | remove them (use a secret pointer or an env var); for a false positive, commit with --no-verify and say why |
| `SND-SCRUB-003` | A different pre-commit hook is already installed. | Add both lines to that hook by hand, in this order: `sindri scrub --staged \|\| exit 1` then `sindri shape --record --staged \|\| true`. |
| `SND-SCRUB-004` | `scrub --staged` or `--install-pre-commit` ran outside a git repo. | cd into the repo first, or pass --repo PATH to --install-pre-commit. |
| `SND-SCRUB-005` | `git diff --cached` failed, so `scrub --staged` could not scan the staged changes. | Fix the git error shown in the details, then retry the commit. |
| `SND-SECRET-001` | A secret pointer could not be resolved. | check the pointer in the profile and that the secret exists (env var, file, keychain item or 1Password item) |
| `SND-SECRET-002` | A secret file is readable by other users. | chmod 600 &lt;file&gt; |
| `SND-TRACKER-001` | The tracker's source is missing or unreadable (for plan-file: the repo path or plan dir). | Restore that directory, or fix `path` in repos/&lt;name&gt;.yaml and run `sindri profile approve`. |
| `SND-TRACKER-002` | The tracker's source holds no items to read (for plan-file: no plan file matches tracker.include). | Fix tracker.include in profile.yaml (or restore the plan files), then run `sindri profile approve`. |
| `SND-TRACKER-003` | A plan file is too big to read (over 2 MiB). | Split the plan file into smaller plans. |
| `SND-TRACKER-404` | The tracker has no such item. | Check the id with `sindri observe`. |
| `SND-TRACKER-405` | This tracker can't write. | The plan-file tracker is read-only; edit the plan file. |
