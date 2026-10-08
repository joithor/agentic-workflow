export type GitResult = { ok: true; stdout: string } | { ok: false; stderr: string };

export interface GitRunner {
  run(args: string[], cwd: string): Promise<GitResult>;
}
