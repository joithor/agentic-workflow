// Parses the command line: tally [--lines | --words | --chars] [--json] <file>...
export function parseArgs(argv) {
  const options = { mode: "words", json: false, files: [] };
  for (const arg of argv) {
    if (arg === "--lines") options.mode = "lines";
    else if (arg === "--words") options.mode = "words";
    else if (arg === "--chars") options.mode = "chars";
    else if (arg === "--json") options.json = true;
    else if (arg.startsWith("-")) throw new Error(`unknown option ${arg}`);
    else options.files.push(arg);
  }
  return options;
}
