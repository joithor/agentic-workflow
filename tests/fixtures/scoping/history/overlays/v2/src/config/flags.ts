// Feature flags, read once at start from FLAGS (comma separated names).
const on = new Set((process.env.FLAGS ?? "").split(",").filter(Boolean));

export const flags = {
  auditReport: on.has("auditReport"),
  digest: !on.has("noDigest"),
};
