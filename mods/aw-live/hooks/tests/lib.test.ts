import { expect, test } from 'claude-code/testing'

import type { LiveSnapshot } from '../../types'
import { MIN_GAP_MS, RUN_TIMEOUT_FIRST_MS, RUN_TIMEOUT_MS, binCandidates, isDue, liveArgs, runTimeout } from '../lib/refresh'
import { parseSnapshot } from '../lib/snapshot'
import { bandText, bar, contextPercent, cut, fmtTokens, paneSections, providerShare, statusText } from '../lib/format'
import { SAMPLE_JSON } from './fixtures/sample'

const sample = (): LiveSnapshot => JSON.parse(SAMPLE_JSON) as LiveSnapshot
const HOST = { usd: 1.839, percent: 44, window: 1_000_000 }

test('parseSnapshot accepts the sample and rejects everything else', () => {
  expect(parseSnapshot(SAMPLE_JSON)?.sessionId).toBe('session-1')
  expect(parseSnapshot('not json')).toBeNull()
  expect(parseSnapshot('[]')).toBeNull()
  expect(parseSnapshot('null')).toBeNull()
  expect(parseSnapshot(JSON.stringify({ ...sample(), v: 2 }))).toBeNull()
  expect(parseSnapshot(JSON.stringify({ ...sample(), sessionId: 3 }))).toBeNull()
  expect(parseSnapshot(JSON.stringify({ ...sample(), context: null }))).toBeNull()
  expect(parseSnapshot(JSON.stringify({ ...sample(), ingest: null }))).toBeNull()
  expect(parseSnapshot(SAMPLE_JSON)?.ingest.readErrors).toBe(0)
  expect(parseSnapshot(JSON.stringify({ ...sample(), judge: { state: 'weird' } }))).toBeNull()
  expect(parseSnapshot(JSON.stringify({ ...sample(), judge: 'ok' }))).toBeNull()
  expect(parseSnapshot(JSON.stringify({ ...sample(), judge: { state: 'none' } }))?.judge.state).toBe('none')
})

test('number formatting', () => {
  expect([fmtTokens(950), fmtTokens(84_000), fmtTokens(1_900_000)]).toEqual(['950', '84k', '1.9M'])
  expect(bar(42, 10)).toBe('▓▓▓▓░░░░░░')
  expect(bar(null, 4)).toBe('░░░░')
  expect(bar(250, 4)).toBe('▓▓▓▓')
  expect(cut('abcdef', 4)).toBe('abc…')
  expect(cut('abc', 4)).toBe('abc')
  expect(providerShare({ jev: 10, rules: 2, 'claude-cli': 1 })).toBe('jev 77% · rules 15%')
  expect(providerShare({})).toBe('')
})

test('the host percent wins over the transcript estimate', () => {
  expect(contextPercent(sample(), HOST)).toBe(44)
  expect(contextPercent(sample(), null)).toBe(42)
  expect(contextPercent(sample(), { usd: null, percent: null, window: null })).toBe(42)
})

test('band text: headline numbers, quiet parts omitted', () => {
  expect(bandText(sample(), HOST, 200)).toBe('live · ctx 44% · $1.84 · 31 calls · 2 >200k · judge 12 (3 unsure) · gates 12 · 2 queued')
  const quiet: LiveSnapshot = { ...sample(), usage: { ...sample().usage, callsOver200k: 0 }, judge: { state: 'none' }, wakes: { queuedNow: 0 } }
  expect(bandText(quiet, null, 200)).toBe('live · ctx 42% · 31 calls')
  expect(bandText(sample(), HOST, 20)).toHaveLength(20)
})

test('band text: judge without unsure answers; nothing to say without a transcript or host', () => {
  const s = sample()
  if (s.judge.state !== 'ok') throw new Error('fixture')
  const calm: LiveSnapshot = { ...s, judge: { ...s.judge, undecided: 0 } }
  expect(bandText(calm, null, 200)).toContain('judge 12 ·')
  const none: LiveSnapshot = { ...s, transcript: { found: false, files: 0 }, context: { tokens: null, window: 200000, percent: null } }
  expect(bandText(none, null, 200)).toBeNull()
  expect(bandText(none, HOST, 200)).toContain('ctx 44%')
  const noPercent: LiveSnapshot = { ...none, usage: { ...none.usage, calls: 0, callsOver200k: 0 }, judge: { state: 'none' }, wakes: { queuedNow: 0 } }
  expect(bandText(noPercent, { usd: null, percent: null, window: null }, 200)).toBe('live · 0 calls')
})

test('pane sections for an ok judge, an unscoped judge and no judge', () => {
  const titles = (s: LiveSnapshot) => paneSections(s, HOST).map(x => x.title)
  expect(titles(sample())).toEqual(['context', 'session', 'judge', 'gates', 'wakes'])
  expect(titles({ ...sample(), judge: { state: 'unscoped' } })).toEqual(['context', 'session', 'judge', 'wakes'])
  expect(titles({ ...sample(), judge: { state: 'none' } })).toEqual(['context', 'session', 'wakes'])
  const gates = paneSections(sample(), HOST).find(x => x.title === 'gates')
  expect(gates?.rows[1]).toEqual({ label: 'done-gate', value: '5 · continue 4 · ask 1 · p50 350 ms', tone: 'ok' })
  const ctx = paneSections(sample(), { usd: null, percent: 91, window: null })[0]
  expect(ctx?.rows[0]?.tone).toBe('warn')
  expect(ctx?.rows[1]?.value).toBe('84k of 200k')
})

test('pane sections: unknown values read as dashes; quiet gates say so', () => {
  const s = sample()
  if (s.judge.state !== 'ok') throw new Error('fixture')
  const empty: LiveSnapshot = {
    ...s,
    context: { tokens: null, window: 200000, percent: null },
    usage: { ...s.usage, callsOver200k: 0 },
    wakes: { queuedNow: 0 },
    contextGuard: { fires: 0 },
    judge: { ...s.judge, byProvider: {}, undecided: 0, gates: {
      'scope-gate': { question: 'brief-scope', fired: 0, byDecision: {}, p50LatencyMs: 0 },
      'done-gate': { question: 'ask-check', fired: 0, byDecision: {}, p50LatencyMs: 0 },
      'send-gate': { question: 'wake-gate', fired: 0, byDecision: {}, p50LatencyMs: 0 },
    } },
  }
  const sections = paneSections(empty, null)
  expect(sections[0]?.rows[0]?.value).toBe('-- ░░░░░░░░░░')
  expect(sections[0]?.rows[1]?.value).toBe('-- of 200k')
  expect(sections[1]?.rows[0]?.value).toBe('--')
  expect(sections[2]?.rows[0]?.value).toBe('12')
  expect(sections[3]?.rows.map(r => r.value)).toEqual(['quiet', 'quiet', 'quiet'])
})

test('refresh helpers', () => {
  expect(liveArgs('s1', '/repo', 1_000_000)).toEqual(['live', '--session', 's1', '--cwd', '/repo', '--window', '1000000', '--json'])
  expect(liveArgs('s1', '/repo', null)).toEqual(['live', '--session', 's1', '--cwd', '/repo', '--json'])
  expect(binCandidates('/home/me', 'scorer')).toEqual(['/home/me/.local/bin/scorer', 'scorer'])
  expect(binCandidates(undefined, 'judge')).toEqual(['judge'])
  expect(binCandidates('', 'judge')).toEqual(['judge'])
  expect(binCandidates('/home/me', 'judge', '/x/judge')).toEqual(['/x/judge', '/home/me/.local/bin/judge', 'judge'])
  expect(binCandidates('/home/me', 'judge', '')).toEqual(['/home/me/.local/bin/judge', 'judge'])
  expect(runTimeout(true)).toBe(RUN_TIMEOUT_MS)
  expect(runTimeout(false)).toBe(RUN_TIMEOUT_FIRST_MS)
  expect(RUN_TIMEOUT_FIRST_MS).toBe(30_000)
  expect(isDue(null, 5)).toBe(true)
  expect(isDue(1000, 1000 + MIN_GAP_MS - 1)).toBe(false)
  expect(isDue(1000, 1000 + MIN_GAP_MS)).toBe(true)
})

test('status text: band line, then each section; a hint when there are no numbers', () => {
  const text = statusText(sample(), HOST)
  expect(text.split('\n')[0]).toBe('live · ctx 44% · $1.84 · 31 calls · 2 >200k · judge 12 (3 unsure) · gates 12 · 2 queued')
  expect(text).toContain('\ncontext\n  window        44% ▓▓▓▓░░░░░░')
  expect(text).toContain('  done-gate     5 · continue 4 · ask 1 · p50 350 ms')
  expect(statusText(null, null)).toContain('scripts/install-scorer.sh')
  const bare: LiveSnapshot = { ...sample(), transcript: { found: false, files: 0 }, context: { tokens: null, window: 200000, percent: null } }
  expect(statusText(bare, null).split('\n')[0]).toBe('live')
})

test('read errors show a stale warning in the band and the pane', () => {
  const stale: LiveSnapshot = { ...sample(), ingest: { newLines: 0, ms: 5, readErrors: 2 } }
  expect(bandText(stale, HOST, 200)).toMatch(/ · stale$/)
  expect(bandText(sample(), HOST, 200)).not.toContain('stale')
  const warning = paneSections(stale, HOST)[1]?.rows.find(r => r.label === 'warning')
  expect(warning).toEqual({ label: 'warning', value: 'stale: 2 transcript read errors', tone: 'warn' })
  const one: LiveSnapshot = { ...sample(), ingest: { newLines: 0, ms: 5, readErrors: 1 } }
  expect(paneSections(one, HOST)[1]?.rows.at(-1)?.value).toBe('stale: 1 transcript read error')
  expect(paneSections(sample(), HOST)[1]?.rows.some(r => r.label === 'warning')).toBe(false)
})
