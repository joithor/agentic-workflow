// aw-live: the scorer's numbers for THIS session, live inside Claude Code.
// `/live` toggles a pane; a one-line band above the prompt shows the headline.
// Every number comes from `scorer live --json` (one short Node process, never
// in-process). A refresh never blocks a hook and every failure is silent: the
// band just keeps its last good value.

import { atom, read, update } from 'claude-code'
import type { EngineInterface, Register } from 'claude-code'

import type { HostUsage, LiveSnapshot, LiveStatus } from '../types'
import { bandText, paneSections, statusText } from './lib/format'
import type { Tone } from './lib/format'
import { TIMER_MS, binCandidates, isDue, liveArgs, runTimeout } from './lib/refresh'
import { parseSnapshot } from './lib/snapshot'

const PANE = 'aw-live'
const TITLE = 'Live scorer'
const PANE_COLUMNS = 72

const snapshot = atom({ plugin: 'aw-live', key: 'snapshot' } as const, null as LiveSnapshot | null)
const host = atom({ plugin: 'aw-live', key: 'host' } as const, null as HostUsage | null)
const status = atom({ plugin: 'aw-live', key: 'status' } as const, null as LiveStatus | null)

const TONE_COLOR: Record<Tone, string | undefined> = { ok: '#7ec699', warn: '#d4a054', dim: '#8b949e' }

// Module variables restart on a hot reload; that only costs one extra refresh.
let lastAt: number | null = null
let running: Promise<void> | null = null

/** Test seam: forget the refresh bookkeeping so each test starts cold. */
export const resetForTests = (): void => {
  lastAt = null
  running = null
}

const isTimeout = (err: unknown): boolean => /time/i.test(err instanceof Error ? err.message : String(err))

/** Runs scorer by argv, trying each candidate path; null on any failure. */
const runScorer = async ($: EngineInterface, args: string[], timeoutMs: number): Promise<string | null> => {
  const names = binCandidates(await $.env.get('HOME'), 'scorer', await $.env.get('AW_SCORER_BIN'))
  for (const name of names) {
    try {
      const r = await $.process.run([name, ...args], { timeoutMs })
      if (r.exitCode === 0) return r.stdout
      return null
    } catch (err) {
      // A timeout means the binary exists but is slow: do not stack another wait on it.
      if (isTimeout(err)) return null
      // otherwise not runnable at this path: try the next candidate
    }
  }
  return null
}

const runRefresh = async ($: EngineInterface, force: boolean): Promise<void> => {
  let now: number | null = null
  let ok = false
  try {
    now = await $.clock.now()
    if (!force && !isDue(lastAt, now)) return
    lastAt = now
    const sessionId = await $.session.id()
    if (sessionId === '') return
    const [cwd, usage] = await Promise.all([$.session.cwd(), $.session.usage()])
    await update($, host, (): HostUsage => ({
      usd: usage.cost?.usd ?? null,
      percent: usage.context.percent ?? null,
      window: usage.context.window,
    }))
    const hasSnapshot = (await read($, snapshot)) !== null
    const out = await runScorer($, liveArgs(sessionId, cwd, usage.context.window), runTimeout(hasSnapshot))
    const parsed = out === null ? null : parseSnapshot(out)
    if (parsed !== null) await update($, snapshot, () => parsed)
    ok = parsed !== null
  } catch {
    // fail silent: the band keeps its last good value
  } finally {
    const at = now
    if (at !== null) await update($, status, (): LiveStatus => ({ ok, at })).catch(() => undefined)
  }
}

/** One refresh at a time: a caller that arrives while one runs waits for that one. */
const refresh = ($: EngineInterface, force: boolean): Promise<void> => {
  if (running !== null) return running
  running = runRefresh($, force).finally(() => {
    running = null
  })
  return running
}

export const register: Register = on => {
  on('session.start', async ($, e, next) => {
    try {
      await $.command.register({ name: 'live', description: 'Toggle the live scorer pane (context, cost, judge, gates)' })
      $.clock.every(TIMER_MS, () => {
        void refresh($, false)
      })
      void refresh($, true)
    } catch {
      // a host error must not stop session start
    }
    return next(e)
  })

  on('turn.complete', async ($, e, next) => {
    try {
      $.clock.after(0, () => {
        void refresh($, false)
      })
    } catch {
      // a host error must not stop the turn
    }
    return next(e)
  })

  on('command.run', { command: 'live' }, async ($, e) => {
    if (e.args.trim() === 'status') {
      await refresh($, true)
      try {
        return { text: statusText(await read($, snapshot), await read($, host)) }
      } catch {
        return { text: 'aw-live: no numbers yet (is scorer installed? scripts/install-scorer.sh)' }
      }
    }
    try {
      if ((await $.ui.panes()).some(pane => pane.id === PANE)) {
        await $.ui.close({ id: PANE })
        return {}
      }
      await refresh($, true)
      await $.ui.open({ id: PANE, title: TITLE, focus: true, closeOnEscape: true, columns: PANE_COLUMNS })
    } catch {
      // a host error: leave the pane as it is
    }
    return {}
  })

  on('ui.render', { component: 'AbovePrompt' }, async ($, e, next) => {
    try {
      if (e.props.hasSurvey) return next(e)
      const s = await read($, snapshot)
      if (s === null) return next(e)
      const text = bandText(s, await read($, host), e.props.bodyColumns)
      if (text === null) return next(e)
      const { Box, Text } = $.ui.resolve(e)
      return (
        <Box>
          <Text color={TONE_COLOR.dim} wrap="truncate-end">{text}</Text>
        </Box>
      )
    } catch {
      return next(e)
    }
  })

  on('ui.render', { component: 'Pane', requestId: PANE }, async ($, e, next) => {
    try {
      const { Box, Text, Button } = $.ui.resolve(e)
      const s = await read($, snapshot)
      if (s === null) {
        return (
          <Box flexDirection="column">
            <Text color={TONE_COLOR.dim}>No numbers yet. Is `scorer` installed? (scripts/install-scorer.sh)</Text>
          </Box>
        )
      }
      const sections = paneSections(s, await read($, host))
      return (
        <Box flexDirection="column">
          {sections.map(section => (
            <Box key={section.title} borderStyle="round" borderColor="#3d4450" paddingX={1} flexDirection="column">
              <Text color="#7eb8da">{section.title}</Text>
              {section.rows.map(row => (
                <Text key={row.label} wrap="truncate-end">
                  <Text color={TONE_COLOR.dim}>{row.label.padEnd(14)}</Text>
                  <Text color={TONE_COLOR[row.tone]}>{row.value}</Text>
                </Text>
              ))}
            </Box>
          ))}
          <Box flexDirection="row" columnGap={2} paddingX={1}>
            <Button key="refresh" label="refresh" hotkey="r" plain onPress={() => refresh($, true)} />
          </Box>
        </Box>
      )
    } catch {
      return next(e)
    }
  })
}
