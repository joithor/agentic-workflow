export type Role = "owner" | "editor" | "viewer";

const rank: Record<Role, number> = { viewer: 1, editor: 2, owner: 3 };

export function atLeast(have: Role, need: Role): boolean {
  return rank[have] >= rank[need];
}
