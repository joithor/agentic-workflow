// Visible interactive elements (non-zero box, not hidden): DOM-order index, role, accessible name, test id, text.
(selector) => {
  const named = (v) => (v && v.trim()) || null;
  const out = [];
  Array.from(document.querySelectorAll(selector)).forEach((el, index) => {
    const rect = el.getBoundingClientRect();
    const style = getComputedStyle(el);
    if (rect.width === 0 || rect.height === 0 || style.visibility === "hidden" || style.display === "none") return;
    const text = el.textContent?.trim() ?? null;
    out.push({
      index,
      role: el.getAttribute("role") ?? el.tagName.toLowerCase(),
      // A submit input or an icon button has no text: its value or title is what it says (the repair write check reads this).
      // An empty aria-label names nothing, and only a button-like input's value is a label: a text or password field's
      // value is what was typed into it, which must never reach the judge.
      // A blank aria-label, text or title names nothing either: each source counts only when it has non-space text.
      accessibleName: named(el.getAttribute("aria-label")) ?? named(text) ?? named(el.getAttribute("title"))
        ?? (el instanceof HTMLInputElement && ["submit", "button", "reset"].includes(el.type) ? named(el.value) : null) ?? text,
      testId: el.getAttribute("data-testid"),
      text,
    });
  });
  return out;
}
