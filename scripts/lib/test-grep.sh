# Sourced by bash test suites; defines functions only and sets no shell options.
#
# has [grep flags] PATTERN [FILE...]: grep that reports a match through its exit status, like `grep -q`,
# but reads all of its input.
#
# Why not `producer | grep -q ...`: grep -q exits on the first match, the producer then gets SIGPIPE
# ("write error: Broken pipe", status 141), and under `set -o pipefail` the pipeline fails although the
# output was right. It flakes with the timing. Redirecting grep's output to /dev/null instead of using -q
# keeps it reading to the end, so the producer never sees a closed pipe. Pass the same flags as grep minus q.
has() { grep "$@" >/dev/null; }
