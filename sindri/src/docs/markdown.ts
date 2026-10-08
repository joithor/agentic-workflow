// A GitHub table cell: `|` ends the cell, and the sanitizer strips unknown tags
// such as <name>. Outside code spans escape both; inside one only `|`.
const TEXT: Record<string, string> = { "<": "&lt;", ">": "&gt;", "|": "\\|" };

export function cell(s: string): string {
  return s
    .split("`")
    .map((part, i) => (i % 2 === 1 ? part.replace(/\|/g, "\\|") : part.replace(/[<>|]/g, (c) => TEXT[c])))
    .join("`");
}
