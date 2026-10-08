// Secret and identifier shapes (spec §8.4): config/hooks/detect-secrets.sh,
// judge/src/redact.ts, plus private keys, credentialed URLs and MRN/SSN shapes.
// Order matters: when two patterns overlap, the earlier one names the hit.
// Deliberately absent: judge's bare 40-hex rule, which would erase git SHAs.
export interface ScrubPattern {
  kind: string;
  re: RegExp;
  valueGroup?: number;
}

export const BUILTIN_PATTERNS: readonly ScrubPattern[] = [
  // Bounded span: an unterminated BEGIN can't make the scan quadratic.
  { kind: "private-key", re: /-----BEGIN [A-Z ]{0,40}PRIVATE KEY-----[\s\S]{0,16384}?-----END [A-Z ]{0,40}PRIVATE KEY-----/g },
  { kind: "anthropic-key", re: /\bsk-ant-[A-Za-z0-9_-]{20,}/g },
  { kind: "openai-key", re: /\bsk-(?:proj-)?[A-Za-z0-9_-]{20,}/g },
  { kind: "aws-access-key", re: /\bA(?:KIA|SIA)[0-9A-Z]{16}\b/g },
  { kind: "github-token", re: /\b(?:gh[pousr]_[A-Za-z0-9_]{30,}|github_pat_[A-Za-z0-9_]{22,})/g },
  { kind: "slack-token", re: /\bxox[abprs]-[A-Za-z0-9-]{10,}/g },
  { kind: "stripe-key", re: /\b[sr]k_(?:live|test)_[A-Za-z0-9]{16,}/g },
  { kind: "google-api-key", re: /\bAIza[0-9A-Za-z_-]{35}\b/g },
  { kind: "linear-key", re: /\blin_(?:api|oauth)_[A-Za-z0-9]{32,}/g },
  { kind: "jwt", re: /\beyJ[A-Za-z0-9_-]{8,4096}\.[A-Za-z0-9_-]{8,4096}\.[A-Za-z0-9_-]{8,4096}/g },
  { kind: "bearer", re: /\bBearer\s+([A-Za-z0-9._~+/=-]{20,})/gi, valueGroup: 1 },
  { kind: "credentialed-url", re: /\b[a-z][a-z0-9+.-]{0,30}:\/\/[^\s/:@]{1,256}:([^\s/@]{1,256})@/gi, valueGroup: 1 },
  {
    // Code style (spec §8.4, Review Focus 5): NAME [=:] "value". NAME ends in a secret
    // word in any case (camelCase, snake_case, hyphenated, or a quoted JSON key). Only
    // a QUOTED value counts, so identifiers, member access and calls never do
    // (`refreshToken: RefreshTokenSchemaV2`, `token = generateToken256Bits()`). The
    // value needs a digit in its first 256 chars (bounded, so a long run stays linear).
    kind: "secret-assignment",
    re: /(?<![A-Za-z0-9_-])[A-Za-z0-9_-]*?(?:API[_-]?KEY|SECRET(?:[_-]?KEY)?|TOKEN|PASSWORD|PRIVATE[_-]?KEY|ACCESS[_-]?KEY)['"]?\s*[=:]\s*['"](?=[A-Za-z0-9+/=_-]{0,255}?\d)([A-Za-z0-9+/=_-]{20,})/gi,
    valueGroup: 1,
  },
  {
    // Env style: an UPPER_SNAKE name (API_KEY=..., export X_TOKEN=...) may take an
    // unquoted value. Case-sensitive, so camelCase code never matches here.
    kind: "secret-assignment",
    re: /(?<![A-Za-z0-9_-])(?:[A-Z0-9]+_)*(?:API_?KEY|SECRET(?:_KEY)?|TOKEN|PASSWORD|PRIVATE_KEY|ACCESS_KEY)\s*[=:]\s*(?=[A-Za-z0-9+/=_-]{0,255}?\d)([A-Za-z0-9+/=_-]{20,})/g,
    valueGroup: 1,
  },
  {
    // Header style: a hyphenated header name (X-API-Key: ...) may take an unquoted value.
    kind: "secret-assignment",
    re: /(?<![A-Za-z0-9_-])(?:[A-Za-z0-9]+-)+(?:API-?KEY|SECRET|TOKEN|PASSWORD|ACCESS-?KEY|PRIVATE-?KEY):\s*(?=[A-Za-z0-9+/=_-]{0,255}?\d)([A-Za-z0-9+/=_-]{20,})/gi,
    valueGroup: 1,
  },
  { kind: "ssn", re: /\b\d{3}-\d{2}-\d{4}\b/g },
  { kind: "mrn", re: /\bMRN[\s:#-]*(\d{6,10})\b/gi, valueGroup: 1 },
];
