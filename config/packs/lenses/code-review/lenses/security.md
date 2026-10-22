# Security lens

Read the diff for exploitable weaknesses. Treat every input as untrusted until the code says otherwise.

Look for:
- Missing or bypassable authentication and authorisation checks on a changed path.
- Injection: queries, shell commands, file paths, templates, deserialisation, and text placed in a prompt to a model.
- Secrets or personal data in code, logs, errors, responses or test fixtures.
- Unsafe defaults, disabled verification, wildcard permissions and open cross-origin settings.
- Unvalidated redirects, uploads, sizes and rates.
- New dependencies, and whether the change widens what they can reach.
- Output from a model or an external service used without validation.

Severities:
- blocking: an exploitable path to data or actions, or a committed secret.
- issue: a missing check or an unsafe pattern on a real path that must be fixed before merge.
- suggestion: hardening that reduces risk without a known exploit.
- nit: a comment or naming point about a security-relevant line.
