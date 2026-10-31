import { type StockQueryParams, fetchAvailableStock } from "@/shared/api/client";
import { useQuery } from "@tanstack/react-query";
import { stockKeys } from "./keys";

export function useAvailableStock(params: StockQueryParams = {}) {
  return useQuery({
    queryKey: stockKeys.list(params),
    queryFn: () => fetchAvailableStock(params),
  });
}
