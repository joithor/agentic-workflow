#!/usr/bin/env bash
set -euo pipefail
# HOOK-FAILOPEN: record why this hook is failing open (config/lib/failopen.sh,
# installed at <hooks>/lib/failopen.sh). Logging only: it runs in a subshell
# with its output discarded, so it can never change this hook's exit code,
# stdout or stderr, whatever set -e/-u or the environment do to it.
failopen() {
  (
    set +eu
    d="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)" || exit 0
    for f in "$d/lib/failopen.sh" "$d/../lib/failopen.sh"; do
      if [ -f "$f" ]; then . "$f" && aw_failopen detect-secrets "$@"; exit 0; fi
    done
  ) > /dev/null 2>&1 || true
}
# A crash (exit other than 0 or 2) lets the call through under Claude Code; count
# it. Under an adapter (AW_PROVIDER set) the adapter logs hook-exit-<N> instead.
# The trap doesn't call exit, so the hook's exit status is unchanged.
trap 'aw_rc=$?; [ -n "${AW_PROVIDER:-}" ] || case "$aw_rc" in 0|2) ;; *) failopen "exit-$aw_rc" ;; esac' EXIT

# PreToolUse hook: Aggressive secret detection in bash commands
# Exit 2 = deny, Exit 0 = allow.

INPUT=$(cat)
COMMAND=$(printf '%s\n' "$INPUT" | jq -r '.tool_input.command // empty' 2>/dev/null) || true
# No command to check: log why when the payload itself was unreadable.
if [ -z "$COMMAND" ]; then
  failopen input "$INPUT" '.tool_name == "Bash"'
  exit 0
fi

# AWS access keys (AKIA for permanent, ASIA for STS temporary credentials)
if printf '%s\n' "$COMMAND" | grep -qE 'A(KIA|SIA)[0-9A-Z]{16}'; then
  echo "BLOCKED: Detected what appears to be an AWS access key."
  echo "Suggestion: Store AWS credentials in ~/.aws/credentials or use environment variables from .env files."
  exit 2
fi

# GitHub tokens (ghp_, gho_, ghs_, ghu_, ghr_ prefixes and fine-grained PATs)
if printf '%s\n' "$COMMAND" | grep -qE '(gh[posur]_[A-Za-z0-9_]{36,}|github_pat_[A-Za-z0-9_]{22,})'; then
  echo "BLOCKED: Detected what appears to be a GitHub token."
  echo "Suggestion: Use 'gh auth login' or store tokens in .env files, not in commands."
  exit 2
fi

# Bearer tokens in curl headers
if printf '%s\n' "$COMMAND" | grep -qE 'curl\s+.*(-H|--header)\s+.*[Bb]earer\s+[A-Za-z0-9._/+=-]{20,}'; then
  echo "BLOCKED: Detected a Bearer token in a curl command."
  echo "Suggestion: Store tokens in environment variables and reference them as \$TOKEN."
  exit 2
fi

# Secret assignments with high-entropy values (>20 chars) — case-insensitive
if printf '%s\n' "$COMMAND" | grep -iqE "(export\s+)?(API_KEY|SECRET|TOKEN|PASSWORD|PRIVATE_KEY|ACCESS_KEY|SECRET_KEY)\s*=\s*['\"]?[A-Za-z0-9+/=_.,-]{20,}"; then
  echo "BLOCKED: Detected a secret being assigned in a command."
  echo "Suggestion: Store secrets in .env files or a secrets manager, not inline in commands."
  exit 2
fi

exit 0
