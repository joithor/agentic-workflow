import { api } from "../api-client";

// A member's switch for the weekly digest email.
export function DigestSettings(subscribed: boolean) {
  return {
    title: "Email digest",
    toggle: { label: "Send me the weekly digest", on: subscribed, run: (on: boolean) => api("PUT", "/api/me/digest", { on }) },
  };
}
