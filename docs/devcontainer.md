# Devcontainer

`.devcontainer/` is a development environment **for humans**. It is never the agent sandbox: it has no
Docker-in-Docker, no host Docker socket and no privileged mode, because Docker-in-Docker means root on the host
(readiness spec §4d). Agent sessions keep using the Sindri containers (main spec §8.2), which this image is not.

## What's inside

| Tool | Version | Pinned by |
|---|---|---|
| .NET SDK | 10.0.401 (the `global.json` pin, `rollForward: disable`) | the base image `mcr.microsoft.com/dotnet/sdk:10.0.401-noble`, by digest |
| Node | 22.23.3 (`.nvmrc` says 22) | version and the release's SHA-256, per architecture |
| uv | 0.12.17 | the `ghcr.io/astral-sh/uv` image, by digest |
| git, jq, sqlite3, shellcheck, shfmt, python3, build tools | Ubuntu 24.04 packages | the base image's apt sources |

It runs as the non-root `ubuntu` user. `AW_STATE_DIR` points at `/home/ubuntu/.agentic-workflow` inside the
container, so a session there never touches the host's toolkit state. After creation it prints the tool
versions and runs a locked `dotnet restore` (`.devcontainer/post-create.sh`); the TS packages stay
`npm ci` per package, on demand.

`scripts/tests/repo-templates.test.sh` checks that the image tag matches `global.json`, the Node major matches
`.nvmrc`, every image is pinned by digest, and nothing adds Docker-in-Docker, a socket mount or privileges.

## What it deliberately doesn't do

- **Serena and the sindri containers.** They need Docker, and the devcontainer has none (no Docker-in-Docker,
  above). Run them from the host.
- **`setup.sh`.** The installed toolkit lives on the host (it wires hooks into Claude Code, Codex and Cursor
  there). The devcontainer is for building and testing the code.

## Optional services, on the host

Both run on the host, not inside the container; the container reaches them through `host.docker.internal`.

- **Ollama** (the code index's embeddings): install it on the host and set `OLLAMA_HOST=http://host.docker.internal:11434`
  in the container.
- **A local OpenTelemetry collector** (the exporter is off by default, spec §5 Level 3): start one on the host,
  for example the `otel/opentelemetry-collector` image with its default OTLP ports, and point the C# build's
  exporter at `http://host.docker.internal:4318` once you turn it on.

## Environment variables

`.env.example` lists every variable the code reads, with its default. The container sets only `AW_STATE_DIR`,
`DOTNET_CLI_TELEMETRY_OPTOUT` and `DOTNET_NOLOGO`.
