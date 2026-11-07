import * as React from "react";

import type { StockQueryParams } from "@/shared/api/client";

import {
    BLOOD_COMPONENTS,
    BLOOD_TYPES,
    BloodTypeFilter,
    ComponentFilter,
    DEFAULT_PAGE_SIZE,
} from "../constants/stock.constants";

export interface UseStockFiltersOptions {
    defaultPageSize?: number;
    initialBloodType?: BloodTypeFilter;
    initialComponent?: ComponentFilter;
    initialPage?: number;
}

export function useStockFilters({
    defaultPageSize = DEFAULT_PAGE_SIZE,
    initialBloodType = "ALL",
    initialComponent = "ALL",
    initialPage = 1,
}: UseStockFiltersOptions = {}) {
    const [bloodType, setBloodTypeState] = React.useState<string>(initialBloodType);
    const [component, setComponentState] = React.useState<string>(initialComponent);
    const [page, setPage] = React.useState<number>(initialPage);

    const queryParams: StockQueryParams = React.useMemo(
        () => ({
            bloodType: bloodType === "ALL" ? undefined : bloodType,
            component: component === "ALL" ? undefined : component,
            page,
            pageSize: defaultPageSize,
        }),
        [bloodType, component, page, defaultPageSize],
    );

    const setBloodType = React.useCallback((value: string) => {
        setBloodTypeState(value);
        setPage(1);
    }, []);

    const setComponent = React.useCallback((value: string) => {
        setComponentState(value);
        setPage(1);
    }, []);

    const resetFilters = React.useCallback(() => {
        setBloodTypeState("ALL");
        setComponentState("ALL");
        setPage(1);
    }, []);

    return {
        bloodType,
        component,
        page,
        pageSize: defaultPageSize,
        queryParams,
        setBloodType,
        setComponent,
        setPage,
        resetFilters,
        availableBloodTypes: BLOOD_TYPES,
        availableComponents: BLOOD_COMPONENTS,
    };
}
