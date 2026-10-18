import { readFileSync } from "node:fs";

// Reads a file as UTF-8; "-" reads standard input.
export function readText(file) {
  return readFileSync(file === "-" ? 0 : file, "utf8");
}
