import { z } from "zod";

export const SIZES = ["XS", "S", "M", "L", "XL"] as const;
export type Size = (typeof SIZES)[number];

export const ItemRecordSchema = z.object({
  id: z.string().min(1),
  size: z.enum(SIZES).optional(),
  ambiguous: z.boolean().optional(),
  authorsTrusted: z.boolean().optional(),
});
export type ItemRecord = z.infer<typeof ItemRecordSchema>;

// Share of items the router could auto-start (spec §7.1, §13 step 0).
// Missing fields count as "not eligible": unknown is never treated as safe.
export function autoStartShare(items: readonly ItemRecord[], maxSize: Size | undefined): { eligible: number; total: number; share: number } {
  const limit = maxSize === undefined ? -1 : SIZES.indexOf(maxSize);
  const eligible = items.filter((i) => i.size !== undefined && SIZES.indexOf(i.size) <= limit && i.ambiguous === false && i.authorsTrusted === true).length;
  return { eligible, total: items.length, share: items.length === 0 ? 0 : eligible / items.length };
}
