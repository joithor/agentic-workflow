// The lint snapshot: every button, link, input, role=button and test-id element with its box, text and computed style,
// and whether a readable style sheet gives it (or an ancestor) a :hover and a :focus rule. A sheet the page can't read
// (cross-origin) makes both unknown, which counts as present: the rule must never fire on what it can't see.
() => {
  const hover = [];
  const focus = [];
  let opaque = false;
  const walk = (rules) => {
    for (const rule of rules) {
      if (rule.cssRules) walk(Array.from(rule.cssRules));
      if (!rule.selectorText) continue;
      for (const part of rule.selectorText.split(",")) {
        if (part.includes(":hover")) hover.push(part.replace(/:hover/g, "").trim() || "*");
        if (/:focus(-visible|-within)?/.test(part)) focus.push(part.replace(/:focus(-visible|-within)?/g, "").trim() || "*");
      }
    }
  };
  for (const sheet of Array.from(document.styleSheets)) {
    try { walk(Array.from(sheet.cssRules)); } catch { opaque = true; }
  }
  const styled = (el, selectors) => opaque || selectors.some((s) => { try { return !!el.closest(s); } catch { return false; } });
  return Array.from(document.querySelectorAll("button, a, input, [role='button'], [data-testid]")).map((el) => {
    const rect = el.getBoundingClientRect();
    const style = getComputedStyle(el);
    return {
      selector: el.tagName.toLowerCase() + (el.id ? "#" + el.id : ""),
      x: rect.x, y: rect.y, w: rect.width, h: rect.height,
      text: el.textContent?.trim() ?? "",
      textOverflow: style.textOverflow, overflow: style.overflow, color: style.color,
      hasHoverState: styled(el, hover),
      hasFocusState: styled(el, focus),
    };
  });
}
