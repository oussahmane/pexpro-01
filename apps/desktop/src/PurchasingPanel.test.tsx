import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { PurchasingPanel } from "./PurchasingPanel";
import type { Locale } from "./i18n";

afterEach(() => {
  document.documentElement.lang = "fr";
  document.documentElement.dir = "ltr";
});

describe("PurchasingPanel", () => {
  it.each([
    ["fr", "Fournisseurs", "Achats"],
    ["en", "Suppliers", "Purchases"],
    ["ar", "الموردون", "المشتريات"],
  ] as const)("translates the headings in %s", async (locale: Locale, suppliers, purchases) => {
    render(
      <PurchasingPanel
        apiBaseUrl="http://127.0.0.1:5080"
        accessToken={token("Cashier")}
        fetcher={emptyFetcher()}
        locale={locale}
      />,
    );

    expect(await screen.findByRole("heading", { name: suppliers })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: purchases })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /brouillon|draft|المسودة/i })).not.toBeInTheDocument();
  });

  it("keeps purchase figures left to right and does not post stock", async () => {
    document.documentElement.lang = "ar";
    document.documentElement.dir = "rtl";
    const user = userEvent.setup();
    const calls: string[] = [];
    const fetcher = vi.fn(async (input: string, init?: RequestInit) => {
      const url = String(input);
      calls.push(`${init?.method ?? "GET"} ${url}`);
      if (url.endsWith("/api/v1/products")) {
        return json([
          {
            id: "p1",
            sku: "EAU-1",
            name: "Eau",
            unit: "u",
            unitPrice: "19.5000",
            isActive: true,
            trackStock: true,
          },
        ]);
      }
      if (url.endsWith("/api/v1/suppliers")) {
        return json([
          {
            id: "s1",
            name: "Atlas",
            taxId: null,
            isActive: true,
            contactName: null,
            phone: null,
            email: null,
            address: null,
            notes: null,
          },
        ]);
      }
      if (url.endsWith("/api/v1/purchases") && init?.method === "POST") {
        return json(
          {
            id: "d1",
            number: "A-000001",
            status: "draft",
            supplierId: "s1",
            supplierName: "Atlas",
            supplierTaxId: null,
            supplierDocumentRef: null,
            notes: null,
            version: 1,
            lines: [
              {
                productId: "p1",
                productSku: "EAU-1",
                productName: "Eau",
                productUnit: "u",
                quantityOrdered: "1.5000",
                quantityReceived: "0.0000",
                unitCost: "4.2500",
                sellingPrice: "19.5000",
              },
            ],
          },
          201,
        );
      }
      if (url.includes("/api/v1/purchases")) {
        return json([]);
      }
      throw new Error(url);
    });

    render(
      <PurchasingPanel
        apiBaseUrl="http://127.0.0.1:5080"
        accessToken={token("Administrator")}
        fetcher={fetcher}
        locale="ar"
      />,
    );
    await screen.findByRole("heading", { name: "المشتريات" });
    await user.selectOptions(screen.getByLabelText("المورد"), "s1");
    await user.click(screen.getByRole("button", { name: "إضافة سطر" }));
    await user.selectOptions(screen.getByLabelText("منتج الشراء"), "p1");
    await user.type(screen.getByLabelText("الكمية المطلوبة"), "1.5000");
    await user.type(screen.getByLabelText("تكلفة الشراء"), "4.2500");

    expect(screen.getByLabelText("الكمية المطلوبة")).toHaveAttribute("dir", "ltr");
    expect(screen.getByLabelText("تكلفة الشراء")).toHaveAttribute("dir", "ltr");
    expect(screen.getByText("19.5000")).toHaveAttribute("dir", "ltr");
    await user.click(screen.getByRole("button", { name: "حفظ المسودة" }));
    await user.click(await screen.findByRole("button", { name: "حذف المسودة" }));

    expect(calls.some((call) => call.startsWith("POST") && call.includes("/api/v1/purchases"))).toBe(true);
    expect(calls.some((call) => call.startsWith("DELETE") && call.includes("/api/v1/purchases/d1?version=1"))).toBe(true);
    expect(calls.some((call) => call.includes("/api/v1/stock/"))).toBe(false);
  });

  it("orders a draft without posting stock", async () => {
    const user = userEvent.setup();
    const calls: string[] = [];
    const fetcher = vi.fn(async (input: string, init?: RequestInit) => {
      const url = String(input);
      calls.push(`${init?.method ?? "GET"} ${url}`);
      if (url.endsWith("/api/v1/products") || url.endsWith("/api/v1/suppliers")) {
        return json([]);
      }
      if (url.endsWith("/api/v1/purchases")) {
        return json([
          {
            id: "d1",
            number: "A-000001",
            status: "draft",
            supplierId: "s1",
            supplierName: "Atlas",
            supplierDocumentRef: null,
          },
        ]);
      }
      if (url.endsWith("/api/v1/purchases/d1") && (init?.method ?? "GET") === "GET") {
        return json(draftDetail());
      }
      if (url.endsWith("/api/v1/purchases/d1/order")) {
        return json({ ...draftDetail(), status: "ordered" });
      }
      throw new Error(url);
    });

    render(
      <PurchasingPanel
        apiBaseUrl="http://127.0.0.1:5080"
        accessToken={token("Administrator")}
        fetcher={fetcher}
        locale="fr"
      />,
    );
    await user.click(await screen.findByRole("button", { name: /A-000001/ }));
    await user.click(await screen.findByRole("button", { name: "Commander" }));

    const order = calls.find((call) => call.includes("/order"));
    expect(order).toContain("POST");
    expect(calls.some((call) => call.includes("/api/v1/stock/"))).toBe(false);
  });

  it("omits a blank receipt line", async () => {
    const user = userEvent.setup();
    let receiptBody = "";
    const fetcher = vi.fn(async (input: string, init?: RequestInit) => {
      const url = String(input);
      if (url.endsWith("/api/v1/products") || url.endsWith("/api/v1/suppliers")) {
        return json([]);
      }
      if (url.endsWith("/api/v1/purchases")) {
        return json([
          {
            id: "d1",
            number: "A-000002",
            status: "ordered",
            supplierId: "s1",
            supplierName: "Atlas",
            supplierDocumentRef: null,
          },
        ]);
      }
      if (url.endsWith("/receipts") && init?.method === "POST") {
        receiptBody = String(init.body);
        return json({ id: "r1" }, 201);
      }
      if (url.includes("/api/v1/purchases/d1")) {
        return json(orderedDetail());
      }
      throw new Error(url);
    });

    render(
      <PurchasingPanel
        apiBaseUrl="http://127.0.0.1:5080"
        accessToken={token("Administrator")}
        fetcher={fetcher}
        locale="en"
      />,
    );
    await user.click(await screen.findByRole("button", { name: /A-000002/ }));
    await user.type(screen.getByLabelText("Quantity received · Eau"), "1.0000");
    await user.type(screen.getByLabelText("Receipt reference"), "BL-1");
    await user.click(screen.getByRole("button", { name: "Receive" }));

    const body = JSON.parse(receiptBody) as { lines: { purchaseLineId: string; quantity: string }[] };
    expect(body.lines).toEqual([{ purchaseLineId: "line-1", quantity: "1.0000" }]);
  });

  it("hides ordering and receiving from a cashier", async () => {
    const fetcher = vi.fn(async (input: string) => {
      const url = String(input);
      if (url.endsWith("/api/v1/purchases")) {
        return json([
          {
            id: "d1",
            number: "A-000003",
            status: "ordered",
            supplierId: "s1",
            supplierName: "Atlas",
            supplierDocumentRef: null,
          },
        ]);
      }
      if (url.includes("/api/v1/purchases/d1")) {
        return json(orderedDetail());
      }
      return json([]);
    });

    render(
      <PurchasingPanel
        apiBaseUrl="http://127.0.0.1:5080"
        accessToken={token("Cashier")}
        fetcher={fetcher}
        locale="en"
      />,
    );
    await userEvent.setup().click(await screen.findByRole("button", { name: /A-000003/ }));

    expect(screen.queryByRole("button", { name: "Order" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Receive" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Cancel order" })).not.toBeInTheDocument();
    expect(await screen.findByRole("heading", { name: "Receipt history" })).toBeInTheDocument();
  });
});

function draftDetail() {
  return {
    id: "d1",
    number: "A-000001",
    status: "draft",
    supplierId: "s1",
    supplierName: "Atlas",
    supplierTaxId: null,
    supplierDocumentRef: null,
    notes: null,
    version: 4,
    lines: [
      {
        id: "line-1",
        productId: "p1",
        productSku: "EAU-1",
        productName: "Eau",
        productUnit: "u",
        quantityOrdered: "2.0000",
        quantityReceived: "0.0000",
        unitCost: "1.0000",
        sellingPrice: "2.0000",
      },
    ],
    receipts: [],
  };
}

function orderedDetail() {
  return {
    ...draftDetail(),
    number: "A-000002",
    status: "ordered",
    lines: [
      draftDetail().lines[0],
      {
        ...draftDetail().lines[0],
        id: "line-2",
        productSku: "FAR-1",
        productName: "Farine",
      },
    ],
  };
}

function token(role: string): string {
  return `e.${btoa(JSON.stringify({ role }))}.s`;
}

function emptyFetcher() {
  return vi.fn(async () => json([]));
}

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json" },
  });
}
