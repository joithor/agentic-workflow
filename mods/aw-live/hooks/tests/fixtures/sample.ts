// A synthetic `scorer live --json` result. scorer/tests/live-contract.test.ts reads this
// file as text, takes the JSON between the backticks and validates it against the zod
// schema, so edit the JSON, not the delimiters.
export const SAMPLE_JSON = `{
  "v": 1,
  "sessionId": "session-1",
  "at": "2026-10-04T12:00:00.000Z",
  "transcript": { "found": true, "files": 2 },
  "ingest": { "newLines": 12, "ms": 31, "readErrors": 0 },
  "context": { "tokens": 84000, "window": 200000, "percent": 42 },
  "usage": { "calls": 31, "subagentCalls": 9, "contextTokens": 1900000, "outputTokens": 52000, "callsOver200k": 2 },
  "judge": {
    "state": "ok",
    "calls": 12,
    "byProvider": { "jev": 10, "rules": 2 },
    "undecided": 3,
    "agreed": 6,
    "overrode": 3,
    "p50LatencyMs": 380,
    "p95LatencyMs": 6100,
    "gates": {
      "scope-gate": { "question": "brief-scope", "fired": 4, "byDecision": { "ready": 3, "missing": 1 }, "p50LatencyMs": 400 },
      "done-gate": { "question": "ask-check", "fired": 5, "byDecision": { "continue": 4, "ask": 1 }, "p50LatencyMs": 350 },
      "send-gate": { "question": "wake-gate", "fired": 3, "byDecision": { "send": 2, "batch": 1 }, "p50LatencyMs": 360 }
    },
    "labelable": true,
    "latest": { "id": "d-9", "ts": "2026-10-04T11:59:00.000Z", "question": "ask-check", "decision": "continue", "provider": "jev", "confidence": 0.9, "itemId": "item-9" }
  },
  "contextGuard": { "fires": 1 },
  "wakes": { "queuedNow": 2 }
}`
