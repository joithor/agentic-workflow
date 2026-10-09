import { createHash } from "node:crypto";
import { createRequire } from "node:module";
import type TS from "typescript";

import { isSourcePath } from "./files.js";

export interface ParsedSymbol {
  name: string;
  kind: "function" | "method" | "arrow" | "class";
  file: string;
  startLine: number;
  endLine: number;
  exported: boolean;
  signature: string;
  astHash: string;
  tokens: string[];
  complexity: number;
  callees: string[];
  text: string;
}

export interface Parser {
  supports(p: string): boolean;
  parse(file: string, text: string): ParsedSymbol[];
}

const TEXT_CAP = 4000;

// Bump when parsing or hashing changes: every structure/clone row is rebuilt.
export const INDEXER_VERSION = "1";

// The parser's identity, recorded with every AST hash it makes (the structure stamp, each shape
// run). Hashes are built from SyntaxKind names, which a TypeScript upgrade can rename, so hashes
// from different parsers never compare. The version is read from the package.json of the same
// typescript the lazy loader loads, so asking for it never loads the compiler.
export const parserId = (): string =>
  `parse-ts@${INDEXER_VERSION}+ts${(createRequire(import.meta.url)("typescript/package.json") as { version: string }).version}`;

// `typescript` is about 9 MB and costs a few hundred ms to load, so it is loaded on the
// first parse: `sindri --help`, `observe` and the hook's `scrub --staged` never pay for it.
interface Kit {
  ts: typeof TS;
  decisions: ReadonlySet<TS.SyntaxKind>;
  logical: ReadonlySet<TS.SyntaxKind>;
}

let kit: Kit | undefined;

function load(): Kit {
  if (kit === undefined) {
    const ts = createRequire(import.meta.url)("typescript") as typeof TS;
    const k = ts.SyntaxKind;
    kit = {
      ts,
      decisions: new Set([k.IfStatement, k.ForStatement, k.ForInStatement, k.ForOfStatement, k.WhileStatement, k.DoStatement, k.CaseClause, k.CatchClause, k.ConditionalExpression]),
      logical: new Set([k.AmpersandAmpersandToken, k.BarBarToken, k.QuestionQuestionToken]),
    };
  }
  return kit;
}

function scriptKind(ts: typeof TS, file: string): TS.ScriptKind {
  if (file.endsWith(".tsx")) return ts.ScriptKind.TSX;
  if (file.endsWith(".jsx")) return ts.ScriptKind.JSX;
  if (/\.(?:js|mjs|cjs)$/.test(file)) return ts.ScriptKind.JS;
  return ts.ScriptKind.TS;
}

// Identifiers and literals are abstracted, so a renamed copy hashes the same (spec §6.2 clones).
function tokensOf(ts: typeof TS, node: TS.Node): string[] {
  const out: string[] = [];
  const walk = (n: TS.Node): void => {
    if (ts.isIdentifier(n) || ts.isPrivateIdentifier(n)) out.push("$id");
    else if (ts.isLiteralExpression(n) || n.kind === ts.SyntaxKind.TrueKeyword || n.kind === ts.SyntaxKind.FalseKeyword) out.push("$lit");
    else if (n.kind === ts.SyntaxKind.ExportKeyword || n.kind === ts.SyntaxKind.DefaultKeyword || n.kind === ts.SyntaxKind.DeclareKeyword) {
      // Not part of the structure: `export function f` and `function f` are the same clone.
    } else {
      out.push(ts.SyntaxKind[n.kind]);
      // forEachChild skips these nodes' operator tokens, so hash them explicitly (`!a` vs `-a`).
      if (ts.isPrefixUnaryExpression(n) || ts.isPostfixUnaryExpression(n) || ts.isTypeOperatorNode(n)) out.push(ts.SyntaxKind[n.operator]);
      else if (ts.isMetaProperty(n)) out.push(ts.SyntaxKind[n.keywordToken]);
      ts.forEachChild(n, walk);
    }
  };
  walk(node);
  return out;
}

function analyze(k: Kit, node: TS.Node): { complexity: number; callees: string[] } {
  const { ts } = k;
  let complexity = 1;
  const callees = new Set<string>();
  const walk = (n: TS.Node): void => {
    if (k.decisions.has(n.kind) || (ts.isBinaryExpression(n) && k.logical.has(n.operatorToken.kind))) complexity++;
    if (ts.isCallExpression(n)) {
      if (ts.isIdentifier(n.expression)) callees.add(n.expression.text);
      else if (ts.isPropertyAccessExpression(n.expression)) callees.add(n.expression.name.text);
    }
    ts.forEachChild(n, walk);
  };
  ts.forEachChild(node, walk);
  return { complexity, callees: [...callees].sort() };
}

export const typescriptParser: Parser = {
  supports: isSourcePath,
  parse(file, text) {
    const k = load();
    const { ts } = k;
    const flags = (n: TS.Node): TS.ModifierFlags => ts.getCombinedModifierFlags(n as TS.Declaration);
    const isExported = (n: TS.Node): boolean => (flags(n) & ts.ModifierFlags.Export) !== 0;
    const sf = ts.createSourceFile(file, text, ts.ScriptTarget.ES2022, true, scriptKind(ts, file));
    const out: ParsedSymbol[] = [];
    const add = (name: string, kind: ParsedSymbol["kind"], node: TS.Node, signature: string, exported: boolean): void => {
      const tokens = tokensOf(ts, node);
      out.push({
        name,
        kind,
        file,
        startLine: sf.getLineAndCharacterOfPosition(node.getStart(sf)).line + 1,
        endLine: sf.getLineAndCharacterOfPosition(node.getEnd()).line + 1,
        exported,
        signature: signature.replace(/\s+/g, " "),
        astHash: createHash("sha256").update(tokens.join(" ")).digest("hex"),
        tokens,
        ...analyze(k, node),
        text: node.getText(sf).slice(0, TEXT_CAP),
      });
    };
    const sig = (f: TS.SignatureDeclaration): string =>
      `(${f.parameters.map((p) => p.getText(sf)).join(", ")})${f.type === undefined ? "" : `: ${f.type.getText(sf)}`}`;

    const visit = (node: TS.Node, cls: { name: string; exported: boolean } | null): void => {
      let inner = cls;
      if (ts.isFunctionDeclaration(node) && node.body !== undefined) {
        add(node.name?.text ?? "default", "function", node, sig(node), isExported(node));
      } else if (ts.isVariableDeclaration(node) && ts.isIdentifier(node.name) && node.initializer !== undefined &&
        (ts.isArrowFunction(node.initializer) || ts.isFunctionExpression(node.initializer))) {
        add(node.name.text, "arrow", node, sig(node.initializer), isExported(node));
      } else if (ts.isClassDeclaration(node)) {
        const name = node.name?.text ?? "default";
        add(name, "class", node, "", isExported(node));
        inner = { name, exported: isExported(node) };
      } else if (ts.isMethodDeclaration(node) && node.body !== undefined && cls !== null) {
        const priv = (flags(node) & ts.ModifierFlags.Private) !== 0;
        add(`${cls.name}.${node.name.getText(sf)}`, "method", node, sig(node), cls.exported && !priv);
      }
      ts.forEachChild(node, (child) => visit(child, inner));
    };
    visit(sf, null);
    return out;
  },
};
