// Path globs for index.denyPaths and index.utilityGlobs. Matching is case-insensitive.
// Paths are repo-relative with "/" separators. "**/" = zero or more directories; trailing "**" = anything;
// "*" = anything inside one segment; "?" = one character.
export function globToRegExp(glob: string): RegExp {
  let re = "";
  for (let i = 0; i < glob.length; i++) {
    const c = glob[i];
    if (c === "*" && glob[i + 1] === "*") {
      if (glob[i + 2] === "/") {
        re += "(?:.*/)?";
        i += 2;
      } else {
        re += ".*";
        i += 1;
      }
    } else if (c === "*") {
      re += "[^/]*";
    } else if (c === "?") {
      re += "[^/]";
    } else {
      re += c.replace(/[.+^${}()|[\]\\]/g, "\\$&");
    }
  }
  return new RegExp(`^${re}$`, "is");
}

// Each glob is compiled once per process: matchesAny runs per tracked file and per staged path.
const compiled = new Map<string, RegExp>();

export function compiledGlob(glob: string): RegExp {
  let re = compiled.get(glob);
  if (re === undefined) {
    re = globToRegExp(glob);
    compiled.set(glob, re);
  }
  return re;
}

export function matchesAny(p: string, globs: readonly string[]): boolean {
  return globs.some((g) => compiledGlob(g).test(p));
}
