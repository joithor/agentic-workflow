# Aw.Redaction

The one secret-pattern set (spec §4c "One pattern source", §4e B10). Tier 0, sublevel 0 in `deps.yaml`; the BCL
only. It replaces the two TS copies (judge `src/redact.ts`, sindri `src/scrub/patterns.ts`) when those packages are
ported; the TS scrubbers stay frozen until then. `Aw.Kernel.Telemetry`'s scrubbing processor uses it on every log and
export path.

## API

```csharp
IRedactor redactor = Redactor.Default;            // built-in rules, 1 s match timeout per pattern
ScrubResult result = redactor.Scrub(text);
// result.Text     scrubbed text; each hit becomes [REDACTED:<rule>]
// result.Hits     RedactionHit(Rule, Start, End) in input order: spans in the original text, never the value
// result.Outcome  Clean (Text is the input, same instance) | Redacted | Blocked
// result.BlockCode AW-REDACT-001 when Blocked
```

- `Redactor.Create(TimeSpan)` gives the same rules with another per-pattern timeout.
- Register `IRedactor` as a singleton (`Redactor.Default`) in the composing `Add…()` method; tests use the real
  redactor (it has no I/O, so it needs no fake).

## Rules

| Rule id | Catches |
|---|---|
| `private-key` | `-----BEGIN … PRIVATE KEY-----` through `-----END …-----`; without an END line, the base64 and `Proc-Type`/`DEK-Info` lines after BEGIN |
| `anthropic-key`, `openai-key` | `sk-ant-…`, `sk-…`, `sk-proj-…` |
| `aws-access-key` | `AKIA…` and `ASIA…` (STS) access key ids |
| `github-token` | `ghp_`, `gho_`, `ghu_`, `ghs_`, `ghr_`, `github_pat_` |
| `slack-token`, `stripe-key`, `google-api-key`, `linear-key`, `jwt` | `xox[abprs]-`, `sk_live_`/`rk_test_`…, `AIza…`, `lin_api_`/`lin_oauth_`, three-part `eyJ…` tokens |
| `bearer` | the token after `Bearer` (the word stays) |
| `credentialed-url` | the password in `scheme://user:password@host` |
| `secret-assignment` | the value of `NAME = "value"` (NAME ends in API_KEY, SECRET, TOKEN, PASSWORD, PRIVATE_KEY, ACCESS_KEY; any case, quoted value), `UPPER_SNAKE=value`, `X-Api-Key: value` headers, and `aws_secret_access_key` / `aws_session_token` lines; a value needs 20+ characters with a digit |
| `ssn`, `mrn` | `123-45-6789`, `MRN: 12345678` |

Overlapping hits merge into one placeholder named by the earlier rule (`Redactor.RuleIds` is the priority order). A
`[REDACTED:<rule>]` placeholder naming one of these rules is never matched again, so scrubbing is idempotent; a crafted
placeholder holding anything else is still scanned. Not a rule: bare 40+ hex runs (judge's TS rule), which erase git
SHAs and digests and match no secret format.

## Evasions it folds away

The rules run on a folded copy of the text; hits are mapped back to the original spans, and only the original is
rewritten:

- zero-width characters (U+200B–U+200D, U+2060–U+2064, U+FEFF, U+180E) and soft hyphens are dropped, so a joiner
  inside a token doesn't split it;
- Unicode spaces (NBSP and the U+2000 block) become a space; fullwidth ASCII (U+FF01–U+FF5E) and Cyrillic and Greek
  letters that look like Latin ones become ASCII (`gһp_`, `ΑΚΙΑ`);
- JSON escapes (`\n`, `\r`, `\t`, `\"`, `\/`, `\\`, `\uXXXX`) are decoded, so a token right after an escaped newline
  in serialized JSON still starts a word, and an escaped `client_secret` pair still matches.

Not folded: mathematical alphanumeric look-alikes (astral code points) and base64-wrapped secrets.

## Timeouts block

Every pattern is a `Regex` with a match timeout. A timeout returns `ScrubOutcome.Blocked` with
`Text = "[REDACTED:blocked]"` and `BlockCode = "AW-REDACT-001"`: the input is withheld, never passed on. The caller
logs the block (Redaction has no logger) and refuses the commit, write or egress.

## Pattern rules

Explicit ASCII classes (`[0-9]`, `[A-Za-z0-9_]`) and lookarounds instead of `\d`, `\s`, `\w` and `\b`, which are
Unicode-aware in .NET; `RegexOptions.CultureInvariant` and `ExplicitCapture` on every pattern; the value of a pair is
the named group `v`; no `RegexOptions.NonBacktracking` (the patterns use lookarounds). A new rule needs a corpus case
in `tests/Aw.Redaction.Tests/Corpus.cs` (assembled from parts, so no scanner sees a whole token in the source) and,
when it is a new kind, a row above.

## AOT

`src/Redaction/Aw.Redaction.AotSmoke/run.sh` publishes a NativeAOT canary, fails on any IL2xxx/IL3xxx warning and
compares its output with `expected.txt`; CI's .NET job runs it. `Aw.Redaction.AotSmoke --stdin` scrubs standard input
(text to stdout, outcome and hits to stderr; exit 3 when blocked).
