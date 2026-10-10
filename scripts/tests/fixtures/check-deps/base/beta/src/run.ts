import { execFile } from "node:child_process";
export const ask = (id: string) => execFile("alpha", ["why", id], () => {});
export const dbPath = (stateDir: string) => path.join(stateDir, "alpha", "a.sqlite");
// "alpha" in a comment or a plain string is not a launch:
const label = "alpha";
