import fs from "node:fs";
import path from "node:path";

import { err, ok } from "../../adapters/types.js";
import type { Scrubber } from "../../scrub/scrub.js";
import { clean, type Source } from "../source.js";

export function fileSource(file: string, scrubber?: Scrubber): Source {
  return {
    name: "file",
    async find() {
      let raw: string;
      try {
        raw = fs.readFileSync(file, "utf8");
      } catch {
        return err({ kind: "not-found", code: "SND-SCOPE-020", message: `cannot read ${path.basename(file)}` });
      }
      const text = clean(raw, scrubber);
      const title = /^#\s+(.+)$/m.exec(text)?.[1].trim() ?? path.basename(file);
      return ok([{ ref: `file:${path.basename(file)}`, kind: "brief", title, text, author: null, createdAt: null, trust: "trusted" }]);
    },
  };
}
