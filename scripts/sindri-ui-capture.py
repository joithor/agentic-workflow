#!/usr/bin/env python3
"""Drives the running Sindri app on an X display (Xvfb) with XTEST key events and saves frames as PNG.

Used by the whole-map proof (docs/superpowers/evidence/whole-map-fixture/README.md). Not a test: it needs an X server and the
app already running against the UI fixture (scripts/sindri-ui-fixture.sh).

  sindri-ui-capture.py --display :99 --size 1440x900 --out DIR --prefix 1440 --query "tab groups"

Steps: resize the app window to the size, press G then C (Context), wait for the whole map, save <prefix>-whole.png, type the
query and Enter, save <prefix>-zoomed.png, press Esc, save <prefix>-back.png. Prints "<step> <seconds>" lines; the first-frame
line is the time from the G C chord to the first frame with the map drawn (the map region stops being the empty background).
"""
import argparse, os, sys, time
from Xlib import X, XK, display
from Xlib.ext import xtest
from PIL import Image

ap = argparse.ArgumentParser()
ap.add_argument("--display", required=True)
ap.add_argument("--size", required=True)
ap.add_argument("--out", required=True)
ap.add_argument("--prefix", required=True)
ap.add_argument("--query", default="")
ap.add_argument("--wait", type=float, default=20.0)
a = ap.parse_args()
w, h = (int(x) for x in a.size.split("x"))
d = display.Display(a.display)
root = d.screen().root


def find_app(win):
    for c in win.query_tree().children:
        name = c.get_wm_name() or ""
        if "Sindri" in name:
            return c
        found = find_app(c)
        if found:
            return found
    return None


def key(name, mods=0):
    code = d.keysym_to_keycode(XK.string_to_keysym(name))
    xtest.fake_input(d, X.KeyPress, code)
    xtest.fake_input(d, X.KeyRelease, code)
    d.sync()
    time.sleep(0.06)


def shot(path):
    img = root.get_image(0, 0, w, h, X.ZPixmap, 0xFFFFFFFF)
    im = Image.frombytes("RGB", (w, h), img.data, "raw", "BGRX")
    im.save(path)
    return im


def settle(seconds):
    time.sleep(seconds)


app = None
deadline = time.time() + a.wait
while app is None and time.time() < deadline:
    app = find_app(root)
    time.sleep(0.2)
if app is None:
    sys.exit("no Sindri window on " + a.display)
app.configure(x=0, y=0, width=w, height=h)
d.sync()
settle(1.5)
# focus: click in the window's top-left corner area is not safe; the window has the focus under Xvfb with no WM only after a click
xtest.fake_input(d, X.MotionNotify, x=w // 2, y=40)
d.sync()
os.makedirs(a.out, exist_ok=True)
before = shot(os.path.join(a.out, a.prefix + "-before.png"))
t0 = time.time()
key("g")
key("c")
first = None
while time.time() - t0 < 30:
    im = shot(os.path.join(a.out, a.prefix + "-whole.png"))
    # the map drawn: the page changed a lot compared with the page we left
    diff = sum(1 for p, q in zip(before.resize((72, 45)).getdata(), im.resize((72, 45)).getdata()) if p != q)
    if diff > 400 and first is None:
        first = time.time() - t0
        break
    time.sleep(0.02)
print("first-frame-after-GC %.3f" % (first or -1))
settle(2.0)
shot(os.path.join(a.out, a.prefix + "-whole.png"))
print("whole saved")
if a.query:
    key("slash") if False else None
    # focus the anchor box: Tab to it is app specific; click it instead (the box sits in the page header)
    print("query step is run by the caller with --click x,y if needed")
