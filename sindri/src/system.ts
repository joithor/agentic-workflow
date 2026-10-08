// Host facts the lock and doctor need. Real implementation: system-real.ts.
export interface SystemProbe {
  readonly platform: NodeJS.Platform;
  readonly pid: number;
  hostname(): string;
  bootId(): string | null;
  pidAlive(pid: number): boolean;
  pidStartTime(pid: number): string | null;
  isLocalDisk(p: string): boolean | null;
  username(): string;
}

// /proc/<pid>/stat: the command name (field 2) is in parens and may contain
// spaces or parens, so split after the last ')'. starttime is field 22.
export function parseLinuxStartTime(stat: string): string | null {
  const close = stat.lastIndexOf(")");
  if (close < 0) return null;
  const fields = stat.slice(close + 2).split(" ");
  return fields.length > 19 ? fields[19] : null;
}

// macOS: `df -P <path>` names the mount point (last column of line 2);
// `mount` lists "<dev> on <mountpoint> (<type>, local, ...)".
export function parseDarwinLocal(dfOut: string, mountOut: string): boolean | null {
  const row = dfOut.trim().split("\n")[1];
  if (row === undefined) return null;
  const mountPoint = row.trim().split(/\s+/).at(-1) as string;
  const line = mountOut.split("\n").find((l) => l.includes(` on ${mountPoint} (`));
  if (line === undefined) return null;
  return /[(,]\s*local\s*[,)]/.test(line);
}

const REMOTE_FS = new Set(["nfs", "nfs4", "cifs", "smb2", "smbfs", "fuse.sshfs", "9p", "afs"]);

export function isRemoteFsType(type: string): boolean {
  return REMOTE_FS.has(type.trim());
}
