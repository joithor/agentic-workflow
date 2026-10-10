// scripts/deps/yaml.mjs — the YAML subset deps.yaml is written in, parsed without a library.
//
// Supported: block mappings and sequences, plain, 'single' and "double" quoted scalars, [flow, lists] of
// scalars, # comments. Rejected with a located YamlError: anchors, aliases, tags, block scalars (| >),
// flow mappings ({}), directives, multiple documents, tab indentation, duplicate keys, double-quoted
// escapes JSON doesn't share, and integers beyond 2^53. Maps have a null prototype, so a key such as
// __proto__ stays an ordinary key that the schema can reject.

export class YamlError extends Error {
  constructor(line, msg) { super(`deps.yaml:${line}: ${msg}`); }
}

// A quote opens a quoted scalar only at the start of a value: after "key: ", "- ", "[" or ",".
// A quote in the middle of a plain scalar is text, so " #" after it still starts a comment, as in YAML.
const VALUE_START = /(?:^|:\s|-\s|[[,]\s*)$/;

function stripComment(text) {
  let q = null;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (q) {
      if (q === '"' && c === "\\") { i++; continue; }
      if (c === q) { if (q === "'" && text[i + 1] === "'") { i++; continue; } q = null; }
      continue;
    }
    if ((c === '"' || c === "'") && VALUE_START.test(text.slice(0, i))) { q = c; continue; }
    if (c === "#" && (i === 0 || /\s/.test(text[i - 1]))) return text.slice(0, i).trimEnd();
  }
  return text.trimEnd();
}

const BANNED = { "&": "an anchor", "*": "an alias", "!": "a tag", "|": "a block scalar", ">": "a block scalar", "{": "a flow mapping", "%": "a directive", "@": "a reserved indicator", "`": "a reserved indicator" };

function splitFlow(inner, line) {
  const parts = [];
  let cur = "";
  let q = null;
  for (const c of inner) {
    if (q) { cur += c; if (c === q) q = null; continue; }
    if ((c === '"' || c === "'") && cur.trim() === "") q = c;
    if (c === "[" || c === "{") throw new YamlError(line, "nested flow collections are outside the supported YAML subset");
    if (c === ",") { parts.push(cur); cur = ""; } else cur += c;
  }
  parts.push(cur);
  return parts;
}

function parseScalar(raw, line) {
  const s = raw.trim();
  if (BANNED[s[0]]) throw new YamlError(line, `${BANNED[s[0]]} is outside the supported YAML subset`);
  if (s.startsWith("[")) {
    if (!s.endsWith("]")) throw new YamlError(line, "unterminated flow sequence");
    const inner = s.slice(1, -1).trim();
    return inner === "" ? [] : splitFlow(inner, line).map((p) => parseScalar(p, line));
  }
  if (s.startsWith('"')) {
    if (!/^"(?:[^"\\]|\\.)*"$/.test(s)) throw new YamlError(line, "bad double-quoted string");
    try { return JSON.parse(s); } catch { throw new YamlError(line, "unsupported escape in a double-quoted string (use the JSON escapes \\\" \\\\ \\n \\t \\uXXXX)"); }
  }
  if (s.startsWith("'")) {
    if (!/^'(?:[^']|'')*'$/.test(s)) throw new YamlError(line, "bad single-quoted string");
    return s.slice(1, -1).replace(/''/g, "'");
  }
  if (s === "" || s === "null" || s === "~") return null;
  if (s === "true" || s === "false") return s === "true";
  if (/^-?[0-9]+$/.test(s)) {
    const n = Number(s);
    if (!Number.isSafeInteger(n)) throw new YamlError(line, `integer out of range: ${s}`);
    return n;
  }
  if (/:\s|:$/.test(s)) throw new YamlError(line, `ambiguous plain scalar "${s}" (quote it)`);
  return s;
}

function tokenize(text) {
  const lines = [];
  text.split("\n").forEach((rawLine, i) => {
    const lead = /^[ \t]*/.exec(rawLine)[0];
    if (lead.includes("\t")) throw new YamlError(i + 1, "tab indentation");
    const body = stripComment(rawLine.slice(lead.length));
    if (body === "") return;
    if (body === "---" || body === "...") throw new YamlError(i + 1, "multiple documents are outside the supported YAML subset");
    lines.push({ indent: lead.length, text: body, line: i + 1 });
  });
  return lines;
}

const isItem = (t) => t === "-" || t.startsWith("- ");

export function parseYaml(text) {
  const lines = tokenize(text);
  let i = 0;
  const block = (indent) => (isItem(lines[i].text) ? seq(indent) : map(indent));
  function seq(indent) {
    const out = [];
    while (i < lines.length && lines[i].indent === indent && isItem(lines[i].text)) {
      const l = lines[i];
      const rest = l.text.slice(1).trimStart();
      if (rest === "") { i++; out.push(i < lines.length && lines[i].indent > indent ? block(lines[i].indent) : null); continue; }
      if (/^[A-Za-z_][A-Za-z0-9_-]*:(\s|$)/.test(rest)) {
        lines[i] = { indent: indent + (l.text.length - rest.length), text: rest, line: l.line };
        out.push(map(lines[i].indent));
        continue;
      }
      if (isItem(rest)) {
        lines[i] = { indent: indent + (l.text.length - rest.length), text: rest, line: l.line };
        out.push(seq(lines[i].indent));
        continue;
      }
      out.push(parseScalar(rest, l.line));
      i++;
    }
    return out;
  }
  function map(indent) {
    const out = Object.create(null);
    while (i < lines.length && lines[i].indent === indent && !isItem(lines[i].text)) {
      const l = lines[i];
      const m = /^([A-Za-z_][A-Za-z0-9_-]*):(?:\s+(.*))?$/.exec(l.text);
      if (!m) throw new YamlError(l.line, `expected "key: value", got "${l.text}"`);
      if (Object.hasOwn(out, m[1])) throw new YamlError(l.line, `duplicate key "${m[1]}"`);
      i++;
      if (m[2] !== undefined && m[2] !== "") { out[m[1]] = parseScalar(m[2], l.line); continue; }
      const next = lines[i];
      if (next && (next.indent > indent || (next.indent === indent && isItem(next.text)))) out[m[1]] = block(next.indent);
      else out[m[1]] = null;
    }
    if (i < lines.length && lines[i].indent > indent) throw new YamlError(lines[i].line, "unexpected indentation");
    return out;
  }
  if (lines.length === 0) return null;
  const doc = block(lines[0].indent);
  if (i < lines.length) throw new YamlError(lines[i].line, "unexpected indentation");
  return doc;
}
