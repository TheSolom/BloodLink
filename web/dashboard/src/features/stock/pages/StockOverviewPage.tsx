import { Droplet, RefreshCw } from "lucide-react";
import * as React from "react";

import { Button } from "@/shared/ui/button";

import { useAvailableStock } from "../api/queries";
import { StockEmptyState } from "../components/StockEmptyState";
import { StockErrorAlert } from "../components/StockErrorAlert";
import { StockFilters } from "../components/StockFilters";
import { StockLoadingSkeleton } from "../components/StockLoadingSkeleton";
import { StockPagination } from "../components/StockPagination";
import { StockTable } from "../components/StockTable";
import { useStockFilters } from "../hooks/useStockFilters";

export function StockOverviewPage() {
    const {
        bloodType,
        component,
        page,
        pageSize,
        queryParams,
        setBloodType,
        setComponent,
        setPage,
    } = useStockFilters();

    const { data, isLoading, isError, error, refetch, isFetching } = useAvailableStock(queryParams);

    const totalCount = Number(data?.totalCount ?? 0);
    const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

    let content: React.ReactNode;
    if (isLoading) {
        content = <StockLoadingSkeleton />;
    } else if (isError) {
        content = <StockErrorAlert error={error} onRetry={() => refetch()} />;
    } else if (!data || data.items.length === 0) {
        content = <StockEmptyState />;
    } else {
        content = (
            <div className="space-y-4">
                <StockTable items={data.items} />
                <StockPagination
                    page={page}
                    totalPages={totalPages}
                    totalCount={totalCount}
                    itemsCount={data.items.length}
                    onPageChange={setPage}
                />
            </div>
        );
    }

    return (
        <div className="container mx-auto p-6 max-w-6xl space-y-6">
            <header className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 pb-6 border-b border-neutral-200">
                <div>
                    <h1 className="text-2xl font-bold tracking-tight text-neutral-900 flex items-center gap-2">
                        <Droplet className="h-6 w-6 text-red-600 fill-red-600" />
                        Blood Stock Overview
                    </h1>
                    <p className="text-sm text-neutral-500 mt-1">
                        Real-time monitoring of available, non-expired blood units across
                        facilities.
                    </p>
                </div>
                <Button
                    variant="outline"
                    size="sm"
                    onClick={() => refetch()}
                    disabled={isFetching}
                    className="flex items-center gap-2"
                    aria-label="Refresh stock data"
                >
                    <RefreshCw className={`h-4 w-4 ${isFetching ? "animate-spin" : ""}`} />
                    Refresh
                </Button>
            </header>

            <StockFilters
                bloodType={bloodType}
                component={component}
                onBloodTypeChange={setBloodType}
                onComponentChange={setComponent}
            />

            {/* Content States */}
            {content}
        </div>
    );
}
