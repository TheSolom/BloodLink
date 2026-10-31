import type { StockQueryParams } from "@/shared/api/client";

export const stockKeys = {
  all: ["stock"] as const,
  lists: () => [...stockKeys.all, "list"] as const,
  list: (params: StockQueryParams) => [...stockKeys.lists(), params] as const,
};
