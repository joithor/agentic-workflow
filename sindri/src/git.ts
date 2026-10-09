// code: git's exit status, when it ran and exited (`git grep` exits 1 for "no match").
export type GitResult = { ok: true; stdout: string } | { ok: false; stderr: string; code?: number };

export interface GitRunner {
  // foreign: the call is about another repo, so git's repository variables (GIT_DIR,
  // GIT_INDEX_FILE, …, which git exports to hooks) are cleared for it (githooks(5)).
  // env: extra variables for this call only.
  run(args: string[], cwd: string, o?: { foreign?: boolean; env?: Record<string, string> }): Promise<GitResult>;
}
