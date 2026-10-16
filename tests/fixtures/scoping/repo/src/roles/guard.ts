import { atLeast, Role } from "./roles";

export interface Req {
  role?: Role;
  [field: string]: unknown;
}

export function requireRole<T>(need: Role, handler: (req: Req) => T) {
  return (req: Req) => {
    if (!req.role || !atLeast(req.role, need)) throw new Error("forbidden");
    return handler(req);
  };
}
