# Accessibility lens

Read the diff for interface changes a person using a keyboard, a screen reader or a zoomed screen cannot use.

Look for:
- Controls that are not reachable or operable by keyboard; a focus order that does not match the visual order; focus lost after a change.
- Missing or wrong labels, roles, names and states on controls and images; heading order that skips levels.
- Contrast that is too low, information carried by colour alone, text that does not scale.
- Motion with no way to reduce it; content that appears with no announcement.
- Error messages that are not tied to their field.

Severities:
- blocking: a main flow cannot be completed without a mouse or without sight.
- issue: an unlabelled control, a keyboard trap, or a state that is not announced, on a changed path.
- suggestion: a better role, label or order.
- nit: wording of a label.
