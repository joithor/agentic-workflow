export async function api<T>(method: string, path: string, body?: unknown): Promise<T> {
  const res = await fetch(path, { method, body: body === undefined ? undefined : JSON.stringify(body) });
  if (!res.ok) throw new Error(`${method} ${path}: ${res.status}`);
  return res.json() as Promise<T>;
}
