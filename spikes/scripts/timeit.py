#!/usr/bin/env python3
"""Wall-time harness for the spike's numbers.

Reads a commands file (one `name<TAB>command` per line; `{tmp}` becomes a fresh temp path per run), runs every
command WARMUP times, then ROUNDS rounds in round-robin order (each round shuffled with a fixed seed), so thermal
state and page cache affect every command alike. Each run is timed with time.perf_counter_ns() around
subprocess.run (no helper process inside the interval). `/usr/bin/true` is always measured the same way, as the
process-spawn floor; the table shows each median raw and minus that floor. Load average is recorded.

Usage: timeit.py <commands.tsv> [--rounds N] [--warmup N]"""
import os, random, shlex, statistics, subprocess, sys, tempfile, time

args = sys.argv[1:]
path = args[0]
rounds = int(args[args.index("--rounds") + 1]) if "--rounds" in args else 15
warmup = int(args[args.index("--warmup") + 1]) if "--warmup" in args else 1
cmds = [("/usr/bin/true", ["/usr/bin/true"])]
for line in open(path):
    line = line.rstrip("\n")
    if line and not line.startswith("#"):
        name, cmd = line.split("\t", 1)
        cmds.append((name, shlex.split(cmd)))
tmp = tempfile.mkdtemp()
counter = 0

def run(argv):
    global counter
    counter += 1
    argv = [a.replace("{tmp}", os.path.join(tmp, f"r{counter}")) for a in argv]
    t0 = time.perf_counter_ns()
    rc = subprocess.run(argv, stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode
    return (time.perf_counter_ns() - t0) / 1e6, rc

for _ in range(warmup):
    for _, argv in cmds:
        run(argv)
samples = {name: [] for name, _ in cmds}
codes = {name: set() for name, _ in cmds}
rng = random.Random(42)
for _ in range(rounds):
    order = cmds[:]
    rng.shuffle(order)
    for name, argv in order:
        ms, rc = run(argv)
        samples[name].append(ms)
        codes[name].add(rc)
floor = statistics.median(samples["/usr/bin/true"])
print(f"1-min load average at end: {os.getloadavg()[0]:.2f}; rounds {rounds}, warmup {warmup}, round-robin shuffled")
print(f"{'command':34} {'min':>8} {'median':>8} {'max':>8} {'med-floor':>10}  exit")
for name, _ in cmds:
    s = samples[name]
    print(f"{name:34} {min(s):8.1f} {statistics.median(s):8.1f} {max(s):8.1f} {statistics.median(s) - floor:10.1f}  {sorted(codes[name])}")
