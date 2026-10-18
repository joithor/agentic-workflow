import { readFileSync } from "node:fs";

export function readText(file) {
  return readFileSync(file, "utf8");
}
