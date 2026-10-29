import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { HealthPanel } from "./HealthPanel";

describe("HealthPanel", () => {
  it("shows the API and database as healthy", async () => {
    const fetcher = vi.fn().mockResolvedValue({
      json: async () => ({ status: "healthy", database: "up" }),
    });

    render(<HealthPanel apiBaseUrl="http://127.0.0.1:5080" fetcher={fetcher} />);

    expect(await screen.findByTestId("api-status")).toHaveTextContent("reachable");
    expect(screen.getByTestId("database-status")).toHaveTextContent("up");
    expect(screen.getByTestId("overall-status")).toHaveTextContent("healthy");
  });

  it("shows the API as unreachable when the request fails", async () => {
    const fetcher = vi.fn().mockRejectedValue(new Error("connection refused"));

    render(<HealthPanel apiBaseUrl="http://127.0.0.1:5080" fetcher={fetcher} />);

    expect(await screen.findByTestId("api-status")).toHaveTextContent("unreachable");
    expect(screen.getByTestId("database-status")).toHaveTextContent("unknown");
    expect(screen.getByTestId("overall-status")).toHaveTextContent("unhealthy");
  });
});
