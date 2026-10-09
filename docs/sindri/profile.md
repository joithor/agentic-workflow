# Sindri profile reference

Generated from `sindri/src/profile/schema.ts` by `cd sindri && npm run gen`. Do not edit by hand.
Editor autocomplete: point your YAML extension at `sindri/schema/profile.schema.json` and `repo.schema.json`.

Precedence: `repos/<repo>.yaml` `overrides` > `profile.yaml` > core default (`sindri profile explain <key> [--repo <name>]`).
A changed profile takes effect only after `sindri profile approve <hash>` (spec §8.7).

## profile.yaml

| Key | Type | Required | Default | Meaning |
|---|---|---|---|---|
| `schemaVersion` | `1` | yes |  | Profile format version |
| `mode` | `shadow` \| `assist` \| `auto-small` | no | `"shadow"` | What sindri may do on its own (spec §7.1) |
| `user` | string | yes |  | Namespace for claim refs sindri/&lt;user&gt;/&lt;item&gt; (spec §9.4) |
| `hosts` | object | yes |  | hosts.active: the one host allowed to run ticks |
| `tracker` | object (one of 1 shapes) | yes |  | Where work items come from. Plan 2 ships type: plan-file |
| `repos` | string[] | yes |  | Repo names; each needs repos/&lt;name&gt;.yaml |
| `trustedAuthors` | string[] | no | `[]` | Author ids whose items may auto-start (spec §8.3). For plan-file these are git author emails, which anyone can forge; signed commits are required before auto-small |
| `trustedBots` | string[] | no | `[]` | Bot ids whose review comments feed fix rounds |
| `providers` | object | no | `{}` | providers.allowed: model providers sindri may call (spec §6.1) |
| `budget` | object | no | `{}` | Token budgets; unset until rollout step 3a enforces them |
| `autoStartMaxSize` | `XS` \| `S` \| `M` \| `L` \| `XL` | no | `"XS"` | Largest size auto-small may start |
| `selfMerge` | `human` \| `auto` | no | `"human"` | Who merges toolkit PRs (spec §7.7); protected modules always wait for the human |
| `scrub` | object | no | `{}` | scrub.extraPatterns: extra secret shapes, added to the built-ins (never removes one) |
| `index` | object | no | `{}` | Code index (spec §6.2) |
| `shape` | object | no | `{}` | Shape signals (spec §6.2) |
| `sources` | object | no | `{}` |  |
| `models` | object | no | `{}` |  |
| `evolve` | object | no | `{}` | Self-evolution budgets and limits (sindri evolve) |
| `privacy` | object | no | `{}` | Privacy gate for published proposal tasks |
| `scope` | object | no | `{}` |  |

## `repos/<name>.yaml`

| Key | Type | Required | Default | Meaning |
|---|---|---|---|---|
| `schemaVersion` | `1` | yes |  | Profile format version |
| `name` | string | yes |  | Must match the file name repos/&lt;name&gt;.yaml |
| `path` | string | yes |  | Absolute path of the local checkout |
| `defaultBranch` | string | no | `"main"` | Base branch for claims and indexes |
| `protectedPaths` | string[] | no | `[]` | Globs; a diff touching one parks for approval (spec §8.5) |
| `overrides` | object | no | `{}` | Per-repo values that win over profile.yaml |
| `index` | object | no | `{}` | index.denyPaths for this repo, added to the profile's |
