# Onyx for Sindri's context engine

Sindri's `onyx` source reads business intent (Linear issues with their comments, Slack, docs spaces) from a local Onyx
Community Edition. Onyx stays unmodified; Sindri uses it for recall only and does its own ranking (context spec D2,
D3). This page sets Onyx up so it is pinned, bound to loopback, keeps all data on the machine, and gives Sindri one
read key. Connecting Onyx to company Linear and Slack uses company OAuth: get sign-off first. Sindri never connects a
source itself.

`$SINDRI` is the C# Sindri build (before the switch, `~/.local/bin/sindri` is still the TS one); see
[context.md](context.md).

## 1. Pin the release

Sindri is tested against **Onyx `v4.9.0`**. Use that tag, never `latest`:

```bash
git clone --depth 1 --branch v4.9.0 https://github.com/onyx-dot-app/onyx.git ~/onyx
cd ~/onyx/deployment/docker_compose
cp env.template .env
```

In `.env` set:

```bash
IMAGE_TAG=v4.9.0
DISABLE_TELEMETRY=true
```

The profile records the same tag (`context.sources.onyx.version`), and `$SINDRI doctor` (`context:onyx`) compares it with
what Onyx reports at `GET /api/version`. Changing the Onyx version means changing both, then re-running the manual
checks in §6.

## 2. Bind every published port to loopback

The stock compose file publishes nginx on all interfaces (`"${HOST_PORT:-3000}:80"` and `"80:80"`). Add a
`docker-compose.override.yml` beside it:

```yaml
services:
  nginx:
    ports: !override
      - "127.0.0.1:3000:80"
```

Leave every other service's `ports:` commented out, as shipped. Start it:

```bash
docker compose up -d
lsof -nP -iTCP:3000 -sTCP:LISTEN     # must show 127.0.0.1:3000 only
```

## 3. Keep models local

- Embeddings: the bundled `inference_model_server` and `indexing_model_server` (the defaults). In Admin → Search
  Settings, keep a self-hosted embedding model; configure **no** cloud embedding provider.
- LLM: configure **no** cloud LLM provider. Sindri's search route needs no LLM.
- Leave web search and image generation unconfigured.

## 4. Scope the connectors

Create only these, in Admin → Connectors:

| Connector | Scope |
|---|---|
| Linear | Start date early enough to cover the backtest projects; the product's teams only |
| Slack | The product's channels only (connector-indexed; Sindri doesn't use the federated lane) |
| Docs spaces (Confluence, Drive, …) | The product's spaces only |

First indexing time shows in `$SINDRI context sources status`, read from Onyx's connector status.

## 5. The key

Create a **dedicated service account** in Admin → API Keys named `sindri-read` and copy its key (`on_…`).
The search route Sindri uses (`POST /api/admin/search`, the only v4.9.0 CE route that returns document ids without
an LLM) needs an **admin** key, so this key could change Onyx settings (owner decision 2026-10-12, context spec §8.8).
Sindri only ever sends the five requests in §7; its Onyx adapter refuses any other request before sending it, and a
test proves it. Keep the key for Sindri alone, on its own service account: doctor reads the key's account through
`GET /api/me` and warns (`SND-CONTEXT-053`) when the admin key belongs to a person's account.

Store it by pointer, never in the profile:

```bash
security add-generic-password -s onyx -a sindri-read -w      # prompts for the key
```

```yaml
context:
  sources:
    onyx: { url: "http://127.0.0.1:3000", token: "keychain:onyx/sindri-read", version: "v4.9.0" }
    linear: { teams: [ENG] }        # team keys whose ENG-n identifiers become aliases
```

Pointers Sindri accepts: `keychain:<service>/<account>`, `env:<VAR>`, `file:<absolute path>` (mode 0600, owned by you).
A value without one of these schemes is refused (`SND-CONTEXT-002`). Then `$SINDRI profile validate` and
`$SINDRI profile approve` at a terminal.

## 6. Check it

```bash
$SINDRI doctor            # context:onyx — loopback url, version v4.9.0, the key can search
$SINDRI context sources status
```

| Code | Meaning | Fix |
|---|---|---|
| `SND-CONTEXT-050` | Onyx refused the key (403: missing, wrong, or not admin) | Recreate the key as in §5 and update the pointer |
| `SND-CONTEXT-053` | The admin key isn't a service account's | Create a service account for Sindri (§5) |
| `SND-CONTEXT-055` | `onyx.url` isn't loopback, or Onyx reports a version other than the pinned one | §1, §2 |
| `SND-CONTEXT-051` | Onyx answered with a shape the pinned v4.9.0 doesn't have | Check `version` against what Onyx runs (§1) |
| `SND-CONTEXT-052` | Onyx has no document with that id any more | Nothing; the citation stays a reference |
| `SND-CONTEXT-010` | Onyx didn't answer; queries continue without it (`degraded`) | `docker compose ps`, `sources status` |

**Manual checks on every Onyx version change** (a search key can't read these): Admin → LLM shows no cloud provider;
Admin → Search Settings shows a self-hosted embedding model; `.env` has `DISABLE_TELEMETRY=true`; §2's `lsof` shows
loopback only.

## 7. What Sindri calls

| Request | Why |
|---|---|
| `POST /api/admin/search` `{query, filters:{source_type, time_cutoff}}` | Candidates: document ids, titles, links, `updated_at` |
| `GET /api/document/document-size-info?document_id=` and `GET /api/document/chunk-info?document_id=&chunk_id=` | A cited record's text |
| `GET /api/version` | Doctor's version check (no key sent) |
| `GET /api/me` | Doctor shows the key's role and account type |

Nothing else: the adapter refuses any other request before sending it. Linear issues key on Linear's issue UUID
(Onyx's document id). The `ENG-482`-style identifier is stored as an alias,
read from Onyx's title prefix `[ENG-482]` and checked against the issue link. Onyx doesn't serve `updated_at` by
id, so Onyx citations show `version unchecked` until the native Linear source versions them.
