// Turns results into the lines the tool prints.
export function format(results) {
  return results.map((r) => `${String(r.count).padStart(8)} ${r.file}`).join("\n");
}
