import { HttpResponse, http } from "msw";
import type { GetAvailableStockResponse } from "@/shared/api/client";

export const mockStockResponse: GetAvailableStockResponse = {
  items: [
    {
      id: "7d0d0f7b-9f93-4a0b-80df-6ff14b9104fa",
      donationNumber: "DON-20261031-0001",
      bloodType: "O+",
      component: "RedCells",
      collectedAt: "2026-10-31T10:00:00Z",
      expiresAt: "2026-12-12T10:00:00Z",
      status: "Available",
      bloodBankId: "11111111-1111-1111-1111-111111111111",
    },
    {
      id: "8e1e1f8c-0a04-5b1c-91e0-70a25c0215ab",
      donationNumber: "DON-20261031-0002",
      bloodType: "A-",
      component: "Platelets",
      collectedAt: "2026-10-31T11:00:00Z",
      expiresAt: "2026-11-05T11:00:00Z",
      status: "Available",
      bloodBankId: "11111111-1111-1111-1111-111111111111",
    },
  ],
  page: 1,
  pageSize: 10,
  totalCount: 2,
};

export const handlers = [
  http.get("*/v1/stock", () => {
    return HttpResponse.json(mockStockResponse);
  }),
];
