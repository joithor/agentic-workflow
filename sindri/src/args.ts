import { parseArgs, type ParseArgsConfig } from "node:util";

import { SindriError } from "./errors.js";

type Options = NonNullable<ParseArgsConfig["options"]>;

export function parseFlags<O extends Options>(args: string[], options: O) {
  try {
    return parseArgs({ args, options, allowPositionals: true, strict: true });
  } catch (e) {
    throw new SindriError("SND-CLI-002", (e as Error).message);
  }
}
