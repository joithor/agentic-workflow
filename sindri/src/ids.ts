import { randomBytes } from "node:crypto";

// ULID (https://github.com/ulid/spec), lowercased so it fits snd-<ulid> names
// (spec §8.6: ^snd-[0-9a-z]{26}$). 48-bit ms timestamp + 80 random bits.
const ALPHABET = "0123456789abcdefghjkmnpqrstvwxyz";

export function ulid(now: Date = new Date()): string {
  let t = now.getTime();
  let time = "";
  for (let i = 0; i < 10; i++) {
    time = ALPHABET[t % 32] + time;
    t = Math.floor(t / 32);
  }
  const bytes = randomBytes(16);
  let rand = "";
  for (let i = 0; i < 16; i++) rand += ALPHABET[bytes[i] % 32];
  return time + rand;
}
