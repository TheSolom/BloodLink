import { act, renderHook } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { useStockFilters } from "./useStockFilters";

describe("useStockFilters", () => {
    it("initializes with default values", () => {
        const { result } = renderHook(() => useStockFilters());

        expect(result.current.bloodType).toBe("ALL");
        expect(result.current.component).toBe("ALL");
        expect(result.current.page).toBe(1);
        expect(result.current.pageSize).toBe(10);
        expect(result.current.queryParams).toEqual({
            bloodType: undefined,
            component: undefined,
            page: 1,
            pageSize: 10,
        });
    });

    it("resets page to 1 when changing bloodType", () => {
        const { result } = renderHook(() => useStockFilters());

        act(() => {
            result.current.setPage(3);
        });
        expect(result.current.page).toBe(3);

        act(() => {
            result.current.setBloodType("O+");
        });
        expect(result.current.bloodType).toBe("O+");
        expect(result.current.page).toBe(1);
        expect(result.current.queryParams.bloodType).toBe("O+");
    });

    it("resets page to 1 when changing component", () => {
        const { result } = renderHook(() => useStockFilters());

        act(() => {
            result.current.setPage(4);
        });
        expect(result.current.page).toBe(4);

        act(() => {
            result.current.setComponent("RedCells");
        });
        expect(result.current.component).toBe("RedCells");
        expect(result.current.page).toBe(1);
        expect(result.current.queryParams.component).toBe("RedCells");
    });

    it("resets all filters and page to default with resetFilters", () => {
        const { result } = renderHook(() => useStockFilters());

        act(() => {
            result.current.setBloodType("A-");
            result.current.setComponent("Platelets");
            result.current.setPage(2);
        });

        expect(result.current.bloodType).toBe("A-");
        expect(result.current.component).toBe("Platelets");
        expect(result.current.page).toBe(2);

        act(() => {
            result.current.resetFilters();
        });

        expect(result.current.bloodType).toBe("ALL");
        expect(result.current.component).toBe("ALL");
        expect(result.current.page).toBe(1);
    });
});
