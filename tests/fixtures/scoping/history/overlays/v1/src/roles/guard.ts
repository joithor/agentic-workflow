import { atLeast, Role } from "./roles";

export function requireRole<T>(need: Role, handler: (req: any) => T) {
  return (req: any) => {
    if (!atLeast(req.role, need)) throw new Error("forbidden");
    return handler(req);
  };
}
