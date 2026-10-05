export type GateName = 'scope-gate' | 'done-gate' | 'send-gate'

export type GateStats = {
  question: string
  fired: number
  byDecision: Record<string, number>
  p50LatencyMs: number
}

export type JudgeLatest = {
  id: string
  ts: string
  question: string
  decision: string
  provider: string
  confidence: number
  /** The eval_items id for this decision, once `judge label import` has made one. */
  itemId: string | null
}

export type JudgeLive =
  | { state: 'none' }
  | { state: 'unscoped' }
  | {
      state: 'ok'
      calls: number
      byProvider: Record<string, number>
      undecided: number
      agreed: number
      overrode: number
      p50LatencyMs: number
      p95LatencyMs: number
      gates: Record<GateName, GateStats>
      labelable: boolean
      latest: JudgeLatest | null
    }

export type LiveSnapshot = {
  v: 1
  sessionId: string
  at: string
  transcript: { found: boolean; files: number }
  /** readErrors: transcript files the scorer could not read this pass; the numbers may be stale. */
  ingest: { newLines: number; ms: number; readErrors: number }
  context: { tokens: number | null; window: number; percent: number | null }
  usage: { calls: number; subagentCalls: number; contextTokens: number; outputTokens: number; callsOver200k: number }
  judge: JudgeLive
  contextGuard: { fires: number }
  wakes: { queuedNow: number }
}

/** What the host itself reports; shown in preference to the transcript's estimate. */
export type HostUsage = { usd: number | null; percent: number | null; window: number | null }

export type LiveStatus = { ok: boolean; at: number }

declare module 'claude-code' {
  interface PluginState {
    'aw-live': { snapshot: LiveSnapshot | null; host: HostUsage | null; status: LiveStatus | null }
  }
}
