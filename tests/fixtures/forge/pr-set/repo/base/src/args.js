// Parses the command line: tally [--lines | --words | --chars] <file>...
export function parseArgs(argv) {
  const options = { mode: "words", files: [] };
  for (const arg of argv) {
    if (arg === "--lines") options.mode = "lines";
    else if (arg === "--words") options.mode = "words";
    else if (arg === "--chars") options.mode = "chars";
    else if (arg.startsWith("-")) throw new Error(`unknown option ${arg}`);
    else options.files.push(arg);
  }
  return options;
}
