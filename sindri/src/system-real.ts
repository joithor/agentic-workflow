import { execFileSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";

import { isRemoteFsType, parseDarwinLocal, parseLinuxStartTime, type SystemProbe } from "./system.js";

function run(cmd: string, args: string[]): string | null {
  try {
    // C locale and UTC: `ps -o lstart=` must print the same string for every caller.
    return execFileSync(cmd, args, { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"], timeout: 5000, env: { ...process.env, LC_ALL: "C", TZ: "UTC" } });
  } catch {
    return null;
  }
}

export function realSystemProbe(): SystemProbe {
  const linux = process.platform === "linux";
  return {
    platform: process.platform,
    pid: process.pid,
    hostname: () => os.hostname(),
    bootId: () => {
      if (linux) {
        try {
          return fs.readFileSync("/proc/sys/kernel/random/boot_id", "utf8").trim() || null;
        } catch {
          return null;
        }
      }
      return run("sysctl", ["-n", "kern.bootsessionuuid"])?.trim() || null;
    },
    pidAlive: (pid) => {
      try {
        process.kill(pid, 0);
        return true;
      } catch (e) {
        return (e as NodeJS.ErrnoException).code === "EPERM";
      }
    },
    pidStartTime: (pid) => {
      if (linux) {
        try {
          return parseLinuxStartTime(fs.readFileSync(`/proc/${pid}/stat`, "utf8"));
        } catch {
          return null;
        }
      }
      return run("ps", ["-o", "lstart=", "-p", String(pid)])?.trim() || null;
    },
    isLocalDisk: (p) => {
      if (linux) {
        const type = run("stat", ["-f", "-c", "%T", p]);
        return type === null ? null : !isRemoteFsType(type);
      }
      const df = run("df", ["-P", p]);
      const mounts = run("mount", []);
      return df === null || mounts === null ? null : parseDarwinLocal(df, mounts);
    },
    username: () => os.userInfo().username,
  };
}
