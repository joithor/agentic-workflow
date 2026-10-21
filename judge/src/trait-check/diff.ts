// Unified diff -> per-file hunks -> sources sized for one Jev request. Traits are
// judged on what changed, not on whole files: a source is one or more hunks of
// one file, with their +/- markers kept so the question can ask about `+` lines.
export interface Hunk { start: number; end: number; text: string }
export interface FileDiff { path: string; hunks: Hunk[] }
export interface Source { path: string; startLine: number; endLine: number; text: string }

const HUNK_HEADER = /^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@/;

/** Parses `git diff --src-prefix=a/ --dst-prefix=b/` output. Deleted, binary and quoted-path files are skipped. */
export function parseDiff(diff: string): FileDiff[] {
  const files: FileDiff[] = [];
  let file: FileDiff | null = null;
  let hunk: { start: number; count: number; lines: string[] } | null = null;
  const closeHunk = () => {
    if (file !== null && hunk !== null) {
      file.hunks.push({ start: hunk.start, end: hunk.start + Math.max(hunk.count, 1) - 1, text: hunk.lines.join("\n") });
    }
    hunk = null;
  };
  for (const line of diff.split("\n")) {
    if (line.startsWith("diff --git ")) {
      closeHunk();
      file = null;
      continue;
    }
    if (hunk === null && line.startsWith("+++ ")) {
      const target = line.slice(4);
      file = target.startsWith("b/") ? { path: target.slice(2), hunks: [] } : null;
      if (file !== null) files.push(file);
      continue;
    }
    const header = HUNK_HEADER.exec(line);
    if (header !== null && file !== null) {
      closeHunk();
      hunk = { start: Number(header[1]), count: header[2] === undefined ? 1 : Number(header[2]), lines: [line] };
      continue;
    }
    if (hunk !== null && /^[ +\-\\]/.test(line)) (hunk as { lines: string[] }).lines.push(line);
  }
  closeHunk();
  return files.filter((f) => f.hunks.length > 0);
}

/** Groups a file's hunks into sources of at most maxChars, splitting an oversized hunk by lines. */
export function chunkFile(file: FileDiff, maxChars: number): Source[] {
  const pieces: Hunk[] = [];
  for (const hunk of file.hunks) {
    if (hunk.text.length <= maxChars) {
      pieces.push(hunk);
      continue;
    }
    let lines: string[] = [];
    let size = 0;
    for (const line of hunk.text.split("\n")) {
      if (lines.length > 0 && size + line.length + 1 > maxChars) {
        pieces.push({ start: hunk.start, end: hunk.end, text: lines.join("\n") });
        lines = [];
        size = 0;
      }
      lines.push(line.slice(0, maxChars));
      size += Math.min(line.length, maxChars) + 1;
    }
    pieces.push({ start: hunk.start, end: hunk.end, text: lines.join("\n") });
  }
  const sources: Source[] = [];
  for (const piece of pieces) {
    const last = sources[sources.length - 1];
    if (last !== undefined && last.text.length + piece.text.length + 1 <= maxChars) {
      last.text += `\n${piece.text}`;
      last.endLine = piece.end;
    } else sources.push({ path: file.path, startLine: piece.start, endLine: piece.end, text: piece.text });
  }
  return sources;
}

/** `// trait-ignore-file <name>: <reason>` in the file's leading comment block skips that trait for the file. */
export function ignoredTraits(head: string): Set<string> {
  const names = new Set<string>();
  for (const line of head.split("\n")) {
    const trimmed = line.trim();
    if (trimmed === "" || trimmed.startsWith("#!")) continue;
    if (!trimmed.startsWith("//")) break;
    const match = /^\/\/\s*trait-ignore-file\s+(\S+)\s*:\s*\S/.exec(trimmed);
    if (match !== null) names.add(match[1] as string);
  }
  return names;
}
