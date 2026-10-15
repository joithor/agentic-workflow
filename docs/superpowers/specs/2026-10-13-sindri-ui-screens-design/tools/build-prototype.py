#!/usr/bin/env python3
"""Build prototype.html from the eleven mockups (Python 3, stdlib only).

    prototype.html = tools/prototype-shell.html
                     + CSS   (mockups: the shared shell once, each page's rules scoped to .v-<page>;
                              then tools/prototype-only.css)
                     + TPL   (the big SVG fragments, taken from the mockups)
                     + JS    (tools/prototype-runtime.js: helpers, the fixture and CHIP_TABLE, the renderers,
                              router, actions, sheets, menus, palette, controls panel, announcements)

Run from anywhere:   python3 tools/build-prototype.py [--check] [--ref GIT_REF] [--out FILE]

  --check     build in memory and exit 1 if prototype.html on disk differs (CI-style; writes nothing)
  --ref REF   read the mockups from a git ref (e.g. 9f20ed53) instead of the working tree (for diffing builds)
  --out FILE  write somewhere else than prototype.html
  --css-only FILE   write just the merged mockup CSS (what tools/css-equiv.mjs compares against the mockups)

The build is a pure function of its inputs, so running it twice gives a byte-identical file.
"""
import argparse
import json
import re
import subprocess
import sys
from pathlib import Path

SPEC = Path(__file__).resolve().parent.parent
TOOLS = SPEC / "tools"
MOCKUPS = SPEC / "mockups"

# Page order decides where a rule lands in the merged sheet (first appearance wins).
PAGES = ["home", "workflows", "workflow-project", "workflow-incident", "item-sheet", "recipes",
         "recipe-inspector", "context", "context-filemap", "agents", "system"]


# ---------------------------------------------------------------- reading the mockups
def read_mockup(name, ref):
    if ref is None:
        return (MOCKUPS / f"{name}.html").read_text(encoding="utf-8")
    rel = (MOCKUPS / f"{name}.html").relative_to(subprocess.check_output(
        ["git", "rev-parse", "--show-toplevel"], cwd=SPEC, text=True).strip())
    return subprocess.check_output(["git", "show", f"{ref}:{rel}"], cwd=SPEC, text=True)


def style_of(html):
    m = re.search(r"<style[^>]*>(.*?)</style>", html, re.S)
    if not m:
        raise SystemExit("mockup has no <style>")
    return m.group(1)


# ---------------------------------------------------------------- css
def strip_comments(css):
    return re.sub(r"/\*.*?\*/", "", css, flags=re.S)


def split_blocks(css):
    """Top-level blocks: 'selector{body}' (an @media body keeps its nested rules). Returns [(prelude, body)]."""
    out, depth, start, pre = [], 0, 0, None
    for i, ch in enumerate(css):
        if ch == "{":
            if depth == 0:
                pre = css[start:i].strip()
                start = i + 1
            depth += 1
        elif ch == "}":
            depth -= 1
            if depth == 0:
                out.append((pre, css[start:i]))
                start = i + 1
    if css[start:].strip():
        raise SystemExit("css: trailing text after the last rule: " + css[start:start + 60])
    return out


def squeeze(text):
    """One line: a newline plus indent becomes one space, nothing is kept after '{' or before '}'."""
    t = re.sub(r"\s*\n\s*", " ", text.strip())
    return t.strip()


def split_top(selector_list):
    parts, depth, cur = [], 0, ""
    for ch in selector_list:
        if ch in "([":
            depth += 1
        elif ch in ")]":
            depth -= 1
        if ch == "," and depth == 0:
            parts.append(cur.strip())
            cur = ""
        else:
            cur += ch
    parts.append(cur.strip())
    return [p for p in parts if p]


def norm_rule(pre, body):
    """A comparable text for one rule (an @media rule keeps its inner rules in order)."""
    if pre.startswith("@"):
        inner = [norm_rule(p, b) for p, b in split_blocks(body)]
        return f"{squeeze(pre)}{{" + "\n".join(inner) + "}"
    return f"{squeeze(pre)}{{{squeeze(body)}}}"


def scope_rule(text, prefix):
    """Prefix every selector of a normalised rule with `prefix ` (recursing into @media). The prefix is a :where(...)
    list, which adds no specificity, so a scoped rule keeps the exact weight it had in its mockup."""
    if text.startswith("@"):
        head, _, rest = text.partition("{")
        inner = rest[:-1]
        blocks = [scope_rule(line, prefix) for line in inner.split("\n") if line.strip()]
        return f"{head}{{" + "\n".join(blocks) + "}"
    sel, _, body = text.partition("{")
    sels = [f"{prefix} {s}" for s in split_top(sel)]
    return ",".join(sels) + "{" + body


def parse_page_rules(css):
    rules = []
    for pre, body in split_blocks(strip_comments(css)):
        rules.append(norm_rule(pre, body))
    return rules


def build_css(styles):
    per_page = {n: parse_page_rules(styles[n]) for n in PAGES}
    # Merge the eleven rule lists into one sequence that keeps every page's own rule order (so the cascade of each
    # mockup is exactly what it was). A rule shared by several pages is one entry; when two pages order the same rules
    # differently, the later page gets its own copy of the rule instead of reordering the shared one.
    entries = []  # [text, [pages]]
    for n in PAGES:
        prev = -1
        for r in per_page[n]:
            j = next((k for k in range(prev + 1, len(entries)) if entries[k][0] == r and n not in entries[k][1]), None)
            if j is None:
                j = prev + 1
                entries.insert(j, [r, [n]])
            else:
                entries[j][1].append(n)
            prev = j
    # A rule every page has stays unscoped (the shell, once); the others get a zero-specificity :where(.v-page) scope.
    return [r if len(pages) == len(PAGES) else scope_rule(r, ":where(" + ",".join(f".v-{p}" for p in pages) + ")")
            for r, pages in entries]


# ---------------------------------------------------------------- template fragments from the mockups
def band_templates(context_html):
    """The three lens-band SVGs of context.html: nodes become focusable groups the prototype can select."""
    svgs = [m.group(0) for m in re.finditer(r"<svg[^>]*>.*?</svg>", context_html, re.S) if len(m.group(0)) > 1200][:3]
    if len(svgs) != 3:
        raise SystemExit("context.html: expected three band SVGs")
    out = []
    for svg in svgs:
        svg = svg.replace('role="img"', 'role="group"', 1)
        # a node is a <rect> with its <text> right behind it on the same line; the pair becomes one focusable group
        svg = re.sub(r'<rect class="(node[^"]*)" data-node="([^"]+)"([^>]*)/>(<text.*?</text>)',
                     lambda m: f'<g class="nd" data-node="{m.group(2)}" tabindex="0" role="button">'
                               f'<rect class="{m.group(1)}"{m.group(3)}/>{m.group(4)}</g>', svg)
        out.append(svg)
    return out


def xedges_template(context_html):
    m = re.search(r'<svg class="xedges".*?</svg>', context_html, re.S)
    if not m:
        raise SystemExit("context.html: no xedges SVG")
    return m.group(0)


def sysgraph_template(system_html):
    for m in re.finditer(r"<svg[^>]*>.*?</svg>", system_html, re.S):
        if 'aria-label="Dependency graph' in m.group(0)[:300]:
            return m.group(0)
    raise SystemExit("system.html: no dependency graph SVG")


# ---------------------------------------------------------------- assemble
def merged_css(ref=None):
    """The mockup-derived part of the stylesheet only (no prototype-only layer): shell once, then scoped rules."""
    styles = {n: style_of(read_mockup(n, ref)) for n in PAGES}
    return "\n".join(build_css(styles)) + "\n"


def build(ref=None):
    htmls = {n: read_mockup(n, ref) for n in PAGES}
    styles = {n: style_of(h) for n, h in htmls.items()}
    only = (TOOLS / "prototype-only.css").read_text(encoding="utf-8")
    css = "\n".join(build_css(styles)) + "\n\n" + only
    tpl = {
        "bands": band_templates(htmls["context"]),
        "xedges": xedges_template(htmls["context"]),
        "sysgraph": sysgraph_template(htmls["system"]),
    }
    script = "const TPL=" + json.dumps(tpl, ensure_ascii=False) + ";\n\n" + \
             (TOOLS / "prototype-runtime.js").read_text(encoding="utf-8")
    page = (TOOLS / "prototype-shell.html").read_text(encoding="utf-8")
    return page.replace("{{STYLE}}\n", css, 1).replace("{{SCRIPT}}\n", script, 1)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--ref")
    ap.add_argument("--out")
    ap.add_argument("--css-only")
    a = ap.parse_args()
    if a.css_only:
        Path(a.css_only).write_text(merged_css(a.ref), encoding="utf-8")
        print(f"wrote {a.css_only}")
        return
    html = build(a.ref)
    target = Path(a.out) if a.out else SPEC / "prototype.html"
    if a.check:
        same = target.exists() and target.read_text(encoding="utf-8") == html
        print("prototype.html is up to date" if same else "prototype.html differs from a fresh build")
        sys.exit(0 if same else 1)
    target.write_text(html, encoding="utf-8")
    print(f"wrote {target} ({len(html.encode('utf-8'))} bytes)")


if __name__ == "__main__":
    main()
