// Visible interactive elements (non-zero box, not hidden): DOM-order index, role, accessible name, test id, text.
(selector) => {
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
      accessibleName: el.getAttribute("aria-label") ?? (text || null) ?? el.getAttribute("title") ?? (el instanceof HTMLInputElement ? el.value || null : null) ?? text,
      testId: el.getAttribute("data-testid"),
      text,
    });
  });
  return out;
}
