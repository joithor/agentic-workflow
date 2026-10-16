// scripts/rules/exists.mjs — whether a rule record's check really exists in the tree (scripts/check-rules.mjs).
//
// Evidence is read from live configuration and code only: comments are stripped first, ids match as whole tokens
// (CA103 never matches CA1031), and an analyzer counts only at the severity it enforces (a rule switched off in the
// root .editorconfig is not a check). docs/csharp/rule-records.md documents each check type.

import fs from "node:fs";
import path from "node:path";
import { lexTs } from "../deps/scan.mjs";

const TEXT_EXT = /\.(cs|mjs|js|md|props|targets|txt|json|yaml|yml|config)$|(^|\/)\.editorconfig$/;
const SKIP_DIRS = new Set(["bin", "obj", "node_modules", "TestResults"]);
const escapeRe = (s) => s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
const token = (id) => new RegExp(`(^|[^A-Za-z0-9_])${escapeRe(id)}(?![A-Za-z0-9_])`, "m");

// Where each id-based check type's id has to appear (relative paths; a directory is searched recursively).
export const SEARCH = {
  archunit: ["tests/Aw.Architecture.Tests"],
  "check-deps": ["scripts/check-deps.mjs", "scripts/deps"],
  "lane-cap": ["tests"],
  "check-rules": ["scripts/check-rules.mjs", "scripts/rules"],
};
const BUILD_FILES = ["Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "global.json", ".editorconfig", "nuget.config", "CodeMetricsConfig.txt", "deps.yaml"];

// stripCs(text) — C# without // and /* */ comments. Known limit: interpolation holes in $"…" aren't lexed, so a char
// literal '"' inside a hole ends the string early (the damage stays on that line). Strings are copied as they are: "…" (with \ escapes), verbatim
// @"…", $@"…" and @$"…" ("" escapes), raw """…""" (closed by the same run of quotes) and char literals.
function stripCs(text) {
  let out = "";
  let i = 0;
  const copyTo = (j) => { out += text.slice(i, j); i = j; };
  while (i < text.length) {
    const c = text[i];
    const d = text[i + 1];
    if (c === "/" && d === "/") { while (i < text.length && text[i] !== "\n") i++; continue; }
    if (c === "/" && d === "*") { const e = text.indexOf("*/", i + 2); i = e < 0 ? text.length : e + 2; out += " "; continue; }
    const prefix = text.slice(i).match(/^(\$@|@\$|@|\$*)"/);
    if (prefix) {
      const q = text.slice(i + prefix[1].length).match(/^"+/)[0].length;
      let j = i + prefix[1].length + q;
      const verbatim = prefix[1].includes("@");
      if (q >= 3 && !verbatim) { const e = text.indexOf('"'.repeat(q), j); copyTo(e < 0 ? text.length : e + q); continue; }
      if (q === 2 && !verbatim) { copyTo(j); continue; } // "" is an empty string
      if (verbatim) {
        j = i + prefix[1].length + 1; // the opening quote; "" pairs after it are escaped quotes
        while (j < text.length && !(text[j] === '"' && text[j + 1] !== '"')) j += text[j] === '"' ? 2 : 1;
      }
      else { while (j < text.length && text[j] !== '"' && text[j] !== "\n") j += text[j] === "\\" ? 2 : 1; }
      copyTo(j + 1); continue;
    }
    if (c === "'") { let j = i + 1; while (j < text.length && text[j] !== "'" && text[j] !== "\n") j += text[j] === "\\" ? 2 : 1; copyTo(j + 1); continue; }
    out += c; i++;
  }
  return out;
}

// stripJs(text) — JavaScript without comments, through check-deps' lexer (regex literals, template nesting). A file
// the lexer can't read (an unterminated literal) counts as empty.
function stripJs(text) {
  try { return lexTs(text).code; } catch { return ""; } // proves nothing, as check-deps fails closed on it
}

// stripHashComments(text) — .editorconfig, YAML and text files: a line starting with # or ; is a comment, and so is
// " #…" outside quotes.
function stripHashComments(text) {
  return text.split("\n").map((l) => {
    if (/^\s*[#;]/.test(l)) return "";
    let q = null;
    for (let i = 0; i < l.length; i++) {
      const c = l[i];
      if (q) { if (c === q) q = null; } else if (c === '"' || c === "'") q = c;
      else if (c === "#" && /\s/.test(l[i - 1] ?? "")) return l.slice(0, i).trimEnd();
    }
    return l;
  }).join("\n");
}

// stripComments(file, text) — the text without the comments its format allows.
export function stripComments(file, text) {
  if (/\.cs$/.test(file)) return stripCs(text);
  if (/\.(mjs|js)$/.test(file)) return stripJs(text);
  if (/\.(props|targets|csproj|xml|config)$/.test(file)) {
    return text.split(/(<!\[CDATA\[[\s\S]*?\]\]>)/).map((part) => (part.startsWith("<![CDATA[") ? part : part.replace(/<!--[\s\S]*?-->/g, ""))).join("");
  }
  if (/(^|\/)\.editorconfig$|\.(txt|ya?ml)$/.test(file)) return stripHashComments(text);
  return text;
}

export class Tree {
  constructor(root) { this.root = root; this.cache = new Map(); }

  // text(rel) — the comment-stripped text of one file, or null when it doesn't exist
  text(rel) {
    if (!this.cache.has(rel)) {
      let t = null;
      try { t = stripComments(rel, fs.readFileSync(path.join(this.root, rel), "utf8")); } catch { t = null; }
      this.cache.set(rel, t);
    }
    return this.cache.get(rel);
  }

  exists(rel) { return fs.existsSync(path.join(this.root, rel)); }

  // files(rel) — rel itself if it's a file, else every text file under it
  files(rel) {
    let st;
    try { st = fs.statSync(path.join(this.root, rel)); } catch { return []; }
    if (st.isFile()) return [rel];
    const out = [];
    for (const e of fs.readdirSync(path.join(this.root, rel), { withFileTypes: true })) {
      if (SKIP_DIRS.has(e.name)) continue;
      const child = path.posix.join(rel, e.name);
      if (e.isDirectory()) out.push(...this.files(child));
      else if (TEXT_EXT.test(e.name)) out.push(child);
    }
    return out;
  }

  hasToken(rels, id) { const re = token(id); return rels.flatMap((r) => this.files(r)).some((f) => re.test(this.text(f) ?? "")); }
  // hasLiteral — a literal bounded like a token, so "CA1502: 10" never matches "CA1502: 100"
  hasLiteral(rels, s) { const re = token(s); return rels.some((r) => re.test(this.text(r) ?? "")); }
}

// topLevel(list) — a brace list's elements, split on commas outside nested braces
function topLevel(list) {
  const out = [];
  let depth = 0;
  let cur = "";
  for (const ch of list) {
    if (ch === "," && depth === 0) { out.push(cur.trim()); cur = ""; continue; }
    if (ch === "{") depth++;
    if (ch === "}") depth--;
    cur += ch;
  }
  out.push(cur.trim());
  return out;
}

// A section that applies to C# files in general ([*], [*.cs], [**.cs], [**/*.cs], [*.{…,cs,…}], [{*.cs,…}], [{*.{cs,…}}]); a narrower section
// (a path glob) neither enables nor disables a rule for the whole repo.
function isGeneralCs(glob) {
  if (/^(\*|\*\*|\*\*\/\*)$/.test(glob)) return true;
  const list = glob.match(/^\{(.*)\}$/);
  if (list) return topLevel(list[1]).some((g) => isGeneralCs(g));
  const ext = glob.match(/^(?:\*|\*\*|\*\*\/\*)\.(.+)$/);
  if (!ext) return false;
  const exts = ext[1].match(/^\{(.*)\}$/);
  return exts ? topLevel(exts[1]).some((e) => e === "cs" || (/^\{.*\}$/.test(e) && topLevel(e.slice(1, -1)).includes("cs"))) : ext[1] === "cs";
}

// editorconfigKeys(tree) — the properties the root .editorconfig's general C# sections set, keys lower-cased
// (editorconfig keys are case-insensitive), the last line winning.
function editorconfigKeys(tree) {
  const keys = new Map();
  let applies = false;
  for (const line of (tree.text(".editorconfig") ?? "").split("\n")) {
    const section = line.match(/^\s*\[(.*)\]\s*$/);
    if (section) { applies = isGeneralCs(section[1].trim()); continue; }
    // Roslyn's property syntax: key = value or key: value, a # or ; ending the value.
    const kv = applies && line.match(/^\s*([\w.\-]+)\s*[=:]\s*(.*?)\s*(?:[#;].*)?$/);
    if (kv) keys.set(kv[1].toLowerCase(), kv[2]);
  }
  return keys;
}

// rootSeverities(tree) — per-id severities (ids upper-cased), per-category severities and the bulk severity.
function rootSeverities(tree) {
  const ids = new Map();
  const categories = new Map();
  let bulk = null;
  for (const [key, raw] of editorconfigKeys(tree)) {
    const value = raw.toLowerCase();
    const id = key.match(/^dotnet_diagnostic\.([a-z0-9_]+)\.severity$/);
    const cat = key.match(/^dotnet_analyzer_diagnostic\.category-(.+)\.severity$/);
    if (id) ids.set(id[1].toUpperCase(), value);
    else if (cat) categories.set(cat[1], value);
    else if (key === "dotnet_analyzer_diagnostic.severity") bulk = value;
  }
  return { ids, categories, bulk };
}

// buildSetting(tree, c) — a literal in the root build files; an editorconfig property (`key = value`) must be
// the value a general C# section of the root .editorconfig ends up with.
function buildSetting(tree, c) {
  const prop = c.id.match(/^\s*([A-Za-z_][\w.\-]*)\s+=\s+(.+?)\s*$/);
  if (prop) {
    const got = editorconfigKeys(tree).get(prop[1].toLowerCase());
    const found = got !== undefined && got.toLowerCase() === prop[2].toLowerCase();
    return { found, where: `.editorconfig (a general C# section ${got === undefined ? "doesn't set it" : `ends with ${prop[1]} = ${got}`})` };
  }
  return { found: tree.hasLiteral(BUILD_FILES, c.id), where: BUILD_FILES.join(", ") };
}

function analyzer(tree, c) {
  const want = c.severity ? [c.severity] : ["error", "warning"];
  if (/^AW[0-9]{4}$/.test(c.id) && !tree.hasToken(["src/Aw.Analyzers"], c.id)) {
    return { found: false, where: "src/Aw.Analyzers (no diagnostic descriptor with this id)" };
  }
  const sev = rootSeverities(tree);
  const set = sev.ids.get(c.id.toUpperCase());
  if (set !== undefined) {
    return want.includes(set)
      ? { found: true, where: ".editorconfig" }
      : { found: false, where: `.editorconfig (the root .editorconfig sets severity ${set}; the check needs ${want.join(" or ")})` };
  }
  if (c.via !== "baseline") return { found: false, where: `.editorconfig (no dotnet_diagnostic.${c.id}.severity = ${want.join("|")} line; add one, or mark the check via: baseline with its package)` };
  const pkg = new RegExp(`<GlobalPackageReference\\s[^>]*Include="${escapeRe(c.package)}"`).test(tree.text("Directory.Packages.props") ?? "");
  const cat = sev.categories.get(c.category.toLowerCase());
  if (sev.bulk !== "error") return { found: false, where: ".editorconfig (no bulk dotnet_analyzer_diagnostic.severity = error line in a general C# section for the baseline)" };
  if (!pkg) return { found: false, where: `Directory.Packages.props (no GlobalPackageReference to ${c.package})` };
  if (cat !== undefined && cat !== "error") return { found: false, where: `.editorconfig (dotnet_analyzer_diagnostic.category-${c.category}.severity = ${cat} turns the baseline off)` };
  return { found: true, where: `.editorconfig bulk baseline + ${c.package}` };
}

function bannedSymbols(tree, c) {
  // `ref` names the file when the check isn't in the Core list (BannedSymbols.All.txt, BannedSymbols.Tests.txt, ...).
  const file = c.ref ?? "BannedSymbols.txt";
  const banned = tree.text(file);
  const wired = new RegExp(`<AdditionalFiles\\s[^>]*Include="[^"]*${escapeRe(file)}"`).test(tree.text("Directory.Build.props") ?? "");
  const listed = new Set((banned ?? "").split("\n").map((l) => l.split(";")[0].trim()).filter(Boolean));
  const missing = (c.entries ?? []).filter((e) => !listed.has(e));
  if (c.id !== "RS0030") return { found: false, where: "banned-symbols checks are RS0030 (BannedApiAnalyzers)" };
  if (banned === null) return { found: false, where: `${file} (missing)` };
  if (!wired) return { found: false, where: `Directory.Build.props (no AdditionalFiles item wires ${file})` };
  if (missing.length) return { found: false, where: `${file} (missing: ${missing.join(", ")})` };
  return { found: true, where: `${file} wired in Directory.Build.props` };
}

// stepNames(text) — the names of workflow steps: `name:` keys of the items of a `steps:` list (not jobs or `with:`).
function stepNames(text) {
  const names = [];
  let stepsIndent = -1;
  let itemIndent = -1;
  for (const line of text.split("\n")) {
    if (!line.trim()) continue;
    const indent = line.length - line.trimStart().length;
    const flushItem = indent === stepsIndent && /^\s*-\s/.test(line);
    if (stepsIndent >= 0 && indent <= stepsIndent && !flushItem) stepsIndent = -1;
    if (/^\s*steps:\s*$/.test(line)) { stepsIndent = indent; itemIndent = -1; continue; }
    if (stepsIndent < 0) continue;
    const item = line.match(/^(\s*)-\s+(.*)$/);
    if (item && (itemIndent < 0 || item[1].length === itemIndent)) itemIndent = item[1].length;
    const key = item && item[1].length === itemIndent ? item[2] : indent === itemIndent + 2 ? line.trim() : null;
    const m = key?.match(/^name:\s*(.+?)\s*$/);
    if (m) names.push(m[1].replace(/^(["'])(.*)\1$/, "$2"));
  }
  return names;
}

function ciStep(tree, c) {
  return { found: stepNames(tree.text(c.ref) ?? "").includes(c.id), where: `${c.ref} (a step named exactly "${c.id}")` };
}

// existsCheck(tree, check) -> { found, where } — whether the check is in the tree, and what was looked at
export function existsCheck(tree, c) {
  switch (c.type) {
    case "analyzer": return analyzer(tree, c);
    case "compiler": return { found: /^(CS|IL)[0-9]{4}$/.test(c.id), where: "the C# compiler and trim analyzers (a CS or IL id)" };
    case "banned-symbols": return bannedSymbols(tree, c);
    case "build-setting": return buildSetting(tree, c);
    case "test": return { found: tree.exists(c.ref) && tree.hasToken([c.ref], c.id), where: c.ref };
    case "script": return { found: tree.exists(c.ref), where: c.ref };
    case "ci": return ciStep(tree, c);
    case "review": return { found: true, where: "review" };
    default: return { found: tree.hasToken(SEARCH[c.type], c.id), where: SEARCH[c.type].join(", ") };
  }
}
