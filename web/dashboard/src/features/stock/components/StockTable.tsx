import type { StockItem } from "@/shared/api/client";
import { formatDate } from "@/shared/lib/formatters";
import { Badge } from "@/shared/ui/badge";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/shared/ui/table";

export interface StockTableProps {
    items: StockItem[];
    ariaLabel?: string;
}

export function StockTable({
    items,
    ariaLabel = "Available Blood Stock",
}: Readonly<StockTableProps>) {
    return (
        <div className="rounded-md border border-neutral-200 overflow-hidden bg-white shadow-sm">
            <Table aria-label={ariaLabel}>
                <TableHeader>
                    <TableRow>
                        <TableHead className="w-45">Donation Number</TableHead>
                        <TableHead>Blood Type</TableHead>
                        <TableHead>Component</TableHead>
                        <TableHead>Status</TableHead>
                        <TableHead>Collected Date</TableHead>
                        <TableHead>Expiry Date</TableHead>
                    </TableRow>
                </TableHeader>
                <TableBody>
                    {items.map((item) => (
                        <TableRow key={item.id} data-testid={`stock-row-${item.id}`}>
                            <TableCell className="font-mono font-medium text-neutral-900">
                                {item.donationNumber}
                            </TableCell>
                            <TableCell>
                                <Badge
                                    variant="outline"
                                    className="font-bold text-red-700 border-red-200 bg-red-50"
                                >
                                    {item.bloodType}
                                </Badge>
                            </TableCell>
                            <TableCell className="text-neutral-700">{item.component}</TableCell>
                            <TableCell>
                                <Badge
                                    variant="secondary"
                                    className="bg-emerald-50 text-emerald-700 border border-emerald-200"
                                >
                                    {item.status}
                                </Badge>
                            </TableCell>
                            <TableCell className="text-neutral-600 text-sm">
                                {formatDate(item.collectedAt)}
                            </TableCell>
                            <TableCell className="text-neutral-600 text-sm">
                                {formatDate(item.expiresAt)}
                            </TableCell>
                        </TableRow>
                    ))}
                </TableBody>
            </Table>
        </div>
    );
}
