// Pure formatting for the pane and `/live status`. No `$` in here.

import type { HostUsage, LiveSnapshot } from '../../types'

export type Tone = 'ok' | 'warn' | 'dim'
export type Row = { label: string; value: string; tone: Tone }
export type Section = { title: string; rows: Row[] }

export const WARN_PERCENT = 70

export const fmtTokens = (n: number): string => {
  if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`
  if (n >= 1_000) return `${Math.round(n / 1_000)}k`
  return String(n)
}

/** `▓▓▓▓░░░░░░` for a percent; empty cells when unknown. */
export const bar = (percent: number | null, width: number): string => {
  const filled = percent === null ? 0 : Math.round((Math.min(100, Math.max(0, percent)) / 100) * width)
  return '▓'.repeat(filled) + '░'.repeat(width - filled)
}

/** The host's own context percent when it has one, else the transcript's estimate. */
export const contextPercent = (s: LiveSnapshot, host: HostUsage | null): number | null =>
  host?.percent ?? s.context.percent

/** `jev 83% · rules 17%` for the two biggest deciders; empty with none. */
export const providerShare = (byProvider: Readonly<Record<string, number>>): string => {
  const entries = Object.entries(byProvider).sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]))
  const total = entries.reduce((sum, [, n]) => sum + n, 0)
  return entries.slice(0, 2).map(([name, n]) => `${name} ${Math.round((n / total) * 100)}%`).join(' · ')
}

const GATE_ORDER = ['scope-gate', 'done-gate', 'send-gate'] as const

const decisionList = (byDecision: Readonly<Record<string, number>>): string =>
  Object.entries(byDecision).sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0])).map(([d, n]) => `${d} ${n}`).join(' · ')

/** The pane's cards, top to bottom. */
export const paneSections = (s: LiveSnapshot, host: HostUsage | null): Section[] => {
  const percent = contextPercent(s, host)
  const window = host?.window ?? s.context.window
  const context: Section = {
    title: 'context',
    rows: [
      { label: 'window', value: `${percent === null ? '--' : `${Math.round(percent)}%`} ${bar(percent, 10)}`, tone: percent !== null && percent >= WARN_PERCENT ? 'warn' : 'ok' },
      { label: 'tokens', value: `${s.context.tokens === null ? '--' : fmtTokens(s.context.tokens)} of ${fmtTokens(window)}`, tone: 'dim' },
    ],
  }
  const session: Section = {
    title: 'session',
    rows: [
      { label: 'cost', value: host?.usd == null ? '--' : `$${host.usd.toFixed(2)}`, tone: 'ok' },
      { label: 'tokens', value: `${fmtTokens(s.usage.contextTokens)} in · ${fmtTokens(s.usage.outputTokens)} out`, tone: 'dim' },
      { label: 'calls', value: `${s.usage.calls} (${s.usage.subagentCalls} subagent)`, tone: 'dim' },
      { label: 'over 200k', value: String(s.usage.callsOver200k), tone: s.usage.callsOver200k > 0 ? 'warn' : 'dim' },
    ],
  }
  if (s.ingest.readErrors > 0) {
    session.rows.push({ label: 'warning', value: `stale: ${s.ingest.readErrors} transcript read error${s.ingest.readErrors === 1 ? '' : 's'}`, tone: 'warn' })
  }
  const sections = [context, session]
  if (s.judge.state === 'unscoped') {
    sections.push({ title: 'judge', rows: [{ label: 'sessions', value: 'not linked yet (judge predates decision_details.session_id)', tone: 'dim' }] })
  }
  if (s.judge.state === 'ok') {
    const j = s.judge
    sections.push({
      title: 'judge',
      rows: [
        { label: 'calls', value: `${j.calls}${providerShare(j.byProvider) === '' ? '' : ` · ${providerShare(j.byProvider)}`}`, tone: 'ok' },
        { label: 'vs rules', value: `agreed ${j.agreed} · overrode ${j.overrode} · unsure ${j.undecided}`, tone: j.undecided > 0 ? 'warn' : 'dim' },
        { label: 'latency', value: `p50 ${j.p50LatencyMs} ms · p95 ${j.p95LatencyMs} ms`, tone: 'dim' },
      ],
    })
    sections.push({
      title: 'gates',
      rows: GATE_ORDER.map((name): Row => {
        const g = j.gates[name]
        return {
          label: name,
          value: g.fired === 0 ? 'quiet' : `${g.fired} · ${decisionList(g.byDecision)} · p50 ${g.p50LatencyMs} ms`,
          tone: g.fired === 0 ? 'dim' : 'ok',
        }
      }),
    })
  }
  sections.push({
    title: 'wakes',
    rows: [
      { label: 'queued now', value: String(s.wakes.queuedNow), tone: s.wakes.queuedNow > 0 ? 'warn' : 'dim' },
      { label: 'context guard', value: `${s.contextGuard.fires} fires`, tone: s.contextGuard.fires > 0 ? 'warn' : 'dim' },
    ],
  })
  return sections
}

/** The `/live status` answer: every pane row as plain text. */
export const statusText = (s: LiveSnapshot | null, host: HostUsage | null): string => {
  if (s === null) return 'aw-live: no numbers yet (is scorer installed? scripts/install-scorer.sh)'
  const lines = paneSections(s, host).flatMap(section => [
    `${section.title}`,
    ...section.rows.map(row => `  ${row.label.padEnd(14)}${row.value}`),
  ])
  return lines.join('\n')
}
