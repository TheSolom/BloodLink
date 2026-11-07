import { CircleAlert } from "lucide-react";

import { Button } from "@/shared/ui/button";

export interface StockErrorAlertProps {
    error: unknown;
    onRetry?: () => void;
    title?: string;
}

export function StockErrorAlert({
    error,
    onRetry,
    title = "Failed to load blood stock",
}: Readonly<StockErrorAlertProps>) {
    const message = error instanceof Error ? error.message : "An unexpected error occurred.";

    return (
        <div
            role="alert"
            className="flex items-center gap-3 p-4 rounded-lg bg-red-50 border border-red-200 text-red-800"
        >
            <CircleAlert className="h-5 w-5 shrink-0 text-red-600" />
            <div>
                <h2 className="font-semibold text-sm">{title}</h2>
                <p className="text-sm mt-0.5">{message}</p>
            </div>
            {onRetry && (
                <Button
                    variant="outline"
                    size="sm"
                    onClick={onRetry}
                    className="ml-auto bg-white border-red-300 text-red-700 hover:bg-red-50"
                >
                    Retry
                </Button>
            )}
        </div>
    );
}
