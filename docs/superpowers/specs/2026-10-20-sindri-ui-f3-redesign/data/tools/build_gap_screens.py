#!/usr/bin/env python3
"""Writes the all-screens-pass pages into ../../screens/. Usage: python3 build_gap_screens.py [agents work system jobs recipes home]"""
import sys, pathlib, importlib
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from gap_common import ROOT
names = sys.argv[1:] or ["agents", "work", "system", "jobs", "recipes", "home"]
for n in names:
    m = importlib.import_module("pg_" + n)
    (ROOT / f"{n}.html").write_text(m.build())
    print("wrote", n + ".html")
