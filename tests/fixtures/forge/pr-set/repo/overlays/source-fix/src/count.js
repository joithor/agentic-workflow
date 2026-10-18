// Counts one thing in a text.
export function count(text, mode) {
  if (text === "") return 0;
  if (mode === "lines") return text.split("\n").length;
  if (mode === "chars") return text.length;
  return text.split(/\s+/).filter(Boolean).length;
}
