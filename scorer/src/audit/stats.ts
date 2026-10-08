export interface Interval {
  rate: number;
  lower: number;
  upper: number;
}

const Z = 1.96; // 95%

// Wilson score interval for k successes in n trials. n = 0 is the uninformative interval.
export function wilson(k: number, n: number): Interval {
  if (!Number.isFinite(k) || !Number.isFinite(n) || k < 0 || n < 0 || k > n) throw new RangeError(`wilson(${k}, ${n}): need 0 <= k <= n`);
  if (n === 0) return { rate: 0, lower: 0, upper: 1 };
  const p = k / n;
  const z2 = Z * Z;
  const denom = 1 + z2 / n;
  const center = (p + z2 / (2 * n)) / denom;
  const half = (Z * Math.sqrt((p * (1 - p)) / n + z2 / (4 * n * n))) / denom;
  return { rate: p, lower: Math.max(0, center - half), upper: Math.min(1, center + half) };
}
