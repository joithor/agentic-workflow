// The contract with `scorer live --json`. scorer/src/live-snapshot.ts holds the zod
// schema for the same shape; scorer/tests/live-contract.test.ts parses this mod's
// fixture with it, so the two cannot drift apart unnoticed.

import type { LiveSnapshot } from '../../types'

const isObject = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value)

const JUDGE_STATES = ['none', 'unscoped', 'ok']

/** Parses the CLI's stdout; null for anything that is not a version-1 snapshot. */
export const parseSnapshot = (text: string): LiveSnapshot | null => {
  let value: unknown
  try {
    value = JSON.parse(text)
  } catch {
    return null
  }
  if (!isObject(value) || value.v !== 1 || typeof value.sessionId !== 'string') return null
  const { ingest, context, usage, judge, contextGuard, wakes } = value
  if (!isObject(ingest) || !isObject(context) || !isObject(usage) || !isObject(contextGuard) || !isObject(wakes)) return null
  if (!isObject(judge) || typeof judge.state !== 'string' || !JUDGE_STATES.includes(judge.state)) return null
  return value as unknown as LiveSnapshot
}
