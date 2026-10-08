// `redirect: "error"`: the embedding endpoint is loopback; a redirect would send code elsewhere.
export type FetchLike = (
  url: string,
  init: { method: "POST"; headers: Record<string, string>; body: string; signal: AbortSignal; redirect: "error" },
) => Promise<{ ok: boolean; status: number; json(): Promise<unknown> }>;

export interface ProcessRunner {
  // cleanEnv: run with only PATH, HOME, LANG and TMPDIR (graphify never sees tokens).
  run(argv: string[], o: { cwd: string; timeoutMs: number; cleanEnv?: boolean }): Promise<{ code: number; stdout: string; stderr: string }>;
}

// What `index setup` and `doctor` need from the machine. Real: sandbox-real.ts.
export interface IndexProbes {
  has(bin: string): boolean;
  run: ProcessRunner["run"];
  getJson(url: string, timeoutMs: number): Promise<unknown | null>;
}

export interface IndexIo {
  fetch: FetchLike;
  probes: IndexProbes;
}
