#!/usr/bin/env bash
# alpha why is mentioned in a comment only
command -v alpha >/dev/null || exit 0
OUT="$(alpha why "$ID" 2>/dev/null || true)"
DIR="${AW_STATE_DIR:-$HOME/.agentic-workflow}/alpha/outbox"
echo "beta is not called here"
