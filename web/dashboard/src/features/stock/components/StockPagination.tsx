import { Button } from "@/shared/ui/button";

export interface StockPaginationProps {
    page: number;
    totalPages: number;
    totalCount: number;
    itemsCount: number;
    onPageChange: (newPage: number) => void;
}

export function StockPagination({
    page,
    totalPages,
    totalCount,
    itemsCount,
    onPageChange,
}: Readonly<StockPaginationProps>) {
    return (
        <div className="flex items-center justify-between text-sm text-neutral-600 px-2 py-1">
            <div>
                Showing {itemsCount} of {totalCount} units
            </div>
            <div className="flex items-center gap-2">
                <Button
                    variant="outline"
                    size="sm"
                    onClick={() => onPageChange(Math.max(1, page - 1))}
                    disabled={page <= 1}
                    aria-label="Previous page"
                >
                    Previous
                </Button>
                <span className="text-sm font-medium">
                    Page {page} of {totalPages}
                </span>
                <Button
                    variant="outline"
                    size="sm"
                    onClick={() => onPageChange(Math.min(totalPages, page + 1))}
                    disabled={page >= totalPages}
                    aria-label="Next page"
                >
                    Next
                </Button>
            </div>
        </div>
    );
}
