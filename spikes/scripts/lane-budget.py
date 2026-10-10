#!/usr/bin/env python3
"""Lane time budget check (spec §4b.5 "CI fails a test that runs over its lane's time"), the approach proposed
for Aw.Kernel.Testing: run one lane with xUnit v3's CTRF reporter, then compare the report with the budget.
Usage: lane-budget.py <ctrf.json> <lane> <lane-budget-seconds> [per-test-budget-seconds]
Fails (exit 1) when the lane's total run time or any single test exceeds its budget."""
import json, sys
report = json.load(open(sys.argv[1]))["results"]
lane, budget = sys.argv[2], float(sys.argv[3])
per_test = float(sys.argv[4]) if len(sys.argv) > 4 else budget
s = report["summary"]
total = (s["stop"] - s["start"]) / 1000.0
tests = report["tests"]
slow = sorted(tests, key=lambda t: t["duration"], reverse=True)
over = [t for t in tests if t["duration"] / 1000.0 > per_test]
print(f"lane {lane}: {len(tests)} tests, run {total:.3f} s (budget {budget} s), slowest {slow[0]['name']} {slow[0]['duration']} ms")
for t in over[:5]:
    print(f"  over per-test budget {per_test} s: {t['name']} {t['duration']} ms")
ok = total <= budget and not over
print("OK" if ok else "FAIL: over budget")
sys.exit(0 if ok else 1)
