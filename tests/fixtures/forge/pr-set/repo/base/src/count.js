// Counts one thing in a text.
export function count(text, mode) {
  if (mode === "lines") return text === "" ? 0 : text.split("\n").length;
  if (mode === "chars") return text.length;
  return text.split(/\s+/).filter(Boolean).length;
}
