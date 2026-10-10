# API Contract -- Sindri CLI

The toolkit has no HTTP API since the MCP bridge was retired (R2 stage B). This file keeps the Sindri CLI's output contract.

## Sindri CLI

Its commands are listed in `docs/sindri/README.md`, and its error codes in `docs/sindri/errors.md`.

Output contract:

- State is reported in plain-text words (`ok`, `warn`, `fail`); no color is needed to read it.
- Every read command takes `--json`.
- Exit codes: `0` ok, `1` attention needed, `2` error.
- Errors carry a stable code, `SND-<AREA>-<NNN>`.
