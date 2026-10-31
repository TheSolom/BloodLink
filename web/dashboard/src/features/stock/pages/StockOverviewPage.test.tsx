import { render, screen } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import { createQueryClient, Providers } from "@/app/providers";
import { server } from "@/test/setup";
import { StockOverviewPage } from "./StockOverviewPage";

function renderWithProviders() {
  const testQueryClient = createQueryClient({ retry: false });
  return render(
    <Providers client={testQueryClient}>
      <StockOverviewPage />
    </Providers>,
  );
}

describe("StockOverviewPage", () => {
  it("renders stock items with donation numbers in data state", async () => {
    renderWithProviders();

    // Verify loading state appears first
    expect(screen.getByRole("status", { name: /loading stock units/i })).toBeInTheDocument();

    // Wait for data to load
    const heading = await screen.findByRole("heading", { name: /blood stock overview/i });
    expect(heading).toBeInTheDocument();

    // Verify donation numbers are rendered
    expect(await screen.findByText("DON-20261031-0001")).toBeInTheDocument();
    expect(screen.getByText("DON-20261031-0002")).toBeInTheDocument();

    // Verify blood types and components
    expect(screen.getByText("O+")).toBeInTheDocument();
    expect(screen.getByText("A-")).toBeInTheDocument();
    expect(screen.getByText("RedCells")).toBeInTheDocument();
    expect(screen.getByText("Platelets")).toBeInTheDocument();

    // Verify total count
    expect(screen.getByText(/showing 2 of 2 units/i)).toBeInTheDocument();
  });

  it("renders empty state when no stock is available", async () => {
    server.use(
      http.get("*/v1/stock", () => {
        return HttpResponse.json({
          items: [],
          page: 1,
          pageSize: 10,
          totalCount: 0,
        });
      }),
    );

    renderWithProviders();

    expect(await screen.findByRole("status", { name: /no stock available/i })).toBeInTheDocument();
    expect(screen.getByText(/no blood units available/i)).toBeInTheDocument();
  });

  it("renders error state when api fails", async () => {
    server.use(
      http.get("*/v1/stock", () => {
        return HttpResponse.json(
          { title: "Service Unavailable", detail: "Database connection failed" },
          { status: 500 },
        );
      }),
    );

    renderWithProviders();

    expect(await screen.findByRole("alert")).toBeInTheDocument();
    expect(screen.getByText(/failed to load blood stock/i)).toBeInTheDocument();
    expect(screen.getByText(/database connection failed/i)).toBeInTheDocument();
  });
});
