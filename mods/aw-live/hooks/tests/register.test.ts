import { expect, mock, test } from 'claude-code/testing'
import type { Engine } from 'claude-code/testing'
import type { On } from 'claude-code'

import { SAMPLE_JSON } from './fixtures/sample'

const SURFACES = ['terminal', 'desktop'] as const
const PLUGIN = 'aw-live'
const CMD = {
  command: 'live', args: '', origin: { kind: 'composer' as const },
  presentation: { isFullscreen: false, columns: 120 },
}

type Run = { argv: string[]; timeoutMs?: number }
type Reply = { exitCode: number; stdout: string }

// The world beneath the plugin: a fixed session, a fake usage, and a scripted process.run.
const sessionId = { current: 'session-1' }

const world = (on: On, reply: (argv: string[]) => Reply | 'cannot-start', env?: Record<string, string>) => {
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
  const clock = mock.clock(on, { now: 1_700_000_000_000 })
  on('process.run', (_$, e) => {
    runs.push({ argv: [...e.argv], timeoutMs: e.init?.timeoutMs })
    const r = reply([...e.argv])
    if (r === 'cannot-start') return { deny: 'ENOENT' }
    return { value: { ...r, stderr: '', isStdoutTruncated: false, isStderrTruncated: false } }
  })
  on('ui.toast', (_$, e) => { toasts.push(e.text); return { value: undefined } })
  on('ui.open', (_$, e) => { opened.push(e.id); isOpen = true; return { value: { isPlaced: true } } })
  on('ui.close', (_$, e) => { closed.push(e.id); isOpen = false; return { value: undefined } })
  on('ui.panes', () => ({ value: isOpen ? [{ id: 'aw-live', title: 'Live scorer', isShown: true, isFocused: false, isPlaced: true }] : [] }))
  on('command.register', (_$, e) => ({ value: { command: e.name } }))
  on('session.start', (_$, e) => ({ cwd: e.cwd }))
  on('turn.complete', (_$, e) => ({ text: e.answer }))
  on('ui.render', { component: 'AbovePrompt' }, () => ({ type: 'engine', ref: 0 }))
  on('ui.render', { component: 'Pane' }, () => ({ type: 'engine', ref: 0 }))
  return { runs, opened, closed, toasts, clock }
}

const ok = (stdout: string): Reply => ({ exitCode: 0, stdout })
const start = ($: Engine) => $.session.start({ cwd: '/repo', surface: 'terminal', isInteractive: true })

const band = ($: Engine, surface: (typeof SURFACES)[number], hasSurvey = false) =>
  $.ui.mount({
    plugin: PLUGIN, surface, component: 'AbovePrompt',
    props: { hasSurvey, isWorking: false, maxRows: 5, bodyColumns: 120, scroll: { offset: 0, bodyRows: 5 }, view: {} },
  })

const pane = ($: Engine, surface: (typeof SURFACES)[number]) =>
  $.ui.mount({
    plugin: PLUGIN, surface, component: 'Pane', requestId: 'aw-live',
    props: { title: 'Live scorer', isFocused: true, bodyColumns: 70, placement: 'inline', scroll: { offset: 0, bodyRows: 30 }, view: {} },
  })

test('session start refreshes once and the band shows the headline on every surface', async ($, on) => {
  const { runs, clock } = world(on, () => ok(SAMPLE_JSON))
  await start($)
  await clock.settle()
  expect(runs[0]?.argv).toEqual([
    '/home/me/.local/bin/scorer', 'live', '--session', 'session-1', '--cwd', '/repo', '--window', '1000000', '--json',
  ])
  for (const surface of SURFACES) {
    const ui = await band($, surface)
    expect((await ui.find({ type: 'Text' }))?.text).toBe('live · ctx 44% · $1.84 · 31 calls · 2 >200k · judge 12 (3 unsure) · gates 12 · 2 queued')
    await ui.unmount()
  }
})

test('the band stays quiet under a survey, before any numbers, and when the CLI fails', async ($, on) => {
  world(on, () => ({ exitCode: 1, stdout: '' }))
  await start($)
  const none = await band($, 'terminal')
  expect(await none.find({ type: 'Text' })).toBeUndefined()
  await none.unmount()
  const survey = await band($, 'terminal', true)
  expect(await survey.find({ type: 'Text' })).toBeUndefined()
  await survey.unmount()
})

test('a missing binary fails silent', async ($, on) => {
  world(on, () => 'cannot-start')
  await start($)
  const ui = await band($, 'terminal')
  expect(await ui.find({ type: 'Text' })).toBeUndefined()
  await ui.unmount()
})

test('garbage output fails silent', async ($, on) => {
  world(on, () => ok('garbage'))
  await start($)
  const ui = await band($, 'terminal')
  expect(await ui.find({ type: 'Text' })).toBeUndefined()
  await ui.unmount()
})

test('the installer path is tried first, then PATH', async ($, on) => {
  const { runs, clock } = world(on, argv => (argv[0] === 'scorer' ? ok(SAMPLE_JSON) : 'cannot-start'))
  await start($)
  await clock.settle()
  expect(runs.map(r => r.argv[0])).toEqual(['/home/me/.local/bin/scorer', 'scorer'])
  const ui = await band($, 'terminal')
  expect(await ui.find({ type: 'Text' })).toBeDefined()
  await ui.unmount()
})

test('turn.complete refreshes again, but not twice inside the minimum gap', async ($, on) => {
  const { runs, clock } = world(on, () => ok(SAMPLE_JSON))
  await start($)
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

test('the timer refreshes every ten seconds', async ($, on) => {
  const { runs, clock } = world(on, () => ok(SAMPLE_JSON))
  await start($)
  await clock.settle()
  const first = runs.length
  await clock.advance(10_000)
  expect(runs.length).toBe(first + 1)
})

test('/live opens the pane, shows the sections, and a second /live closes it', async ($, on) => {
  const { opened, closed } = world(on, () => ok(SAMPLE_JSON))
  await start($)
  await $.command.run(CMD)
  expect(opened).toEqual(['aw-live'])
  for (const surface of SURFACES) {
    const ui = await pane($, surface)
    const text = (await ui.findAll({ type: 'Text' })).map(t => t.text).join('\n')
    expect(text).toContain('done-gate')
    expect(text).toContain('5 · continue 4 · ask 1 · p50 350 ms')
    expect(text).toContain('queued now')
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

test('AW_SCORER_BIN is tried before the installer path', async ($, on) => {
  const { runs, clock } = world(on, () => ok(SAMPLE_JSON), { AW_SCORER_BIN: '/build/scorer' })
  await start($)
  await clock.settle()
  expect(runs[0]?.argv[0]).toBe('/build/scorer')
})

test('/live status answers with plain text and opens no pane', async ($, on) => {
  const { opened } = world(on, () => ok(SAMPLE_JSON))
  await start($)
  const answer = await $.command.run({ ...CMD, args: ' status ' })
  expect(answer.text).toContain('done-gate')
  expect(answer.text?.startsWith('live · ctx 44%')).toBe(true)
  expect(opened).toEqual([])
})

test('/live status says so when scorer is missing', async ($, on) => {
  world(on, () => 'cannot-start')
  await start($)
  const answer = await $.command.run({ ...CMD, args: 'status' })
  expect(answer.text).toContain('scripts/install-scorer.sh')
})

test('a /clear gives the session a new id and the next refresh asks for that one (RF-5)', async ($, on) => {
  const { runs, clock } = world(on, () => ok(SAMPLE_JSON))
  await start($)
  await clock.settle()
  sessionId.current = 'session-2'
  await clock.advance(10_000)
  expect(runs.at(-1)?.argv).toContain('session-2')
  expect(runs[0]?.argv).toContain('session-1')
  sessionId.current = 'session-1'
})

test('a refresh that arrives while another runs joins it: one process, not two (RF-3)', async ($, on) => {
  const { runs, clock } = world(on, () => ok(SAMPLE_JSON))
  await start($)
  await $.command.run({ ...CMD, args: 'status' })
  await clock.settle()
  expect(runs.filter(r => r.argv.includes('live')).length).toBe(1)
})

test('no session id yet means no refresh and no crash', async ($, on) => {
  sessionId.current = ''
  const { runs, clock } = world(on, () => ok(SAMPLE_JSON))
  await start($)
  await clock.settle()
  expect(runs).toEqual([])
  sessionId.current = 'session-1'
})
test('the first refresh gets the long timeout, later ones the short one', async ($, on) => {
  const { runs, clock } = world(on, () => ok(SAMPLE_JSON))
  await start($)
  await clock.settle()
  await clock.advance(10_000)
  expect(runs[0]?.timeoutMs).toBe(30_000)
  expect(runs.at(-1)?.timeoutMs).toBe(5_000)
})

test('a malformed payload (judge ok without gates) never throws and the band stays quiet', async ($, on) => {
  const bad = JSON.stringify({ ...JSON.parse(SAMPLE_JSON), judge: { state: 'ok' } })
  world(on, () => ok(bad))
  await start($)
  const ui = await band($, 'terminal')
  expect(await ui.find({ type: 'Text' })).toBeUndefined()
  await ui.unmount()
})
