#!/usr/bin/env node
// generate.mjs: writes the hostile-parse repo tree into a directory (CI12; spec sections 5.3, 9, 11).
//
//   node generate.mjs --out <dir> [--cap-kb 512]
//
// <dir> must not exist or must be empty (the script refuses to write into anything else). It gets:
//   static/*.cs       the small hand-written files of ./static, copied
//   generated/*.cs    the files a repo cannot reasonably commit (megabytes, invalid UTF-8, NUL bytes)
//   links/link.cs     a symlink to ../static/empty.cs (a symlink's absolute target would leak the path; this one is relative)
//   manifest.json     one row per file: path, size, and the outcome a parser run must reach (see README.md)
// Output is deterministic: two runs write the same bytes (the symlink aside). --cap-kb is the profile's index.maxFileKB
// (default 512): the over-cap file is cap+64 KB, the boundary file cap-1 KB.
// Exit: 0 written, 1 refused (non-empty out), 2 usage.
import { mkdirSync, writeFileSync, readdirSync, readFileSync, copyFileSync, symlinkSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const opt = (name, dflt) => { const i = args.indexOf(name); return i < 0 ? dflt : args[i + 1]; };
const out = opt('--out');
const capKb = Number(opt('--cap-kb', '512'));
if (!out || !Number.isInteger(capKb) || capKb < 8) { console.error('usage: node generate.mjs --out <dir> [--cap-kb 512]'); process.exit(2); }
if (existsSync(out) && readdirSync(out).length > 0) { console.error(`refused: ${out} is not empty`); process.exit(1); }

const rows = [];
const put = (rel, content, outcome, note) => {
  const path = join(out, rel);
  mkdirSync(dirname(path), { recursive: true });
  const buf = Buffer.isBuffer(content) ? content : Buffer.from(content, 'utf8');
  writeFileSync(path, buf);
  rows.push({ path: rel, size: buf.length, outcome, note });
};
const wrap = (body) => `namespace Hostile;\n\npublic class Generated\n{\n    public int Run()\n    {\n${body}\n    }\n}\n`;
const repeat = (s, n) => s.repeat(n);

// Static files: each must survive (no throw, no hang, process alive); symbols from the well-formed part are allowed but not required.
const survives = {
  'unterminated-string.cs': 'an unterminated string literal',
  'unterminated-comment.cs': 'an unterminated block comment',
  'unbalanced-braces.cs': 'missing closing braces',
  'unterminated-verbatim.cs': 'an unterminated verbatim string',
  'unterminated-directive.cs': 'an #if and a #region that never close',
  'garbage-tokens.cs': 'tokens that are not C#',
  'bom-mixed-endings.cs': 'a UTF-8 BOM, CRLF and a lone CR, cut off mid-class',
  'unicode-identifiers.cs': 'non-ASCII identifiers, a bidi override and zero-width characters',
  'empty.cs': 'a zero-byte file; parsed with no symbols',
  'only-comments.cs': 'comments only; parsed with no symbols',
  'only-usings.cs': 'usings only; parsed with no symbols',
  'self-recursive.cs': 'overloads that call each other and themselves; symbols and edges, no loop',
};
for (const name of readdirSync(join(here, 'static')).sort()) {
  if (!(name in survives)) throw new Error(`static/${name} has no manifest entry`);
  put(`static/${name}`, readFileSync(join(here, 'static', name)), 'survives', survives[name]);
}

// Depth (CI12): the three the spec names must end parsed = 0, too-deep; the process lives.
put('generated/plus-terms-100000.cs', wrap(`        return ${Array.from({ length: 100000 }, (_, i) => `a${i % 10}`).join(' + ')};`), 'too-deep', '100,000-term a+b+...');
put('generated/deep-parens-20000.cs', wrap(`        return ${repeat('(', 20000)}1${repeat(')', 20000)};`), 'too-deep', '20,000 nested parentheses');
put('generated/deep-generics-5000.cs', wrap(`        ${repeat('List<', 5000)}int${repeat('>', 5000)} x = null;\n        return 0;`), 'too-deep', '5,000 nested generic type arguments');
// Other deep shapes: a guard may refuse them (too-deep) or the parser may cope; either way bounded.
put('generated/deep-blocks-5000.cs', wrap(`${repeat('{', 5000)}${repeat('}', 5000)}\n        return 0;`), 'bounded', '5,000 nested blocks');
put('generated/deep-lambdas-3000.cs', wrap(`        var f = ${repeat('x => ', 3000)}x;\n        return 0;`), 'bounded', '3,000 curried lambdas');
put('generated/deep-ternary-5000.cs', wrap(`        return ${repeat('a ? 1 : ', 5000)}0;`), 'bounded', '5,000 chained conditional expressions');
put('generated/deep-initializers-5000.cs', wrap(`        var x = ${repeat('new[] { ', 5000)}1${repeat(' }', 5000)};\n        return 0;`), 'bounded', '5,000 nested array initializers');
put('generated/deep-member-chain-20000.cs', wrap(`        return a${repeat('.b()', 20000)};`), 'bounded', 'a 20,000-call chain: also 20,000 call sites against the 20,000 per file ceiling, so it may end over-ceiling');
put('generated/string-concat-50000.cs', wrap(`        return ${Array.from({ length: 50000 }, () => '"s"').join(' + ')}.Length;`), 'bounded', '50,000 concatenated literals');

// Width: one huge line and one huge identifier, under the size cap.
put('generated/huge-line.cs', `namespace Hostile;\n\npublic class HugeLine\n{\n    // ${repeat('x', (capKb - 8) * 1024)}\n    public int After() => 1;\n}\n`, 'survives', `one comment line of ${capKb - 8} KB, then a method (under the cap: parsed, symbol After found)`);
put('generated/long-identifier.cs', `namespace Hostile;\n\npublic class LongName\n{\n    public int ${repeat('n', 100000)}() => 1;\n}\n`, 'survives', 'a 100,000-character method name');
put('generated/at-cap-boundary.cs', `namespace Hostile;\n\npublic class Boundary\n{\n    // ${repeat('b', (capKb - 1) * 1024 - 100)}\n    public int Inside() => 1;\n}\n`, 'survives', `just under ${capKb} KB: kept and parsed`);

// Caps (spec 5.1): over the file size cap is skipped before parsing; over the per-file ceilings fails that file with a count.
put('generated/over-cap.cs', `namespace Hostile;\n\npublic class OverCap\n{\n    // ${repeat('o', (capKb + 64) * 1024)}\n    public int A() => 1;\n}\n`, 'skipped:too-large', `${capKb + 64} KB, over index.maxFileKB ${capKb}`);
put('generated/symbols-6000.cs', `namespace Hostile;\n\npublic class ManySymbols\n{\n${Array.from({ length: 6000 }, (_, i) => `    public int M${i}() => ${i};`).join('\n')}\n}\n`, 'over-ceiling:symbols', '6,000 methods against the 5,000 symbols per file ceiling');
put('generated/call-sites-25000.cs', wrap(Array.from({ length: 25000 }, () => '        Run();').join('\n')), 'over-ceiling:call-sites', '25,000 calls in one method against the 20,000 per file ceiling');

// Bytes: not valid UTF-8 text.
put('generated/invalid-utf8.cs', Buffer.concat([Buffer.from('namespace Hostile;\n\npublic class Bytes\n{\n    public int A() => 1; // '), Buffer.from([0xff, 0xfe, 0xc0, 0xaf, 0xed, 0xa0, 0x80, 0xf8, 0x88, 0x80, 0x80, 0x80]), Buffer.from('\n    public int B() => 2;\n}\n')]), 'survives', 'invalid UTF-8 sequences (overlong, surrogate, 5-byte) inside a comment; decoded with replacement');
put('generated/nul-bytes.cs', Buffer.concat([Buffer.from('namespace Hostile;\n\npublic class Nul\n{\n    public int A() => 1;\n'), Buffer.alloc(64), Buffer.from('    public int B() => 2;\n}\n')]), 'survives', 'NUL bytes between members; no truncation of what follows needs to be promised');
put('generated/utf16-le-bom.cs', Buffer.concat([Buffer.from([0xff, 0xfe]), Buffer.from('namespace Hostile;\npublic class Wide { public int A() => 1; }\n', 'utf16le')]), 'survives', 'a UTF-16 LE file with a BOM');

// A symlink: the snapshot skips mode 120000 entries (spec 5.1); on disk this one points to a sibling file.
mkdirSync(join(out, 'links'), { recursive: true });
symlinkSync('../static/empty.cs', join(out, 'links/link.cs'));
rows.push({ path: 'links/link.cs', size: 0, outcome: 'skipped:symlink', note: 'a relative symlink; a git tree holds it as mode 120000' });

writeFileSync(join(out, 'manifest.json'), JSON.stringify({ capKb, files: rows.sort((a, b) => a.path.localeCompare(b.path)) }, null, 2) + '\n');
console.log(`wrote ${rows.length} files to ${out}`);
