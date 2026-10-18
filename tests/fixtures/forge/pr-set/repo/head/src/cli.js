import { parseArgs } from "./args.js";
import { count } from "./count.js";
import { format } from "./format.js";
import { readText } from "./io.js";

export function run(argv, out, err) {
  let options;
  try {
    options = parseArgs(argv);
  } catch (e) {
    err.write(`${e.message}\n`);
    return 2;
  }
  if (options.files.length === 0) {
    err.write("usage: tally [--lines | --words | --chars] <file>...\n");
    return 2;
  }
  const results = options.files.map((file) => ({ file, count: count(readText(file), options.mode) }));
  out.write(`${format(results, options.json)}\n`);
  return 0;
}
