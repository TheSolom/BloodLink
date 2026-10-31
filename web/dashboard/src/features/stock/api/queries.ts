import { useQuery } from "@tanstack/react-query";
import { fetchAvailableStock, type StockQueryParams } from "@/shared/api/client";
import { stockKeys } from "./keys";

export function useAvailableStock(params: StockQueryParams = {}) {
  return useQuery({
    queryKey: stockKeys.list(params),
    queryFn: () => fetchAvailableStock(params),
  });
}
