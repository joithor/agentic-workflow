// Spec §6.2 offline guarantee: the embedding endpoint must be this machine. Only the
// IP literals count: `localhost` goes through the system resolver, which can be overridden.
export function isLoopbackUrl(url: string): boolean {
  let u: URL;
  try {
    u = new URL(url);
  } catch {
    return false;
  }
  if (u.protocol !== "http:" && u.protocol !== "https:") return false;
  if (u.username !== "" || u.password !== "") return false;
  return u.hostname === "127.0.0.1" || u.hostname === "[::1]";
}
