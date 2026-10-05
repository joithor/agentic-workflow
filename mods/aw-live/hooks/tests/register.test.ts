import { expect, mock, test } from 'claude-code/testing'
import type { Engine } from 'claude-code/testing'
import type { On } from 'claude-code'

import { resetForTests } from '../register'
import { SAMPLE_JSON } from './fixtures/sample'

const SURFACES = ['terminal', 'desktop'] as const
const PLUGIN = 'aw-live'
const CMD = {
  command: 'live', args: '', origin: { kind: 'composer' as const },
  presentation: { isFullscreen: false, columns: 120 },
}

type Run = { argv: string[]; timeoutMs?: number }
type Reply = { exitCode: number; stdout: string }

// A host whose clock rejects: now() is refused, timers never fire.
const brokenClock = (on: On): ReturnType<typeof mock.clock> => {
  on('clock.now', () => ({ deny: 'clock down' }))
  on('clock.after', () => ({ value: undefined }))
  on('clock.every', () => ({ value: undefined }))
  return { settle: async () => undefined, advance: async () => undefined } as unknown as ReturnType<typeof mock.clock>
}

// The world beneath the plugin: a fixed session, a fake usage, and a scripted process.run.
const sessionId = { current: 'session-1' }

const world = (on: On, reply: (argv: string[]) => Reply | 'cannot-start' | 'timeout', env?: Record<string, string>, broken: string[] = []) => {
  resetForTests()
  sessionId.current = 'session-1'
  const runs: Run[] = []
  const opened: string[] = []
  const closed: string[] = []
  const toasts: string[] = []
  let isOpen = false
  on('session.id', () => ({ value: sessionId.current }))
  on('session.cwd', () => ({ value: '/repo' }))
  on('session.usage', () => ({
    value: { startedAt: 0, context: { window: 1_000_000, percent: 44, tokens: 440_000 }, rateLimits: [], cost: { usd: 1.839 } },
  }))
  mock.env(on, { HOME: '/home/me', ...(env ?? {}) })
  const clock = broken.includes('clock.now') ? brokenClock(on) : mock.clock(on, { now: 1_700_000_000_000 })
  on('process.run', (_$, e) => {
    runs.push({ argv: [...e.argv], timeoutMs: e.init?.timeoutMs })
    const r = reply([...e.argv])
    if (r === 'timeout') return { deny: 'timed out after 30000ms' }
    if (r === 'cannot-start') return { deny: 'ENOENT' }
    return { value: { ...r, stderr: '', isStdoutTruncated: false, isStderrTruncated: false } }
  })
  on('ui.toast', (_$, e) => { toasts.push(e.text); return { value: undefined } })
  on('ui.open', (_$, e) => { opened.push(e.id); isOpen = true; return { value: { isPlaced: true } } })
  on('ui.close', (_$, e) => { closed.push(e.id); isOpen = false; return { value: undefined } })
  if (broken.includes('ui.panes')) on('ui.panes', () => ({ deny: 'ui down' }))
  else on('ui.panes', () => ({ value: isOpen ? [{ id: 'aw-live', title: 'Live scorer', isShown: true, isFocused: false, isPlaced: true }] : [] }))
  if (broken.includes('command.register')) on('command.register', () => ({ deny: 'register down' }))
  else on('command.register', (_$, e) => ({ value: { command: e.name } }))
  on('session.start', (_$, e) => ({ cwd: e.cwd }))
  on('turn.complete', (_$, e) => ({ text: e.answer }))
  on('ui.render', { component: 'Pane' }, () => ({ type: 'engine', ref: 0 }))
  return { runs, opened, closed, toasts, clock }
}

const ok = (stdout: string): Reply => ({ exitCode: 0, stdout })
const start = ($: Engine) => $.session.start({ cwd: '/repo', surface: 'terminal', isInteractive: true })

const pane = ($: Engine, surface: (typeof SURFACES)[number]) =>
  $.ui.mount({
    plugin: PLUGIN, surface, component: 'Pane', requestId: 'aw-live',
    props: { title: 'Live scorer', isFocused: true, bodyColumns: 70, placement: 'inline', scroll: { offset: 0, bodyRows: 30 }, view: {} },
  })

const text = async (ui: Awaited<ReturnType<typeof pane>>) => (await ui.findAll({ type: 'Text' })).map(t => t.text).join('\n')

test('session start only registers /live: no process runs until the pane or /live status asks', async ($, on) => {
  const { runs, clock } = world(on, () => ok(SAMPLE_JSON))
  await start($)
  await clock.advance(60_000)
  expect(runs).toEqual([])
})

test('the scorer is asked for this session, cwd and window', async ($, on) => {
  const { runs } = world(on, () => ok(SAMPLE_JSON))
  await start($)
  await $.command.run({ ...CMD, args: 'status' })
  expect(runs[0]?.argv).toEqual([
    '/home/me/.local/bin/scorer', 'live', '--session', 'session-1', '--cwd', '/repo', '--window', '1000000', '--json',
  ])
})

test('the installer path is tried first, then PATH', async ($, on) => {
  const { runs } = world(on, argv => (argv[0] === 'scorer' ? ok(SAMPLE_JSON) : 'cannot-start'))
  await start($)
  const answer = await $.command.run({ ...CMD, args: 'status' })
  expect(runs.map(r => r.argv[0])).toEqual(['/home/me/.local/bin/scorer', 'scorer'])
  expect(answer.text).toContain('done-gate')
})

test('AW_SCORER_BIN is tried before the installer path', async ($, on) => {
  const { runs } = world(on, () => ok(SAMPLE_JSON), { AW_SCORER_BIN: '/build/scorer' })
  await start($)
  await $.command.run({ ...CMD, args: 'status' })
  expect(runs[0]?.argv[0]).toBe('/build/scorer')
})

test('turn.complete refreshes an open pane, but not twice inside the minimum gap', async ($, on) => {
  const { runs, clock } = world(on, () => ok(SAMPLE_JSON))
  await start($)
  await $.command.run(CMD)
  await clock.settle()
  const first = runs.length
  await clock.advance(1_000)
  await $.turn.complete({ answer: 'x', durationMs: 1, isAborted: false, turnId: 't1', reason: 'answer' })
  await clock.settle()
  expect(runs.length).toBe(first)
  await clock.advance(1_500)
  await $.turn.complete({ answer: 'x', durationMs: 1, isAborted: false, turnId: 't2', reason: 'answer' })
  await clock.settle()
  expect(runs.length).toBe(first + 1)
})

test('turn.complete costs nothing while the pane is closed', async ($, on) => {
  const { runs, clock } = world(on, () => ok(SAMPLE_JSON))
  await start($)
  await $.turn.complete({ answer: 'x', durationMs: 1, isAborted: false, turnId: 't1', reason: 'answer' })
  await clock.settle()
  expect(runs).toEqual([])
})

test('/live opens the pane, shows the sections, and a second /live closes it', async ($, on) => {
  const { opened, closed } = world(on, () => ok(SAMPLE_JSON))
  await start($)
  await $.command.run(CMD)
  expect(opened).toEqual(['aw-live'])
  for (const surface of SURFACES) {
    const ui = await pane($, surface)
    const t = await text(ui)
    expect(t).toContain('done-gate')
    expect(t).toContain('5 · continue 4 · ask 1 · p50 350 ms')
    expect(t).toContain('queued now')
    await ui.unmount()
  }
  await $.command.run(CMD)
  expect(closed).toEqual(['aw-live'])
})

test('the pane explains itself when nothing has been read yet', async ($, on) => {
  world(on, () => 'cannot-start')
  await start($)
  const ui = await pane($, 'terminal')
  expect((await ui.find({ type: 'Text' }))?.text).toContain('scripts/install-scorer.sh')
  await ui.unmount()
})

test('garbage output fails silent', async ($, on) => {
  world(on, () => ok('garbage'))
  await start($)
  expect((await $.command.run({ ...CMD, args: 'status' })).text).toContain('scripts/install-scorer.sh')
})

test('/live status answers with plain text and opens no pane', async ($, on) => {
  const { opened } = world(on, () => ok(SAMPLE_JSON))
  await start($)
  const answer = await $.command.run({ ...CMD, args: ' status ' })
  expect(answer.text).toContain('done-gate')
  expect(answer.text?.startsWith('context\n')).toBe(true)
  expect(opened).toEqual([])
})

test('/live status says so when scorer is missing', async ($, on) => {
  world(on, () => 'cannot-start')
  await start($)
  const answer = await $.command.run({ ...CMD, args: 'status' })
  expect(answer.text).toContain('scripts/install-scorer.sh')
})

test('a /clear gives the session a new id and the next refresh asks for that one (RF-5)', async ($, on) => {
  const { runs } = world(on, () => ok(SAMPLE_JSON))
  await start($)
  await $.command.run({ ...CMD, args: 'status' })
  sessionId.current = 'session-2'
  await $.command.run({ ...CMD, args: 'status' })
  expect(runs.at(-1)?.argv).toContain('session-2')
  expect(runs[0]?.argv).toContain('session-1')
})

test('a refresh that arrives while another runs joins it: one process, not two (RF-3)', async ($, on) => {
  const { runs, clock } = world(on, () => ok(SAMPLE_JSON))
  await start($)
  await Promise.all([$.command.run({ ...CMD, args: 'status' }), $.command.run({ ...CMD, args: 'status' })])
  await clock.settle()
  expect(runs.filter(r => r.argv.includes('live')).length).toBe(1)
})

test('no session id yet means no refresh and no crash', async ($, on) => {
  const { runs } = world(on, () => ok(SAMPLE_JSON))
  sessionId.current = ''
  await start($)
  await $.command.run({ ...CMD, args: 'status' })
  expect(runs).toEqual([])
})

test('the first refresh gets the long timeout, later ones the short one', async ($, on) => {
  const { runs } = world(on, () => ok(SAMPLE_JSON))
  await start($)
  await $.command.run({ ...CMD, args: 'status' })
  await $.command.run({ ...CMD, args: 'status' })
  expect(runs[0]?.timeoutMs).toBe(30_000)
  expect(runs.at(-1)?.timeoutMs).toBe(5_000)
})

test('a failed or garbage refresh keeps the last good snapshot (the pane still shows it)', async ($, on) => {
  let calls = 0
  world(on, () => {
    calls += 1
    if (calls === 1) return ok(SAMPLE_JSON)
    return calls === 2 ? ok('garbage') : { exitCode: 1, stdout: '' }
  })
  await start($)
  await $.command.run({ ...CMD, args: 'status' })
  await $.command.run({ ...CMD, args: 'status' })
  const answer = await $.command.run({ ...CMD, args: 'status' })
  expect(calls).toBe(3)
  expect(answer.text).toContain('done-gate')
})

// Passes parseSnapshot (it checks only the judge state) but makes formatting throw.
const NO_GATES = JSON.stringify({ ...JSON.parse(SAMPLE_JSON), judge: { state: 'ok' } })

test('a payload that parses but breaks formatting never throws out of a hook', async ($, on) => {
  world(on, () => ok(NO_GATES))
  await start($)
  const answer = await $.command.run({ ...CMD, args: 'status' })
  expect(answer.text).toContain('scripts/install-scorer.sh')
  const p = await pane($, 'terminal')
  expect(await p.find({ type: 'Text' })).toBeUndefined()
  await p.unmount()
})

test('a clock that rejects never escapes a refresh or /live status', async ($, on) => {
  world(on, () => ok(SAMPLE_JSON), undefined, ['clock.now'])
  await start($)
  const answer = await $.command.run({ ...CMD, args: 'status' })
  expect(answer.text).toContain('scripts/install-scorer.sh')
})

test('host calls that throw inside the hooks fall through instead of throwing', async ($, on) => {
  world(on, () => ok(SAMPLE_JSON), undefined, ['ui.panes', 'command.register'])
  await start($)
  expect(await $.command.run(CMD)).toEqual({})
  await $.turn.complete({ answer: 'x', durationMs: 1, isAborted: false, turnId: 't1', reason: 'answer' })
})

test('a timed-out candidate is not followed by another wait', async ($, on) => {
  const { runs } = world(on, () => 'timeout')
  await start($)
  await $.command.run({ ...CMD, args: 'status' })
  expect(runs.length).toBe(1)
})
