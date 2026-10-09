import { describe, expect, it } from "vitest";

import { isoWeek, isoWeekMonday } from "../src/evolve/week.js";

describe("ISO weeks (UTC)", () => {
  it("numbers weeks and finds their Monday, across a Sunday and a year boundary", () => {
    expect(isoWeek(new Date("2026-10-08T12:00:00Z"))).toEqual({ year: 2026, week: 41 });
    expect(isoWeekMonday(new Date("2026-10-08T12:00:00Z"))).toBe("2026-10-05");
    expect(isoWeek(new Date("2026-10-11T23:00:00Z"))).toEqual({ year: 2026, week: 41 }); // a Sunday
    expect(isoWeekMonday(new Date("2026-10-11T23:00:00Z"))).toBe("2026-10-05");
    expect(isoWeek(new Date("2026-10-05T00:00:00Z"))).toEqual({ year: 2026, week: 41 }); // the Monday itself
    expect(isoWeekMonday(new Date("2026-10-05T00:00:00Z"))).toBe("2026-10-05");
    expect(isoWeek(new Date("2027-01-01T00:00:00Z"))).toEqual({ year: 2026, week: 53 });
    expect(isoWeekMonday(new Date("2027-01-01T00:00:00Z"))).toBe("2026-12-28");
  });
});
