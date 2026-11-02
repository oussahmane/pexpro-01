import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { CatalogPanel } from "./CatalogPanel";

const eau = {
  id: "p1",
  sku: "EAU-1",
  name: "Eau",
  unit: "u",
  unitPrice: "10.0000",
  wholesaleUnitPrice: "8.0000",
  minimumUnitPrice: "7.0000",
  trackStock: true,
  isActive: true,
};

describe("CatalogPanel", () => {
  it("sends every price when a product name changes", async () => {
    const user = userEvent.setup();
    const fetcher = vi.fn(async (input: string, init?: RequestInit) => {
      const url = String(input);
      if (url.endsWith("/api/v1/products") && init?.method !== "PATCH") {
        return json([eau]);
      }
      if (url.endsWith("/api/v1/categories")) {
        return json([]);
      }
      if (init?.method === "PATCH") {
        const body = JSON.parse(String(init.body));
        return json({ ...eau, ...body });
      }
      throw new Error(url);
    });
    render(<CatalogPanel apiBaseUrl="http://127.0.0.1:5080" accessToken="token" fetcher={fetcher} locale="fr" />);
    await user.click(await screen.findByRole("button", { name: "Modifier Eau" }));
    const name = screen.getByLabelText("Nom");
    await user.clear(name);
    await user.type(name, "Eau minérale");
    await user.click(screen.getByRole("button", { name: "Enregistrer le produit" }));

    const patch = fetcher.mock.calls.find((call) => call[1]?.method === "PATCH");
    expect(JSON.parse(String(patch?.[1]?.body))).toMatchObject({
      name: "Eau minérale",
      unitPrice: "10.0000",
      wholesaleUnitPrice: "8.0000",
      minimumUnitPrice: "7.0000",
    });
  });
});

function json(body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { "content-type": "application/json" },
  });
}
