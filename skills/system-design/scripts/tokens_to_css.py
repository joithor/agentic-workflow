"""Build the page stylesheet from the project's design-tokens.json (W3C DTCG) plus templates/doc-base.css.
Usage: tokens_to_css.py <design-tokens.json> <out.css>
Maps token names onto the semantic variables doc-base.css uses; anything it cannot map keeps the neutral fallback in
doc-base.css, and is printed so the run can ask the user (or run /design-language) instead of guessing."""
import json, os, sys

tokens_path, out = sys.argv[1], sys.argv[2]
tokens = json.load(open(tokens_path))

def flat(node, prefix=""):
    for k, v in node.items():
        if k.startswith("$"):
            continue
        name = f"{prefix}{k}"
        if isinstance(v, dict) and "$value" in v:
            yield name, v["$value"]
        elif isinstance(v, dict):
            yield from flat(v, name + ".")

flat_tokens = dict(flat(tokens))
# semantic variable -> token names to try, first hit wins
WANT = {
    "--bg": ["color.background", "color.bg", "color.surface-muted"],
    "--surface": ["color.surface", "color.card", "color.background-elevated"],
    "--ink": ["color.text", "color.foreground", "color.ink", "color.text-primary"],
    "--muted": ["color.text-muted", "color.muted", "color.text-secondary"],
    "--line": ["color.border", "color.line", "color.divider"],
    "--accent": ["color.accent", "color.primary", "color.brand"],
    "--secondary": ["color.secondary", "color.accent-secondary"],
    "--good": ["color.success", "color.positive", "color.green"],
    "--warn": ["color.warning", "color.caution", "color.yellow"],
    "--bad": ["color.danger", "color.error", "color.destructive", "color.red"],
    "--f-body": ["font.family-body", "font.family-sans", "font.family", "typography.body"],
    "--f-mono": ["font.family-mono", "typography.mono"],
    "--radius": ["radius.md", "radius.base", "radius.default"],
}
lines, missing = [], []
for var, names in WANT.items():
    hit = next((n for n in names if n in flat_tokens), None)
    if hit:
        lines.append(f"  {var}: {flat_tokens[hit]};")
    else:
        missing.append(var)
# every other color is exposed too, for diagram-adjacent callouts: --color-<name>
for n, v in flat_tokens.items():
    if n.startswith("color."):
        lines.append(f"  --{n.replace('.', '-')}: {v};")
base = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "templates", "doc-base.css")).read()
# project tokens go last so they win over the neutral fallbacks at the top of doc-base.css
open(out, "w").write(f"/* generated from {os.path.basename(tokens_path)} by tokens_to_css.py */\n" + base + "\n:root {\n" + "\n".join(lines) + "\n}\n")
print("wrote", out, "| unmapped (neutral fallback used):", ", ".join(missing) or "none")
