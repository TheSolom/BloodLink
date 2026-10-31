import { env } from "@/shared/lib/env";
import type { components } from "./generated/schema";

export type StockItem = components["schemas"]["StockItemDto"];
export type GetAvailableStockResponse = components["schemas"]["GetAvailableStockResponse"];

export type StockQueryParams = {
  bloodBankId?: string;
  bloodType?: string;
  component?: string;
  page?: number;
  pageSize?: number;
};

export async function fetchAvailableStock(
  params: StockQueryParams = {},
): Promise<GetAvailableStockResponse> {
  const url = new URL(`${env.VITE_API_BASE_URL}/v1/stock`, window.location.origin);
  if (params.bloodBankId) url.searchParams.set("bloodBankId", params.bloodBankId);
  if (params.bloodType) url.searchParams.set("bloodType", params.bloodType);
  if (params.component) url.searchParams.set("component", params.component);
  if (params.page !== undefined) url.searchParams.set("page", params.page.toString());
  if (params.pageSize !== undefined) url.searchParams.set("pageSize", params.pageSize.toString());

  const response = await fetch(url.toString(), {
    headers: {
      Accept: "application/json",
    },
  });

  if (!response.ok) {
    let errorDetail = "Failed to fetch stock";
    try {
      const problem = await response.json();
      errorDetail = problem.detail || problem.title || errorDetail;
    } catch {
      // ignore
    }
    throw new Error(errorDetail);
  }

  return response.json();
}
