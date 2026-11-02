export type CashSaleLine = {
  productId: string;
  quantity: string;
  priceKind: "retail" | "wholesale";
  unitPrice: string;
  lineDiscount: string;
  discountKind: "none" | "amount" | "percent";
  discountRate?: string;
  lineTotal: string;
};

export type SalePaymentMethod = "cash" | "transfer" | "card" | "other";

export type CashSaleRequest = {
  lines: CashSaleLine[];
  total: string;
  discountAmount: string;
  discountKind: "none" | "amount" | "percent";
  discountRate?: string;
  payments: { method: SalePaymentMethod; amount: string }[];
  customerId?: string;
};

type Fetcher = (input: string, init?: RequestInit) => Promise<Response>;

export function cashSaleRequest(
  lines: CashSaleLine[],
  total: string,
  discount: { amount: string; kind: CashSaleRequest["discountKind"]; rate?: string } = {
    amount: "0.0000",
    kind: "none",
  },
  customerId?: string,
  initialPayment?: { method: SalePaymentMethod; amount: string } | null,
): CashSaleRequest {
  const payments = !customerId
    ? [{ method: "cash" as const, amount: total }]
    : initialPayment
      ? [initialPayment]
      : initialPayment === null
        ? []
        : [{ method: "cash" as const, amount: total }];
  return {
    lines,
    total,
    discountAmount: discount.amount,
    discountKind: discount.kind,
    discountRate: discount.rate,
    payments,
    ...(customerId ? { customerId } : {}),
  };
}

export function finalizeCashSale(
  fetcher: Fetcher,
  apiBaseUrl: string,
  accessToken: string,
  idempotencyKey: string,
  sale: CashSaleRequest,
): Promise<Response> {
  return fetcher(`${apiBaseUrl}/api/v1/sales`, {
    method: "POST",
    headers: {
      authorization: `Bearer ${accessToken}`,
      "content-type": "application/json",
      "Idempotency-Key": idempotencyKey,
    },
    body: JSON.stringify(sale),
  });
}

export function listSales(fetcher: Fetcher, apiBaseUrl: string, accessToken: string): Promise<Response> {
  return fetcher(`${apiBaseUrl}/api/v1/sales`, {
    headers: { authorization: `Bearer ${accessToken}` },
  });
}

export function postSalePayment(
  fetcher: Fetcher,
  apiBaseUrl: string,
  accessToken: string,
  saleId: string,
  idempotencyKey: string,
  payment: { method: SalePaymentMethod; amount: string; reference?: string; notes?: string },
): Promise<Response> {
  return fetcher(`${apiBaseUrl}/api/v1/sales/${saleId}/payments`, {
    method: "POST",
    headers: {
      authorization: `Bearer ${accessToken}`,
      "content-type": "application/json",
      "Idempotency-Key": idempotencyKey,
    },
    body: JSON.stringify(payment),
  });
}

export function getSale(
  fetcher: Fetcher,
  apiBaseUrl: string,
  accessToken: string,
  saleId: string,
): Promise<Response> {
  return fetcher(`${apiBaseUrl}/api/v1/sales/${saleId}`, {
    headers: { authorization: `Bearer ${accessToken}` },
  });
}
