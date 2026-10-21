# Using tally

    tally [--lines | --words | --chars] <file>...

| Option | Meaning |
|---|---|
| `--lines` | Count lines |
| `--words` | Count words (the default) |
| `--chars` | Count characters |

Options are parsed by `parseArgs` in `src/args.js`.

Exit status: 0 on success, 2 on a usage error.
