// One definition of "invisible": controls, format characters, line and paragraph separators, and everything Unicode
// marks default-ignorable (zero-width, soft hyphen, bidi marks, tag characters, variation selectors, fillers).
// The adopt diff escapes these, the renderer and the privacy gate strip them.
const INVISIBLE = /[\p{Cc}\p{Cf}\p{Zl}\p{Zp}\p{Default_Ignorable_Code_Point}]/gu;

export const stripInvisible = (s: string, keep: ReadonlySet<string> = new Set()): string => s.replace(INVISIBLE, (c) => (keep.has(c) ? c : ""));

export const escapeInvisible = (s: string, keep: ReadonlySet<string> = new Set()): string =>
  s.replace(INVISIBLE, (c) => (keep.has(c) ? c : `\\u{${(c.codePointAt(0) as number).toString(16).toUpperCase().padStart(4, "0")}}`));

// Characters that separate words or lines; callers that must keep text apart keep these.
export const WHITESPACE: ReadonlySet<string> = new Set(["\t", "\n", "\u000B", "\u000C", "\r", "\u0085", "\u2028", "\u2029"]);
