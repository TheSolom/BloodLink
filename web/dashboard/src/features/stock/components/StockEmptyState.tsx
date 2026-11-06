import { Droplet } from "lucide-react";

export interface StockEmptyStateProps {
    title?: string;
    description?: string;
    ariaLabel?: string;
}

export function StockEmptyState({
    title = "No blood units available",
    description = "There are currently no active, non-expired blood units matching the selected criteria.",
    ariaLabel = "No stock available",
}: Readonly<StockEmptyStateProps>) {
    return (
        <output
            aria-label={ariaLabel}
            className="block text-center py-12 px-4 rounded-lg border border-dashed border-neutral-300 bg-neutral-50/50"
        >
            <Droplet className="h-10 w-10 text-neutral-400 mx-auto mb-3" />
            <h2 className="text-lg font-semibold text-neutral-700">{title}</h2>
            <p className="text-sm text-neutral-500 max-w-sm mx-auto mt-1">{description}</p>
        </output>
    );
}
