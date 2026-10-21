// Repo-relative glob matching for trait `paths`: `**` spans directories, `*` and
// `?` stay within one path segment, `{a,b}` is an alternation. Enough for scoping
// traits without a glob dependency (engines >= 20 has no stable path.matchesGlob).
// An unmatched `{` or `}` is a literal character, so no glob makes an invalid RegExp.
export function globToRegExp(glob: string): RegExp {
  const groups = braceGroups(glob);
  let out = "";
  for (let i = 0; i < glob.length; i++) {
    const c = glob[i] as string;
    if (c === "*" && glob[i + 1] === "*") {
      const slash = glob[i + 2] === "/";
      out += slash ? "(?:.*/)?" : ".*";
      i += slash ? 2 : 1;
    } else if (c === "*") out += "[^/]*";
    else if (c === "?") out += "[^/]";
    else if (groups.open.has(i)) out += "(?:";
    else if (groups.close.has(i)) out += ")";
    else if (c === "," && groups.inside.has(i)) out += "|";
    else out += c.replace(/[.+^$()|[\]\\{}]/g, "\\$&");
  }
  return new RegExp(`^${out}$`);
}

/** Positions of matched braces, and of the commas that sit inside a matched pair. */
function braceGroups(glob: string): { open: Set<number>; close: Set<number>; inside: Set<number> } {
  const open = new Set<number>();
  const close = new Set<number>();
  const inside = new Set<number>();
  const stack: number[][] = [];
  for (let i = 0; i < glob.length; i++) {
    if (glob[i] === "{") stack.push([i]);
    else if (glob[i] === "," && stack.length > 0) (stack[stack.length - 1] as number[]).push(i);
    else if (glob[i] === "}" && stack.length > 0) {
      const [start, ...commas] = stack.pop() as number[];
      open.add(start as number);
      close.add(i);
      for (const comma of commas) inside.add(comma);
    }
  }
  return { open, close, inside };
}

/** A `{` with no matching `}` is almost certainly a typo in a trait's `paths`. */
export function hasUnclosedBrace(glob: string): boolean {
  return braceGroups(glob).open.size < glob.split("{").length - 1;
}

/** A trait without `paths` applies to every changed file. */
export function inScope(paths: readonly string[] | undefined, file: string): boolean {
  return paths === undefined || paths.some((glob) => globToRegExp(glob).test(file));
}
