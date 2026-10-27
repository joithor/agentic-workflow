"""Make a page shareable: inline the style and every local image (png, jpg, svg) as data: URIs.
Usage: build_page.py <src.html> <out.html> <style.css>
The source marks the style spot with <!-- DOC-STYLE --> and references images by relative path
(<img src="diagrams/model.svg">), resolved against the source's folder. Output is one self-contained file; fails on any missing image."""
import base64, mimetypes, os, re, sys

src, out, css = sys.argv[1], sys.argv[2], sys.argv[3]
base = os.path.dirname(os.path.abspath(src))
html = open(src).read()
html = html.replace("<!-- DOC-STYLE -->", f"<style>\n{open(css).read()}</style>", 1)

def inline(m):
    ref = m.group(2)
    if ref.startswith(("data:", "http")):
        return m.group(0)
    path = os.path.join(base, ref)
    mime = mimetypes.guess_type(path)[0] or "application/octet-stream"
    return f'{m.group(1)}"data:{mime};base64,{base64.b64encode(open(path, "rb").read()).decode()}"'

html = re.sub(r'(<img[^>]*?\ssrc=)"([^"]+)"', inline, html)
open(out, "w").write(html)
print("wrote", out, f"{len(html)/1e6:.2f} MB")
