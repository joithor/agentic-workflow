export function ExportScreen() {
  return {
    title: "Export notes",
    choices: [{ label: "Markdown", href: "/api/export?format=markdown" }, { label: "JSON", href: "/api/export?format=json" }],
  };
}
