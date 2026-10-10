// scripts/rules/exists.mjs — whether a rule record's check really exists in the tree (scripts/check-rules.mjs).
//
// Evidence is read from live configuration and code only: comments are stripped first, ids match as whole tokens
// (CA103 never matches CA1031), and an analyzer counts only at the severity it enforces (a rule switched off in the
// root .editorconfig is not a check). docs/csharp/rule-records.md documents each check type.

import fs from "node:fs";
import path from "node:path";

const TEXT_EXT = /\.(cs|mjs|js|md|props|targets|txt|json|yaml|yml)$|(^|\/)\.editorconfig$/;
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
const BUILD_FILES = ["Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "global.json", ".editorconfig", "nuget.config"];

// stripComments(file, text) — the text without the comments its format allows.
export function stripComments(file, text) {
  if (/\.(cs|mjs|js)$/.test(file)) return text.replace(/\/\*[\s\S]*?\*\//g, "").replace(/(^|[^:])\/\/.*$/gm, "$1");
  if (/\.(props|targets|csproj|xml)$/.test(file)) return text.replace(/<!--[\s\S]*?-->/g, "");
  if (/(^|\/)\.editorconfig$|\.(txt|ya?ml)$/.test(file)) {
    return text.split("\n").map((l) => (/^\s*[#;]/.test(l) ? "" : l.replace(/\s+#.*$/, ""))).join("\n");
  }
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
  hasLiteral(rels, s) { return rels.some((r) => (this.text(r) ?? "").includes(s)); }
}

// The severities the root .editorconfig sets per diagnostic id (the last line wins, as in editorconfig).
function rootSeverities(tree) {
  const out = new Map();
  for (const m of (tree.text(".editorconfig") ?? "").matchAll(/^\s*dotnet_diagnostic\.([A-Za-z0-9]+)\.severity\s*=\s*([a-z]+)/gm)) out.set(m[1], m[2]);
  return out;
}

function analyzer(tree, c) {
  const want = c.severity ? [c.severity] : ["error", "warning"];
  if (/^AW[0-9]{4}$/.test(c.id) && !tree.hasToken(["src/Aw.Analyzers"], c.id)) {
    return { found: false, where: "src/Aw.Analyzers (no diagnostic descriptor with this id)" };
  }
  const set = rootSeverities(tree).get(c.id);
  if (set !== undefined) {
    return want.includes(set)
      ? { found: true, where: ".editorconfig" }
      : { found: false, where: `.editorconfig (the root .editorconfig sets severity ${set}; the check needs ${want.join(" or ")})` };
  }
  if (c.via !== "baseline") return { found: false, where: `.editorconfig (no dotnet_diagnostic.${c.id}.severity = ${want.join("|")} line; add one, or mark the check via: baseline with its package)` };
  const bulk = /^\s*dotnet_analyzer_diagnostic\.severity\s*=\s*error\b/m.test(tree.text(".editorconfig") ?? "");
  const pkg = tree.hasLiteral(["Directory.Packages.props"], `Include="${c.package}"`);
  if (!bulk) return { found: false, where: ".editorconfig (no bulk dotnet_analyzer_diagnostic.severity = error line for the baseline)" };
  if (!pkg) return { found: false, where: `Directory.Packages.props (the analyzer package ${c.package} isn't referenced)` };
  return { found: true, where: `.editorconfig bulk baseline + ${c.package}` };
}

function bannedSymbols(tree, c) {
  const banned = tree.text("BannedSymbols.txt");
  const wired = /<AdditionalFiles\s[^>]*Include="[^"]*BannedSymbols\.txt"/.test(tree.text("Directory.Build.props") ?? "");
  const listed = new Set((banned ?? "").split("\n").map((l) => l.split(";")[0].trim()).filter(Boolean));
  const missing = (c.entries ?? []).filter((e) => !listed.has(e));
  if (c.id !== "RS0030") return { found: false, where: "banned-symbols checks are RS0030 (BannedApiAnalyzers)" };
  if (banned === null) return { found: false, where: "BannedSymbols.txt (missing)" };
  if (!wired) return { found: false, where: "Directory.Build.props (no AdditionalFiles item wires BannedSymbols.txt)" };
  if (missing.length) return { found: false, where: `BannedSymbols.txt (missing: ${missing.join(", ")})` };
  return { found: true, where: "BannedSymbols.txt wired in Directory.Build.props" };
}

function ciStep(tree, c) {
  const names = [...(tree.text(c.ref) ?? "").matchAll(/^\s*(?:-\s+)?name:\s*(.+?)\s*$/gm)].map((m) => m[1].replace(/^(["'])(.*)\1$/, "$2"));
  return { found: names.includes(c.id), where: `${c.ref} (a step named exactly "${c.id}")` };
}

// existsCheck(tree, check) -> { found, where } — whether the check is in the tree, and what was looked at
export function existsCheck(tree, c) {
  switch (c.type) {
    case "analyzer": return analyzer(tree, c);
    case "compiler": return { found: /^(CS|IL)[0-9]{4}$/.test(c.id), where: "the C# compiler and trim analyzers (a CS or IL id)" };
    case "banned-symbols": return bannedSymbols(tree, c);
    case "build-setting": return { found: tree.hasLiteral(BUILD_FILES, c.id), where: BUILD_FILES.join(", ") };
    case "test": return { found: tree.exists(c.ref) && tree.hasToken([c.ref], c.id), where: c.ref };
    case "script": return { found: tree.exists(c.ref), where: c.ref };
    case "ci": return ciStep(tree, c);
    case "review": return { found: true, where: "review" };
    default: return { found: tree.hasToken(SEARCH[c.type], c.id), where: SEARCH[c.type].join(", ") };
  }
}
