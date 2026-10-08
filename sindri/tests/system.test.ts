import { describe, expect, it } from "vitest";

import { isRemoteFsType, parseDarwinLocal, parseLinuxStartTime } from "../src/system.js";

describe("system parsers", () => {
  it("reads field 22 of /proc/<pid>/stat, even when the command name has spaces and parens", () => {
    const stat = "1234 (my (odd) proc) S 1 1234 1234 0 -1 4194560 100 0 0 0 1 2 0 0 20 0 1 0 987654 1000 10";
    expect(parseLinuxStartTime(stat)).toBe("987654");
    expect(parseLinuxStartTime("garbage")).toBeNull();
    expect(parseLinuxStartTime("1 (short) S 1")).toBeNull();
  });

  it("finds the mount for a df -P line and checks the local flag", () => {
    const df = "Filesystem 512-blocks Used Available Capacity Mounted on\n/dev/disk3s5 1 1 1 1% /System/Volumes/Data\n";
    const mounts = "/dev/disk3s5 on /System/Volumes/Data (apfs, local, journaled)\nsrv:/x on /Volumes/x (nfs, nodev)\n";
    expect(parseDarwinLocal(df, mounts)).toBe(true);
    const nfsDf = "Filesystem 512-blocks Used Available Capacity Mounted on\nsrv:/x 1 1 1 1% /Volumes/x\n";
    expect(parseDarwinLocal(nfsDf, mounts)).toBe(false);
    expect(parseDarwinLocal("header only\n", mounts)).toBeNull();
    expect(parseDarwinLocal(df, "")).toBeNull();
  });

  it("classifies network filesystem types", () => {
    for (const t of ["nfs", "nfs4", "cifs", "smb2", "smbfs", "fuse.sshfs", "9p", "afs"]) expect(isRemoteFsType(t)).toBe(true);
    for (const t of ["ext2/ext3", "xfs", "btrfs", "apfs", "tmpfs"]) expect(isRemoteFsType(t)).toBe(false);
  });
});
