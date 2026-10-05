#!/usr/bin/env python3
"""Drive a real interactive Claude Code session in a pseudo-terminal and prove the aw-live
pane renders: start `claude --plugin-dir <mod>`, send one tiny prompt (so the session has a
transcript), open /live, capture the screen text, and check the pane's sections are on it.

Evidence, not a test suite: it spends one haiku turn. Run it from a directory Claude Code
already trusts (the main checkout), or the folder-trust dialog eats the first keystrokes.

  scripts/live-pane-proof.py --plugin-dir mods/aw-live --cwd . --out evidence.txt
  env passed through: AW_SCORER_BIN, AW_STATE_DIR, AW_JUDGE_BIN (point them at a build and a scratch dir)

Asserts specific strings, not generic words: the slash-menu entry, a `window NN%` meter line,
and the labels `cost`, `calls`, `over 200k`, `queued now`, `context guard`.
"""
import argparse
import fcntl
import os
import pty
import re
import select
import signal
import struct
import sys
import termios
import time

CURSOR_FORWARD = re.compile(r"\x1b\[(\d*)C")
ANSI = re.compile(r"\x1b\[[0-9;?<>=]*[ -/]*[@-~]|\x1b\][^\x07]*\x07|\x1b[()][A-Z0-9]|\x1b[=>]")
# Literal phrases, compared with all whitespace removed and lowercased.
EXPECT = ["togglethelivescorerpane", "cost", "calls", "over200k", "queuednow", "contextguard"]
# The context meter line: the word `window`, then a percentage, then at least one bar glyph.
WINDOW_METER = re.compile(r"window\s+\d{1,3}%\s*[▓░█▒]", re.IGNORECASE)


def drive(argv, cwd, steps, total, cols=150, rows=50):
    pid, fd = pty.fork()
    if pid == 0:
        os.chdir(cwd)
        env = dict(os.environ, TERM="xterm-256color", COLUMNS=str(cols), LINES=str(rows),
                   CLAUDE_CODE_FORCE_SESSION_PERSISTENCE="1")
        env.pop("CLAUDE_CODE_CHILD_SESSION", None)
        os.execvpe(argv[0], argv, env)
    fcntl.ioctl(fd, termios.TIOCSWINSZ, struct.pack("HHHH", rows, cols, 0, 0))
    out = b""
    start = time.time()
    pending = list(steps)
    while time.time() - start < total:
        ready, _, _ = select.select([fd], [], [], 0.2)
        if ready:
            try:
                chunk = os.read(fd, 65536)
            except OSError:
                break
            if not chunk:
                break
            out += chunk
        while pending and time.time() - start >= pending[0][0]:
            os.write(fd, pending.pop(0)[1])
    try:
        os.kill(pid, signal.SIGKILL)
        os.waitpid(pid, 0)
    except (ProcessLookupError, ChildProcessError):
        pass
    return out.decode("utf8", "replace")


def clean(raw):
    spaced = CURSOR_FORWARD.sub(lambda m: " " * int(m.group(1) or 1), raw)
    return ANSI.sub("", spaced)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--plugin-dir", required=True)
    ap.add_argument("--cwd", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--model", default="haiku")
    ap.add_argument("--prompt", default="reply with the single word ok")
    args = ap.parse_args()
    steps = [
        (9, args.prompt.encode()), (10, b"\r"),   # one turn: the session gets calls in its transcript
        (30, b"/live"), (31, b"\r"),              # open the pane
        (44, b"\x1b"),                            # Esc hands the keys back
    ]
    raw = drive(["claude", "--plugin-dir", args.plugin_dir, "--model", args.model], args.cwd, steps, total=50)
    text = clean(raw)
    with open(args.out, "w") as fh:
        fh.write(text)
    squashed = re.sub(r"\s+", "", text).lower()
    missing = [w for w in EXPECT if w not in squashed]
    meter = WINDOW_METER.search(text) is not None
    print(f"captured {len(text)} chars; missing: {missing or 'none'}; window meter shown: {meter}")
    return 0 if not missing and meter else 1


if __name__ == "__main__":
    sys.exit(main())
