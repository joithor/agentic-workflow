// Repo-relative glob matching for trait `paths`: `**` spans directories, `*` and
// `?` stay within one path segment, `{a,b}` is an alternation. Enough for scoping
// traits without a glob dependency (engines >= 20 has no stable path.matchesGlob).
export function globToRegExp(glob: string): RegExp {
  let out = "";
  let depth = 0;
  for (let i = 0; i < glob.length; i++) {
    const c = glob[i] as string;
    if (c === "*" && glob[i + 1] === "*") {
      const slash = glob[i + 2] === "/";
      out += slash ? "(?:.*/)?" : ".*";
      i += slash ? 2 : 1;
    } else if (c === "*") out += "[^/]*";
    else if (c === "?") out += "[^/]";
    else if (c === "{") {
      out += "(?:";
      depth++;
    } else if (c === "}" && depth > 0) {
      out += ")";
      depth--;
    } else if (c === "," && depth > 0) out += "|";
    else out += c.replace(/[.+^$()|[\]\\{}]/g, "\\$&");
  }
  return new RegExp(`^${out}$`);
}

/** A trait without `paths` applies to every changed file. */
export function inScope(paths: readonly string[] | undefined, file: string): boolean {
  return paths === undefined || paths.some((glob) => globToRegExp(glob).test(file));
}
