import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { describe, expect, it, vi } from "vitest";
import { PosPanel } from "./PosPanel";
import type { Locale } from "./i18n";

const eau = {
  id: "p1",
  sku: "EAU-1",
  name: "Eau",
  unit: "u",
  unitPrice: "10.0000",
  trackStock: true,
  isActive: true,
};

const flour = {
  id: "p2",
  sku: "FAR-1",
  name: "Farine",
  unit: "kg",
  unitPrice: "5.0000",
  trackStock: false,
  isActive: true,
};

const amin = [
  { id: "c1", type: "individual", name: "Amin", phone: "0551000001", address: null, notes: null, rc: null, nif: null, nis: null, ai: null, creditLimit: null, isActive: true },
];

const inactive = {
  id: "p3",
  sku: "OLD-1",
  name: "Ancien",
  unit: "u",
  unitPrice: "8.0000",
  trackStock: false,
  isActive: false,
};

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json" },
  });
}

function saleBody(number = "V-000007") {
  return {
    id: "sale-1",
    number,
    status: "posted",
    subtotal: "10.0000",
    discountAmount: "0.0000",
    total: "10.0000",
    createdAt: "2026-10-30T12:00:00Z",
    createdBy: "user-1",
    lines: [
      {
        id: "line-1",
        lineNumber: 1,
        productId: eau.id,
        productSku: eau.sku,
        productName: eau.name,
        productUnit: eau.unit,
        quantity: "1.0000",
        listUnitPrice: "10.0000",
        unitPrice: "10.0000",
        lineTotal: "10.0000",
        tracksStock: true,
        stockMovementId: "move-1",
        taxRate: "0.190000",
      },
    ],
    payments: [{ id: "pay-1", method: "cash", amount: "10.0000", paidAt: "2026-10-30T12:00:00Z" }],
  };
}

function renderRegister(
  fetcher: (input: string, init?: RequestInit) => Promise<Response>,
  locale: Locale = "fr",
  createKey: () => string = () => "key-1",
  administrator = false,
) {
  return render(
    <PosPanel
      apiBaseUrl="http://127.0.0.1:5080"
      accessToken="token"
      fetcher={fetcher}
      locale={locale}
      administrator={administrator}
      createKey={createKey}
    />,
  );
}

describe("PosPanel", () => {
  it("searches by name, changes quantity, and removes the product", async () => {
    const user = userEvent.setup();
    renderRegister(catalogFetcher([eau, flour]));
    await screen.findByRole("button", { name: /Farine/ });

    await user.type(screen.getByLabelText("Nom, référence ou code scanner"), "far");
    await user.click(screen.getByRole("button", { name: /Farine/ }));
    expect(screen.getByTestId("pos-total")).toHaveTextContent("5.0000");

    await user.clear(screen.getByLabelText("Quantité Farine"));
    await user.type(screen.getByLabelText("Quantité Farine"), "2.0000");
    expect(screen.getByTestId("pos-total")).toHaveTextContent("10.0000");
    expect(screen.getByLabelText("Quantité Farine")).toHaveAttribute("dir", "ltr");

    await user.click(screen.getByRole("button", { name: "Retirer Farine" }));
    expect(screen.getByText("Le ticket est vide.")).toBeInTheDocument();
  });

  it("accepts a keyboard-wedge code and reports unknown and inactive products", async () => {
    const user = userEvent.setup();
    renderRegister(catalogFetcher([eau, inactive]));
    const search = await screen.findByLabelText("Nom, référence ou code scanner");

    await user.type(search, "EAU-1{Enter}");
    expect(screen.getByTestId("pos-total")).toHaveTextContent("10.0000");

    await user.type(screen.getByLabelText("Nom, référence ou code scanner"), "ABSENT{Enter}");
    expect(await screen.findByRole("alert")).toHaveTextContent("Produit introuvable.");

    await user.clear(screen.getByLabelText("Nom, référence ou code scanner"));
    await user.type(screen.getByLabelText("Nom, référence ou code scanner"), "OLD-1{Enter}");
    expect(await screen.findByRole("alert")).toHaveTextContent("Le produit est inactif.");
    expect(screen.getByTestId("pos-total")).toHaveTextContent("10.0000");
  });

  it("rejects an invalid quantity before checkout", async () => {
    const user = userEvent.setup();
    renderRegister(catalogFetcher([eau]));
    await user.type(await screen.findByLabelText("Nom, référence ou code scanner"), "Eau{Enter}");
    const quantity = screen.getByLabelText("Quantité Eau");

    await user.clear(quantity);
    await user.type(quantity, "0");
    expect(screen.getByRole("alert")).toHaveTextContent("La quantité doit être strictement positive.");
    expect(screen.getByRole("button", { name: "Encaisser" })).toBeDisabled();

    await user.clear(quantity);
    await user.type(quantity, "1.00001");
    expect(screen.getByRole("alert")).toHaveTextContent("quatre décimales");
    expect(screen.getByRole("button", { name: "Encaisser" })).toBeDisabled();
  });

  it("confirms a cash sale and clears the ticket only after success", async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn(catalogFetcher([eau], [], saleBody()));
    renderRegister(fetchMock);
    await user.type(await screen.findByLabelText("Nom, référence ou code scanner"), "EAU-1{Enter}");
    await user.click(screen.getByRole("button", { name: "Encaisser" }));
    expect(fetchMock).not.toHaveBeenCalledWith(expect.stringContaining("/api/v1/sales"), expect.objectContaining({ method: "POST" }));

    await user.click(screen.getByRole("button", { name: "Confirmer l'encaissement" }));
    expect(await screen.findByRole("status")).toHaveTextContent("V-000007");
    expect(screen.getByText("Le ticket est vide.")).toBeInTheDocument();
    const posted = fetchMock.mock.calls.find((call) => call[1]?.method === "POST");
    expect(headerValue(posted?.[1], "Idempotency-Key")).toBe("key-1");
    expect(JSON.parse(String(posted?.[1]?.body)).payments).toEqual([{ method: "cash", amount: "10.0000" }]);
  });

  it("keeps the ticket and the same idempotency key when checkout fails", async () => {
    const user = userEvent.setup();
    const createKey = vi.fn(() => "key-kept");
    let attempts = 0;
    const fetchMock = vi.fn(async (input: string, init?: RequestInit) => {
      const url = String(input);
      if (url.endsWith("/api/v1/products")) {
        return json([eau]);
      }
      if (url.endsWith("/api/v1/stock/balances")) {
        return json([{ productId: eau.id, quantity: "0.0000" }]);
      }
      if (init?.method === "POST") {
        attempts += 1;
        if (attempts === 1) {
          return json({ code: "stock.insufficient", available: "0.0000", requested: "1.0000" }, 409);
        }
        return json(saleBody(), 201);
      }
      return json([]);
    });
    renderRegister(fetchMock, "fr", createKey);
    await user.type(await screen.findByLabelText("Nom, référence ou code scanner"), "EAU-1{Enter}");
    expect(screen.getByText("Stock insuffisant.")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Encaisser" }));
    await user.click(screen.getByRole("button", { name: "Confirmer l'encaissement" }));
    expect(await screen.findByText("0.0000 / 1.0000")).toBeInTheDocument();
    expect(screen.getByLabelText("Quantité Eau")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Réessayer l'encaissement" }));
    expect(await screen.findByRole("status")).toHaveTextContent("V-000007");
    expect(createKey).toHaveBeenCalledTimes(1);
    const keys = fetchMock.mock.calls
      .filter((call) => call[1]?.method === "POST")
      .map((call) => headerValue(call[1], "Idempotency-Key"));
    expect(keys).toEqual(["key-kept", "key-kept"]);
  });

  it("does not send a second checkout while the first is still running", async () => {
    const user = userEvent.setup();
    let resolveSale: (response: Response) => void = () => undefined;
    let posts = 0;
    const fetchMock = vi.fn(async (input: string, init?: RequestInit) => {
      const url = String(input);
      if (init?.method === "POST") {
        posts += 1;
        return new Promise<Response>((resolve) => {
          resolveSale = resolve;
        });
      }
      if (url.endsWith("/api/v1/products")) {
        return json([eau]);
      }
      if (url.endsWith("/api/v1/stock/balances") || url.endsWith("/api/v1/sales")) {
        return json([]);
      }
      throw new Error(url);
    });
    renderRegister(fetchMock);
    await user.type(await screen.findByLabelText("Nom, référence ou code scanner"), "EAU-1{Enter}");
    await user.click(screen.getByRole("button", { name: "Encaisser" }));
    const confirm = screen.getByRole("button", { name: "Confirmer l'encaissement" });
    confirm.click();
    confirm.click();
    await waitFor(() => expect(posts).toBe(1));
    resolveSale(json(saleBody(), 201));
    expect(await screen.findByRole("status")).toHaveTextContent("V-000007");
  });

  it("shows sale history and the detail returned by the API", async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn(async (input: string) => {
      const url = String(input);
      if (url.endsWith("/api/v1/products")) {
        return json([]);
      }
      if (url.endsWith("/api/v1/stock/balances")) {
        return json([]);
      }
      if (url.endsWith("/api/v1/sales/sale-1")) {
        return json(saleBody("V-000001"));
      }
      if (url.endsWith("/api/v1/sales")) {
        return json([{ id: "sale-1", number: "V-000001", total: "10.0000", createdAt: "2026-10-30T12:00:00Z" }]);
      }
      throw new Error(url);
    });
    renderRegister(fetchMock);
    await user.click(await screen.findByRole("button", { name: /V-000001/ }));
    expect(await screen.findByRole("article", { name: "V-000001" })).toHaveTextContent("Eau");
    expect(screen.getByRole("article", { name: "V-000001" })).toHaveTextContent("10.0000");
  });

  it("uses Arabic copy and keeps amounts left to right", async () => {
    const user = userEvent.setup();
    function Harness() {
      const [locale, setLocale] = useState<Locale>("fr");
      return (
        <>
          <button type="button" onClick={() => setLocale("ar")}>
            العربية
          </button>
          <PosPanel
            apiBaseUrl="http://127.0.0.1:5080"
            accessToken="token"
            fetcher={catalogFetcher([eau])}
            locale={locale}
            createKey={() => "key-1"}
          />
        </>
      );
    }
    render(<Harness />);
    expect(await screen.findByRole("heading", { name: "Caisse" })).toBeInTheDocument();
    expect(screen.getByTestId("pos")).toHaveAttribute("dir", "ltr");
    await user.click(screen.getByRole("button", { name: "العربية" }));
    expect(screen.getByRole("heading", { name: "الصندوق" })).toBeInTheDocument();
    expect(screen.getByTestId("pos")).toHaveAttribute("dir", "rtl");
    await user.type(screen.getByLabelText("الاسم أو المرجع أو قارئ الرمز"), "EAU-1{Enter}");
    expect(screen.getAllByText("10.0000").every((node) => node.getAttribute("dir") === "ltr")).toBe(true);
  });

  it("replaces a stale catalog price and uses a new idempotency key", async () => {
    const user = userEvent.setup();
    const createKey = vi.fn()
      .mockReturnValueOnce("key-old")
      .mockReturnValueOnce("key-new");
    let price = "10.0000";
    const fetchMock = vi.fn(async (input: string, init?: RequestInit) => {
      const url = String(input);
      if (url.endsWith("/api/v1/products")) {
        return json([{ ...eau, unitPrice: price }]);
      }
      if (url.endsWith("/api/v1/stock/balances")) {
        return json([{ productId: eau.id, quantity: "5.0000" }]);
      }
      if (init?.method === "POST") {
        if (price === "10.0000") {
          price = "15.0000";
          return json({ code: "validation.failed", errors: { unitPrice: ["unitPrice.mismatch"] } }, 400);
        }
        return json(saleBody(), 201);
      }
      return json([]);
    });
    renderRegister(fetchMock, "fr", createKey);
    await user.type(await screen.findByLabelText("Nom, référence ou code scanner"), "EAU-1{Enter}");
    await user.click(screen.getByRole("button", { name: "Encaisser" }));
    await user.click(screen.getByRole("button", { name: "Confirmer l'encaissement" }));
    expect(await screen.findByText("Le prix ne correspond plus au catalogue.")).toBeInTheDocument();
    expect(await screen.findByTestId("pos-total")).toHaveTextContent("15.0000");
    expect(screen.getByLabelText("Quantité Eau")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Réessayer l'encaissement" }));
    expect(await screen.findByRole("status")).toHaveTextContent("V-000007");
    const keys = fetchMock.mock.calls
      .filter((call) => call[1]?.method === "POST")
      .map((call) => headerValue(call[1], "Idempotency-Key"));
    expect(keys).toEqual(["key-old", "key-old"]);
    expect(JSON.parse(String(fetchMock.mock.calls.filter((call) => call[1]?.method === "POST")[1]?.[1]?.body)).lines[0].unitPrice).toBe("15.0000");
  });

  it("lets a cashier discount down to the minimum and blocks a discount below it", async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn(catalogFetcher([{ ...eau, minimumUnitPrice: "8.0000" }]));
    renderRegister(fetchMock);
    await user.type(await screen.findByLabelText("Nom, référence ou code scanner"), "EAU-1{Enter}");
    await user.selectOptions(screen.getByLabelText("Remise Eau"), "amount");
    await user.type(screen.getByLabelText("Valeur de la remise Eau"), "3");
    expect(screen.getByRole("alert")).toHaveTextContent("sous le prix minimum");
    expect(screen.getByRole("button", { name: "Encaisser" })).toBeDisabled();

    await user.clear(screen.getByLabelText("Valeur de la remise Eau"));
    await user.type(screen.getByLabelText("Valeur de la remise Eau"), "2");
    expect(screen.getByTestId("pos-total")).toHaveTextContent("8.0000");

    await user.click(screen.getByRole("button", { name: "Encaisser" }));
    await user.click(screen.getByRole("button", { name: "Confirmer l'encaissement" }));
    const posted = fetchMock.mock.calls.find((call) => call[1]?.method === "POST");
    const body = JSON.parse(String(posted?.[1]?.body));
    expect(body.lines[0]).toMatchObject({
      priceKind: "retail",
      unitPrice: "10.0000",
      discountKind: "amount",
      lineDiscount: "2.0000",
      lineTotal: "8.0000",
    });
    expect(body.discountKind).toBe("none");
    expect(JSON.stringify(body)).not.toContain("belowMinimumOverride");
  });

  it("uses a new idempotency key only after the cashier changes the ticket", async () => {
    const user = userEvent.setup();
    const createKey = vi.fn().mockReturnValueOnce("key-a").mockReturnValueOnce("key-b");
    const fetchMock = vi.fn(async (input: string, init?: RequestInit) => {
      const url = String(input);
      if (url.endsWith("/api/v1/products")) {
        return json([eau]);
      }
      if (init?.method === "POST") {
        return json({ code: "stock.insufficient", available: "0.0000", requested: "1.0000" }, 409);
      }
      return json([]);
    });
    renderRegister(fetchMock, "fr", createKey);
    await user.type(await screen.findByLabelText("Nom, référence ou code scanner"), "EAU-1{Enter}");
    await user.click(screen.getByRole("button", { name: "Encaisser" }));
    await user.click(screen.getByRole("button", { name: "Confirmer l'encaissement" }));
    expect(await screen.findByText("0.0000 / 1.0000")).toBeInTheDocument();

    await user.clear(screen.getByLabelText("Quantité Eau"));
    await user.type(screen.getByLabelText("Quantité Eau"), "2");
    await user.click(screen.getByRole("button", { name: "Encaisser" }));
    await user.click(screen.getByRole("button", { name: "Confirmer l'encaissement" }));
    await waitFor(() => expect(fetchMock.mock.calls.filter((call) => call[1]?.method === "POST")).toHaveLength(2));
    const keys = fetchMock.mock.calls
      .filter((call) => call[1]?.method === "POST")
      .map((call) => headerValue(call[1], "Idempotency-Key"));
    expect(keys).toEqual(["key-a", "key-b"]);
  });

  it("sends an administrator price override without a client override flag", async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn(catalogFetcher([eau], [], saleBody()));
    renderRegister(fetchMock, "fr", () => "key-admin", true);
    await user.type(await screen.findByLabelText("Nom, référence ou code scanner"), "EAU-1{Enter}");
    const price = screen.getByLabelText("Prix saisi Eau");
    await user.clear(price);
    await user.type(price, "7");
    expect(screen.getByTestId("pos-total")).toHaveTextContent("7.0000");
    await user.click(screen.getByRole("button", { name: "Encaisser" }));
    await user.click(screen.getByRole("button", { name: "Confirmer l'encaissement" }));
    const posted = fetchMock.mock.calls.find((call) => call[1]?.method === "POST");
    const body = JSON.parse(String(posted?.[1]?.body));
    expect(body.lines[0].unitPrice).toBe("7.0000");
    expect(JSON.stringify(body)).not.toContain("belowMinimumOverride");
  });

  it("sends a selected customer and returns to a walk-in sale when the selection is cleared", async () => {
    const user = userEvent.setup();
    const customers = [
      { id: "c1", type: "individual", name: "Amin", phone: "0551000001", address: null, notes: null, rc: null, nif: null, nis: null, ai: null, creditLimit: null, isActive: true },
      { id: "c2", type: "business", name: "Amin", phone: "0551000002", address: null, notes: null, rc: null, nif: null, nis: null, ai: null, creditLimit: null, isActive: true },
    ];
    const fetchMock = vi.fn(async (input: string, init?: RequestInit) => {
      const url = String(input);
      if (url.endsWith("/api/v1/products")) {
        return json([eau]);
      }
      if (url.endsWith("/api/v1/stock/balances")) {
        return json([]);
      }
      if (url.includes("/api/v1/customers")) {
        return json(customers);
      }
      if (init?.method === "POST") {
        return json(saleBody(), 201);
      }
      if (url.endsWith("/api/v1/sales")) {
        return json([]);
      }
      throw new Error(url);
    });
    renderRegister(fetchMock);
    await user.type(await screen.findByLabelText("Nom, référence ou code scanner"), "EAU-1{Enter}");
    await user.type(screen.getByLabelText("Client facultatif"), "amin");
    await user.click(await screen.findByRole("button", { name: /0551000002/ }));
    await user.click(screen.getByRole("button", { name: "Retirer le client" }));
    await user.click(screen.getByRole("button", { name: "Encaisser" }));
    await user.click(screen.getByRole("button", { name: "Confirmer l'encaissement" }));
    const walkIn = JSON.parse(String(fetchMock.mock.calls.find((call) => call[1]?.method === "POST")?.[1]?.body));
    expect(walkIn.customerId).toBeUndefined();
    expect(walkIn.payments).toEqual([{ method: "cash", amount: "10.0000" }]);

    await user.type(screen.getByLabelText("Nom, référence ou code scanner"), "EAU-1{Enter}");
    await user.type(screen.getByLabelText("Client facultatif"), "amin");
    await user.click(await screen.findByRole("button", { name: /0551000001/ }));
    await user.click(screen.getByRole("button", { name: "Encaisser" }));
    await user.click(screen.getByRole("button", { name: "Confirmer l'encaissement" }));
    const linked = fetchMock.mock.calls.filter((call) => call[1]?.method === "POST").at(-1);
    expect(JSON.parse(String(linked?.[1]?.body)).customerId).toBe("c1");
    expect(JSON.parse(String(linked?.[1]?.body)).payments).toEqual([{ method: "cash", amount: "10.0000" }]);
  });

  it("sends a full, partial, or unpaid customer tender and shows the outstanding amount", async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn(customerFetcher());
    renderRegister(fetchMock);
    await user.type(await screen.findByLabelText("Nom, référence ou code scanner"), "EAU-1{Enter}");
    await user.type(screen.getByLabelText("Client facultatif"), "amin");
    await user.click(await screen.findByRole("button", { name: /0551000001/ }));
    expect(screen.getByLabelText("Montant reçu")).toHaveValue("10.0000");
    expect(screen.getByTestId("pos-outstanding")).toHaveTextContent("0.0000");
    await user.click(screen.getByRole("button", { name: "Encaisser" }));
    await user.click(screen.getByRole("button", { name: "Confirmer l'encaissement" }));
    const full = JSON.parse(String(fetchMock.mock.calls.find((call) => call[1]?.method === "POST")?.[1]?.body));
    expect(full.payments).toEqual([{ method: "cash", amount: "10.0000" }]);
    expect(full.customerId).toBe("c1");

    await user.type(screen.getByLabelText("Nom, référence ou code scanner"), "EAU-1{Enter}");
    await user.type(screen.getByLabelText("Client facultatif"), "amin");
    await user.click(await screen.findByRole("button", { name: /0551000001/ }));
    await user.selectOptions(screen.getByLabelText("Mode de paiement"), "transfer");
    const amount = screen.getByLabelText("Montant reçu");
    await user.clear(amount);
    await user.type(amount, "4");
    expect(screen.getByTestId("pos-outstanding")).toHaveTextContent("6.0000");
    await user.click(screen.getByRole("button", { name: "Encaisser" }));
    await user.click(screen.getByRole("button", { name: "Confirmer l'encaissement" }));
    const partial = JSON.parse(String(fetchMock.mock.calls.filter((call) => call[1]?.method === "POST").at(-1)?.[1]?.body));
    expect(partial.payments).toEqual([{ method: "transfer", amount: "4.0000" }]);

    await user.type(screen.getByLabelText("Nom, référence ou code scanner"), "EAU-1{Enter}");
    await user.type(screen.getByLabelText("Client facultatif"), "amin");
    await user.click(await screen.findByRole("button", { name: /0551000001/ }));
    await user.clear(screen.getByLabelText("Montant reçu"));
    expect(screen.getByTestId("pos-outstanding")).toHaveTextContent("10.0000");
    await user.click(screen.getByRole("button", { name: "Encaisser" }));
    await user.click(screen.getByRole("button", { name: "Confirmer l'encaissement" }));
    const unpaid = JSON.parse(String(fetchMock.mock.calls.filter((call) => call[1]?.method === "POST").at(-1)?.[1]?.body));
    expect(unpaid.payments).toEqual([]);
    expect(unpaid.customerId).toBe("c1");
  });

  it("uses a new idempotency key when the initial payment changes", async () => {
    const user = userEvent.setup();
    const createKey = vi.fn().mockReturnValueOnce("key-pay-a").mockReturnValueOnce("key-pay-b");
    const fetchMock = vi.fn(async (input: string, init?: RequestInit) => {
      const url = String(input);
      if (url.endsWith("/api/v1/products")) {
        return json([eau]);
      }
      if (url.includes("/api/v1/customers")) {
        return json(amin);
      }
      if (init?.method === "POST") {
        return json({ code: "stock.insufficient", available: "0.0000", requested: "1.0000" }, 409);
      }
      return json([]);
    });
    renderRegister(fetchMock, "fr", createKey);
    await user.type(await screen.findByLabelText("Nom, référence ou code scanner"), "EAU-1{Enter}");
    await user.type(screen.getByLabelText("Client facultatif"), "amin");
    await user.click(await screen.findByRole("button", { name: /0551000001/ }));
    await user.click(screen.getByRole("button", { name: "Encaisser" }));
    await user.click(screen.getByRole("button", { name: "Confirmer l'encaissement" }));
    expect(await screen.findByText("0.0000 / 1.0000")).toBeInTheDocument();
    await user.clear(screen.getByLabelText("Montant reçu"));
    await user.type(screen.getByLabelText("Montant reçu"), "4");
    await user.click(screen.getByRole("button", { name: "Encaisser" }));
    await user.click(screen.getByRole("button", { name: "Confirmer l'encaissement" }));
    await waitFor(() => expect(fetchMock.mock.calls.filter((call) => call[1]?.method === "POST")).toHaveLength(2));
    const keys = fetchMock.mock.calls
      .filter((call) => call[1]?.method === "POST")
      .map((call) => headerValue(call[1], "Idempotency-Key"));
    expect(keys).toEqual(["key-pay-a", "key-pay-b"]);
  });

  it.each([
    ["fr", "Montant reçu", "ltr"],
    ["ar", "المبلغ المقبوض", "rtl"],
    ["en", "Amount received", "ltr"],
  ] as const)("shows the initial payment in %s", async (locale, label, dir) => {
    const user = userEvent.setup();
    renderRegister(vi.fn(customerFetcher()), locale);
    await user.type(await screen.findByLabelText(locale === "fr" ? "Nom, référence ou code scanner" : locale === "ar" ? "الاسم أو المرجع أو قارئ الرمز" : "Name, SKU, or scanner code"), "EAU-1{Enter}");
    await user.type(screen.getByLabelText(locale === "fr" ? "Client facultatif" : locale === "ar" ? "زبون اختياري" : "Optional customer"), "amin");
    await user.click(await screen.findByRole("button", { name: /0551000001/ }));
    expect(screen.getByTestId("pos")).toHaveAttribute("dir", dir);
    expect(screen.getByLabelText(label)).toBeInTheDocument();
  });

  it("rejects a later payment that is empty, zero, or above the outstanding amount", async () => {
    const user = userEvent.setup();
    const fetchMock = collectionFetcher(async () => json({}));
    renderRegister(fetchMock);
    await user.click(await screen.findByRole("button", { name: /V-000010/ }));
    expect(await screen.findByRole("article", { name: "V-000010" })).toHaveTextContent("R-000001");
    expect(screen.getByRole("button", { name: "Enregistrer le paiement" })).toBeDisabled();

    await user.type(screen.getByLabelText("Enregistrer le paiement"), "0");
    expect(screen.getByRole("alert")).toHaveTextContent("Le paiement doit être supérieur à zéro.");
    await user.clear(screen.getByLabelText("Enregistrer le paiement"));
    await user.type(screen.getByLabelText("Enregistrer le paiement"), "7");
    expect(screen.getByRole("alert")).toHaveTextContent("Le paiement dépasse le reste à payer.");
    expect(screen.getByRole("button", { name: "Enregistrer le paiement" })).toBeDisabled();
    expect(fetchMock.mock.calls.some((call) => String(call[0]).includes("/payments"))).toBe(false);
  });

  it("shows the receipt and the updated outstanding amount after a payment", async () => {
    const user = userEvent.setup();
    const fetchMock = collectionFetcher(async (_input, init) => {
      expect(JSON.parse(String(init?.body))).toMatchObject({ method: "transfer", amount: "2.0000", reference: "VIR-2", notes: "Suite" });
      return json({
        saleId: "sale-1",
        saleNumber: "V-000010",
        paidAmount: "6.0000",
        outstandingAmount: "4.0000",
        paymentState: "partial",
        payment: {
          id: "pay-2",
          method: "transfer",
          amount: "2.0000",
          receiptNumber: "R-000002",
          reference: "VIR-2",
          notes: "Suite",
          paidAt: "2026-10-30T13:00:00Z",
          receivedBy: "user-1",
        },
      }, 201);
    });
    renderRegister(fetchMock);
    await user.click(await screen.findByRole("button", { name: /V-000010/ }));
    await user.selectOptions(await screen.findByRole("combobox", { name: "Encaisser un paiement" }), "transfer");
    await user.type(screen.getByLabelText("Enregistrer le paiement"), "2");
    await user.type(screen.getByLabelText("Référence"), "VIR-2");
    await user.type(screen.getByLabelText("Notes du paiement"), "Suite");
    await user.click(screen.getByRole("button", { name: "Enregistrer le paiement" }));
    await user.click(screen.getByRole("button", { name: "Confirmer le paiement" }));
    const article = await screen.findByRole("article", { name: "V-000010" });
    expect(article).toHaveTextContent("R-000002");
    expect(article).toHaveTextContent("Montant payé");
    expect(article).toHaveTextContent("6.0000");
    expect(article).toHaveTextContent("4.0000");
    expect(article).toHaveTextContent("Partiellement payée");
  });

  it("shows the API payment error and keeps the same key until the payment changes", async () => {
    const user = userEvent.setup();
    const keys = ["key-pay-a", "key-pay-b"];
    let attempt = 0;
    const fetchMock = collectionFetcher(async (input) => {
      if (!String(input).includes("/payments")) {
        return undefined;
      }
      attempt += 1;
      if (attempt === 1) {
        return json({ code: "payment.settled" }, 409);
      }
      return json({
        saleId: "sale-1",
        saleNumber: "V-000010",
        paidAmount: "6.0000",
        outstandingAmount: "4.0000",
        paymentState: "partial",
        payment: {
          id: `pay-${attempt}`,
          method: "cash",
          amount: attempt === 2 ? "2.0000" : "3.0000",
          receiptNumber: attempt === 2 ? "R-000002" : "R-000003",
          paidAt: "2026-10-30T13:00:00Z",
          receivedBy: "user-1",
        },
      }, 201);
    });
    renderRegister(fetchMock, "fr", () => keys.shift() ?? "key-extra");
    await user.click(await screen.findByRole("button", { name: /V-000010/ }));
    await user.type(await screen.findByLabelText("Enregistrer le paiement"), "2");
    await user.click(screen.getByRole("button", { name: "Enregistrer le paiement" }));
    await user.click(screen.getByRole("button", { name: "Confirmer le paiement" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("Cette vente est déjà soldée.");
    await user.click(screen.getByRole("button", { name: "Confirmer le paiement" }));
    await user.clear(screen.getByLabelText("Enregistrer le paiement"));
    await user.type(screen.getByLabelText("Enregistrer le paiement"), "3");
    await user.click(screen.getByRole("button", { name: "Enregistrer le paiement" }));
    await user.click(screen.getByRole("button", { name: "Confirmer le paiement" }));
    await screen.findByText("R-000003");
    const sent = fetchMock.mock.calls
      .filter((call) => String(call[0]).includes("/payments"))
      .map((call) => headerValue(call[1], "Idempotency-Key"));
    expect(sent).toEqual(["key-pay-a", "key-pay-a", "key-pay-b"]);
  });
});

function headerValue(init: RequestInit | undefined, name: string): string | undefined {
  const headers = init?.headers;
  if (!headers || headers instanceof Headers || Array.isArray(headers)) {
    return undefined;
  }

  return headers[name];
}

function customerFetcher() {
  return async (input: string, init?: RequestInit) => {
    const url = String(input);
    if (url.endsWith("/api/v1/products")) {
      return json([eau]);
    }
    if (url.endsWith("/api/v1/stock/balances")) {
      return json([]);
    }
    if (url.includes("/api/v1/customers")) {
      return json(amin);
    }
    if (init?.method === "POST") {
      return json(saleBody(), 201);
    }
    if (url.endsWith("/api/v1/sales")) {
      return json([]);
    }
    throw new Error(url);
  };
}

function partialSale() {
  return {
    ...saleBody("V-000010"),
    customerId: "c1",
    customerName: "Amin",
    paidAmount: "4.0000",
    outstandingAmount: "6.0000",
    paymentState: "partial" as const,
    payments: [
      {
        id: "pay-1",
        method: "transfer",
        amount: "4.0000",
        receiptNumber: "R-000001",
        reference: "VIR-1",
        paidAt: "2026-10-30T12:00:00Z",
      },
    ],
  };
}

function collectionFetcher(onPayment: (input: string, init?: RequestInit) => Promise<Response | undefined>) {
  return vi.fn(async (input: string, init?: RequestInit) => {
    const url = String(input);
    if (url.endsWith("/api/v1/products")) {
      return json([]);
    }
    if (url.endsWith("/api/v1/stock/balances")) {
      return json([]);
    }
    if (url.endsWith("/api/v1/sales/sale-1")) {
      return json(partialSale());
    }
    if (url.endsWith("/api/v1/sales")) {
      return json([{ id: "sale-1", number: "V-000010", total: "10.0000", createdAt: "2026-10-30T12:00:00Z" }]);
    }
    if (url.includes("/payments")) {
      const response = await onPayment(input, init);
      if (response) {
        return response;
      }
    }
    throw new Error(url);
  });
}

function catalogFetcher(products: unknown[], balances: unknown[] = [], posted?: unknown) {
  return async (input: string, init?: RequestInit) => {
    const url = String(input);
    if (url.endsWith("/api/v1/products")) {
      return json(products);
    }
    if (url.endsWith("/api/v1/stock/balances")) {
      return json(balances);
    }
    if (init?.method === "POST") {
      return json(posted ?? saleBody(), 201);
    }
    if (url.endsWith("/api/v1/sales")) {
      return json([]);
    }
    throw new Error(url);
  };
}
