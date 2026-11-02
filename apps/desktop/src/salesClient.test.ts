import { describe, expect, it, vi } from "vitest";
import { cashSaleRequest, finalizeCashSale, getSale, listSales } from "./salesClient";

describe("salesClient", () => {
  it("sends one cash payment for the sale total and the idempotency key", async () => {
    const fetcher = vi.fn(async () => new Response("{}", { status: 201 }));
    const sale = cashSaleRequest(
      [{
        productId: "product-1",
        quantity: "2.0000",
        priceKind: "retail",
        unitPrice: "10.0000",
        lineDiscount: "0.0000",
        discountKind: "none",
        lineTotal: "20.0000",
      }],
      "20.0000",
    );

    await finalizeCashSale(fetcher, "http://127.0.0.1:5080", "token", "key-1", sale);

    expect(fetcher).toHaveBeenCalledWith("http://127.0.0.1:5080/api/v1/sales", {
      method: "POST",
      headers: {
        authorization: "Bearer token",
        "content-type": "application/json",
        "Idempotency-Key": "key-1",
      },
      body: JSON.stringify(sale),
    });
    expect(sale.payments).toEqual([{ method: "cash", amount: "20.0000" }]);
  });

  it("reads sale history and one sale", async () => {
    const fetcher = vi.fn(async () => new Response("[]", { status: 200 }));
    await listSales(fetcher, "http://127.0.0.1:5080", "token");
    await getSale(fetcher, "http://127.0.0.1:5080", "token", "sale-1");

    expect(fetcher).toHaveBeenNthCalledWith(1, "http://127.0.0.1:5080/api/v1/sales", {
      headers: { authorization: "Bearer token" },
    });
    expect(fetcher).toHaveBeenNthCalledWith(2, "http://127.0.0.1:5080/api/v1/sales/sale-1", {
      headers: { authorization: "Bearer token" },
    });
  });
});
