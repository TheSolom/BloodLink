import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/shared/ui/select";
import { Skeleton } from "@/shared/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/shared/ui/table";
import { AlertCircle, Droplet, RefreshCw } from "lucide-react";
import * as React from "react";
import { useAvailableStock } from "../api/queries";

export function StockOverviewPage() {
  const [bloodType, setBloodType] = React.useState<string>("ALL");
  const [component, setComponent] = React.useState<string>("ALL");
  const [page, setPage] = React.useState<number>(1);
  const pageSize = 10;

  const queryParams = {
    bloodType: bloodType === "ALL" ? undefined : bloodType,
    component: component === "ALL" ? undefined : component,
    page,
    pageSize,
  };

  const { data, isLoading, isError, error, refetch, isFetching } = useAvailableStock(queryParams);

  const totalCount = Number(data?.totalCount ?? 0);
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  return (
    <div className="container mx-auto p-6 max-w-6xl space-y-6">
      <header className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 pb-6 border-b border-neutral-200">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-neutral-900 flex items-center gap-2">
            <Droplet className="h-6 w-6 text-red-600 fill-red-600" />
            Blood Stock Overview
          </h1>
          <p className="text-sm text-neutral-500 mt-1">
            Real-time monitoring of available, non-expired blood units across facilities.
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

      {/* Filters */}
      <section
        aria-label="Stock Filters"
        className="flex flex-wrap items-center gap-4 bg-neutral-50 p-4 rounded-lg border border-neutral-200"
      >
        <div className="flex items-center gap-2">
          <label htmlFor="blood-type-filter" className="text-sm font-medium text-neutral-700">
            Blood Type:
          </label>
          <div className="w-36">
            <Select
              value={bloodType}
              onValueChange={(val) => {
                setBloodType(val);
                setPage(1);
              }}
            >
              <SelectTrigger id="blood-type-filter" aria-label="Filter by Blood Type">
                <SelectValue placeholder="All Blood Types" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="ALL">All Types</SelectItem>
                <SelectItem value="A+">A+</SelectItem>
                <SelectItem value="A-">A-</SelectItem>
                <SelectItem value="B+">B+</SelectItem>
                <SelectItem value="B-">B-</SelectItem>
                <SelectItem value="AB+">AB+</SelectItem>
                <SelectItem value="AB-">AB-</SelectItem>
                <SelectItem value="O+">O+</SelectItem>
                <SelectItem value="O-">O-</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <label htmlFor="component-filter" className="text-sm font-medium text-neutral-700">
            Component:
          </label>
          <div className="w-40">
            <Select
              value={component}
              onValueChange={(val) => {
                setComponent(val);
                setPage(1);
              }}
            >
              <SelectTrigger id="component-filter" aria-label="Filter by Component">
                <SelectValue placeholder="All Components" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="ALL">All Components</SelectItem>
                <SelectItem value="RedCells">Red Cells</SelectItem>
                <SelectItem value="Platelets">Platelets</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>
      </section>

      {/* Content States */}
      {isLoading ? (
        <div role="status" aria-label="Loading stock units" className="space-y-3">
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-12 w-full" />
          <Skeleton className="h-12 w-full" />
          <Skeleton className="h-12 w-full" />
          <span className="sr-only">Loading stock data...</span>
        </div>
      ) : isError ? (
        <div
          role="alert"
          className="flex items-center gap-3 p-4 rounded-lg bg-red-50 border border-red-200 text-red-800"
        >
          <AlertCircle className="h-5 w-5 shrink-0 text-red-600" />
          <div>
            <h2 className="font-semibold text-sm">Failed to load blood stock</h2>
            <p className="text-sm mt-0.5">
              {error instanceof Error ? error.message : "An unexpected error occurred."}
            </p>
          </div>
          <Button
            variant="outline"
            size="sm"
            onClick={() => refetch()}
            className="ml-auto bg-white border-red-300 text-red-700 hover:bg-red-50"
          >
            Retry
          </Button>
        </div>
      ) : !data || data.items.length === 0 ? (
        <div
          role="status"
          aria-label="No stock available"
          className="text-center py-12 px-4 rounded-lg border border-dashed border-neutral-300 bg-neutral-50/50"
        >
          <Droplet className="h-10 w-10 text-neutral-400 mx-auto mb-3" />
          <h2 className="text-lg font-semibold text-neutral-700">No blood units available</h2>
          <p className="text-sm text-neutral-500 max-w-sm mx-auto mt-1">
            There are currently no active, non-expired blood units matching the selected criteria.
          </p>
        </div>
      ) : (
        <div className="space-y-4">
          <div className="rounded-md border border-neutral-200 overflow-hidden bg-white shadow-sm">
            <Table aria-label="Available Blood Stock">
              <TableHeader>
                <TableRow>
                  <TableHead className="w-[180px]">Donation Number</TableHead>
                  <TableHead>Blood Type</TableHead>
                  <TableHead>Component</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Collected Date</TableHead>
                  <TableHead>Expiry Date</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.items.map((item) => (
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
                      {new Date(item.collectedAt).toLocaleDateString()}
                    </TableCell>
                    <TableCell className="text-neutral-600 text-sm">
                      {new Date(item.expiresAt).toLocaleDateString()}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>

          {/* Pagination */}
          <div className="flex items-center justify-between text-sm text-neutral-600 px-2 py-1">
            <div>
              Showing {data.items.length} of {totalCount} units
            </div>
            <div className="flex items-center gap-2">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
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
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page >= totalPages}
                aria-label="Next page"
              >
                Next
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
