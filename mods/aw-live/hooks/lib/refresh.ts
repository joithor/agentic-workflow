// Pure pieces of the refresh loop. register.tsx owns every `$` call.

export const MIN_GAP_MS = 2_000
export const RUN_TIMEOUT_MS = 5_000
/** The first refresh has no snapshot to fall back on; a cold ingest of a long session needs longer. */
export const RUN_TIMEOUT_FIRST_MS = 30_000

export const runTimeout = (hasSnapshot: boolean): number => (hasSnapshot ? RUN_TIMEOUT_MS : RUN_TIMEOUT_FIRST_MS)

/** The `scorer live` command line. `window` is the host's context window, when known. */
export const liveArgs = (sessionId: string, cwd: string, window: number | null): string[] => [
  'live', '--session', sessionId, '--cwd', cwd, ...(window === null ? [] : ['--window', String(window)]), '--json',
]

/** Where to look for an aw CLI: an explicit override, the installer's directory, then PATH. */
export const binCandidates = (home: string | undefined, name: 'scorer' | 'judge', override?: string): string[] => [
  ...(override === undefined || override === '' ? [] : [override]),
  ...(home === undefined || home === '' ? [name] : [`${home}/.local/bin/${name}`, name]),
]

/** A refresh is due when none has run yet or the last one is at least `minGapMs` old. */
export const isDue = (lastAt: number | null, now: number, minGapMs: number = MIN_GAP_MS): boolean =>
  lastAt === null || now - lastAt >= minGapMs
