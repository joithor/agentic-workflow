import { randomBytes } from "node:crypto";

// An opaque, unguessable link token: 24 random bytes, url-safe.
export function newToken(): string {
  return randomBytes(24).toString("base64url");
}
