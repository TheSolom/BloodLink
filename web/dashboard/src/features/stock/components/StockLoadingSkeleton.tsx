import { Skeleton } from "@/shared/ui/skeleton";

export interface StockLoadingSkeletonProps {
    ariaLabel?: string;
}

export function StockLoadingSkeleton({
    ariaLabel = "Loading stock units",
}: Readonly<StockLoadingSkeletonProps>) {
    return (
        <output aria-label={ariaLabel} className="block space-y-3">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-12 w-full" />
            <Skeleton className="h-12 w-full" />
            <Skeleton className="h-12 w-full" />
            <span className="sr-only">Loading stock data...</span>
        </output>
    );
}
