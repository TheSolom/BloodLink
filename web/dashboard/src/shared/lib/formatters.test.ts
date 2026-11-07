import { describe, expect, it } from "vitest";

import { formatDate } from "./formatters";

describe("formatDate", () => {
    it("formats a valid ISO date string correctly", () => {
        const result = formatDate("2026-10-31T10:00:00Z");
        expect(result).toBeTruthy();
        expect(result).toMatch(/2026/);
    });

    it("formats a Date instance correctly", () => {
        const date = new Date("2026-11-06T12:00:00Z");
        expect(formatDate(date)).toMatch(/2026/);
    });

    it("returns empty string for null, undefined, or empty input", () => {
        expect(formatDate(null)).toBe("");
        expect(formatDate(undefined)).toBe("");
        expect(formatDate("")).toBe("");
    });

    it("returns empty string for invalid date strings", () => {
        expect(formatDate("not-a-valid-date")).toBe("");
    });
});
