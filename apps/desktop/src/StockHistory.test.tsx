import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { StockHistory, type StockHistoryProduct } from "./StockHistory";
import type { Locale } from "./i18n";

const products: StockHistoryProduct[] = [
  { productId: "p1", sku: "EAU-1", name: "Eau" },
];

const movement = {
  id: "m1",
  productId: "p1",
  stockLocationId: "loc",
  movementType: "adjustment",
  quantityDelta: "-1.2500",
  reason: "Casse",
  balanceAfter: "11.2500",
  createdAt: "2026-10-28T18:40:00.000Z",
  createdBy: "8c1a6e2e-1c4a-4d5b-9e10-111111111111",
};

const receipt = {
  ...movement,
  id: "m2",
  movementType: "receipt",
  quantityDelta: "2.5000",
  reason: "Livraison",
  balanceAfter: "12.5000",
};

describe("StockHistory", () => {
  it("loads movements and shows the fields returned by the API", async () => {
    const fetcher = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => [movement],
    });

    render(
      <StockHistory
        apiBaseUrl="http://127.0.0.1:5080"
        accessToken="token"
        fetcher={fetcher}
        locale="fr"
        products={products}
      />,
    );

    const list = await screen.findByTestId("stock-history-list");
    const row = within(list).getByText("EAU-1 · Eau").closest("li");
    expect(row).toHaveTextContent("Ajustement");
    expect(screen.getByText("-1.2500")).toHaveAttribute("dir", "ltr");
    expect(screen.getByText("11.2500")).toHaveAttribute("dir", "ltr");
    expect(screen.getByText("Casse")).toBeInTheDocument();
    expect(screen.getByText("8c1a6e2e-1c4a-4d5b-9e10-111111111111")).toHaveAttribute("dir", "ltr");
    expect(within(row!).getByText(/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$/)).toHaveAttribute("dir", "ltr");
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
    expect(fetcher).toHaveBeenCalledWith(
      "http://127.0.0.1:5080/api/v1/stock/movements",
      expect.objectContaining({ headers: { authorization: "Bearer token" } }),
    );
  });

  it("shows an empty state when the ledger has no movements", async () => {
    const fetcher = vi.fn().mockResolvedValue({ ok: true, json: async () => [] });
    render(
      <StockHistory
        apiBaseUrl="http://127.0.0.1:5080"
        accessToken="token"
        fetcher={fetcher}
        locale="en"
        products={products}
      />,
    );

    expect(await screen.findByText("No movements.")).toBeInTheDocument();
    expect(screen.queryByText("Loading history…")).not.toBeInTheDocument();
  });

  it("shows a translated error when the history request fails", async () => {
    const fetcher = vi.fn().mockResolvedValue({ ok: false, json: async () => ({ code: "auth.forbidden" }) });
    render(
      <StockHistory
        apiBaseUrl="http://127.0.0.1:5080"
        accessToken="token"
        fetcher={fetcher}
        locale="fr"
        products={products}
      />,
    );

    expect(await screen.findByRole("alert")).toHaveTextContent("L'historique des mouvements est indisponible.");
    expect(screen.queryByText("Aucun mouvement.")).not.toBeInTheDocument();
    expect(screen.queryByText("-1.2500")).not.toBeInTheDocument();
  });

  it("shows a translated error when the request cannot be sent", async () => {
    const fetcher = vi.fn().mockRejectedValue(new Error("offline"));
    render(
      <StockHistory
        apiBaseUrl="http://127.0.0.1:5080"
        accessToken="token"
        fetcher={fetcher}
        locale="en"
        products={products}
      />,
    );

    expect(await screen.findByRole("alert")).toHaveTextContent("Movement history is unavailable.");
  });

  it.each([
    ["fr", "Historique", "Aucun mouvement."],
    ["ar", "السجل", "لا توجد حركات."],
    ["en", "History", "No movements."],
  ] as const)("translates the history heading and empty state in %s", async (locale: Locale, heading, empty) => {
    const fetcher = vi.fn().mockResolvedValue({ ok: true, json: async () => [] });
    render(
      <StockHistory
        apiBaseUrl="http://127.0.0.1:5080"
        accessToken="token"
        fetcher={fetcher}
        locale={locale}
        products={products}
      />,
    );

    expect(await screen.findByRole("heading", { name: heading })).toBeInTheDocument();
    expect(screen.getByText(empty)).toBeInTheDocument();
  });

  it("keeps a negative quantity readable in an Arabic right-to-left layout", async () => {
    document.documentElement.lang = "ar";
    document.documentElement.dir = "rtl";
    const fetcher = vi.fn().mockResolvedValue({ ok: true, json: async () => [movement] });
    render(
      <StockHistory
        apiBaseUrl="http://127.0.0.1:5080"
        accessToken="token"
        fetcher={fetcher}
        locale="ar"
        products={products}
      />,
    );

    const quantity = await screen.findByText("-1.2500");
    expect(quantity.tagName).toBe("BDI");
    expect(quantity).toHaveAttribute("dir", "ltr");
    expect(quantity).toHaveStyle({ unicodeBidi: "isolate" });
    expect(quantity).toHaveTextContent("-1.2500");
    expect(screen.getByRole("heading", { name: "السجل" })).toBeInTheDocument();
    expect(quantity.closest("li")).toHaveTextContent("تسوية");
    document.documentElement.lang = "fr";
    document.documentElement.dir = "ltr";
  });

  it("shows a loading state until the history response arrives", async () => {
    let resolve: (value: { ok: boolean; json: () => Promise<unknown[]> }) => void = () => undefined;
    const fetcher = vi.fn(
      () =>
        new Promise<{ ok: boolean; json: () => Promise<unknown[]> }>((done) => {
          resolve = done;
        }),
    );
    render(
      <StockHistory
        apiBaseUrl="http://127.0.0.1:5080"
        accessToken="token"
        fetcher={fetcher}
        locale="fr"
        products={products}
      />,
    );

    expect(screen.getByText("Chargement de l'historique…")).toBeInTheDocument();
    expect(screen.queryByText("Aucun mouvement.")).not.toBeInTheDocument();
    resolve({ ok: true, json: async () => [] });
    expect(await screen.findByText("Aucun mouvement.")).toBeInTheDocument();
    expect(screen.queryByText("Chargement de l'historique…")).not.toBeInTheDocument();
  });

  it("filters by product through the API and by movement type in the view", async () => {
    const user = userEvent.setup();
    const fetcher = vi.fn(async (input: string) => {
      const url = String(input);
      const rows = url.includes("productId=p1") ? [movement] : [movement, receipt];
      return { ok: true, json: async () => rows };
    });
    render(
      <StockHistory
        apiBaseUrl="http://127.0.0.1:5080"
        accessToken="token"
        fetcher={fetcher}
        locale="fr"
        products={products}
      />,
    );

    const list = await screen.findByTestId("stock-history-list");
    expect(within(list).getByText("Livraison")).toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText("Type de mouvement"), "adjustment");
    expect(within(list).queryByText("Livraison")).not.toBeInTheDocument();
    expect(within(list).getByText("Casse")).toBeInTheDocument();
    expect(fetcher).toHaveBeenCalledTimes(1);

    await user.selectOptions(screen.getByLabelText("Produit"), "p1");
    const filtered = await screen.findByTestId("stock-history-list");
    expect(within(filtered).getByText("Casse")).toBeInTheDocument();
    expect(fetcher).toHaveBeenLastCalledWith(
      "http://127.0.0.1:5080/api/v1/stock/movements?productId=p1",
      expect.anything(),
    );
  });
});
