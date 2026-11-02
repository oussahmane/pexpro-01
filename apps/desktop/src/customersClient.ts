export type CustomerType = "individual" | "business";

export type CustomerRow = {
  id: string;
  type: CustomerType;
  name: string;
  phone: string | null;
  address: string | null;
  notes: string | null;
  rc: string | null;
  nif: string | null;
  nis: string | null;
  ai: string | null;
  creditLimit: string | null;
  isActive: boolean;
  outstandingAmount?: string;
};

export type CustomerStatementLine = {
  occurredAt: string;
  kind: string;
  documentId: string;
  documentNumber: string | null;
  amount: string;
  runningBalance: string;
};

export type CustomerStatement = {
  customerId: string;
  customerName: string;
  timeZoneId: string;
  from: string;
  to: string;
  openingBalance: string;
  closingBalance: string;
  outstandingAmount: string;
  lines: CustomerStatementLine[];
};

type Fetcher = (input: string, init?: RequestInit) => Promise<Response>;

export type CustomerInput = {
  type: CustomerType;
  name: string;
  phone?: string;
  address?: string;
  notes?: string;
  rc?: string;
  nif?: string;
  nis?: string;
  ai?: string;
  creditLimit?: string;
  isActive?: boolean;
};

export function listCustomers(
  fetcher: Fetcher,
  apiBaseUrl: string,
  accessToken: string,
  query: string,
): Promise<Response> {
  const suffix = query.trim() ? `?q=${encodeURIComponent(query.trim())}` : "";
  return fetcher(`${apiBaseUrl}/api/v1/customers${suffix}`, {
    headers: { authorization: `Bearer ${accessToken}` },
  });
}

export function createCustomer(
  fetcher: Fetcher,
  apiBaseUrl: string,
  accessToken: string,
  idempotencyKey: string,
  customer: CustomerInput,
): Promise<Response> {
  return fetcher(`${apiBaseUrl}/api/v1/customers`, {
    method: "POST",
    headers: {
      authorization: `Bearer ${accessToken}`,
      "content-type": "application/json",
      "Idempotency-Key": idempotencyKey,
    },
    body: JSON.stringify(customer),
  });
}

export function formatStatementInstant(instant: string, timeZoneId: string): string {
  const date = new Date(instant);
  if (Number.isNaN(date.getTime())) {
    return instant;
  }

  try {
    const parts = new Intl.DateTimeFormat("en-US", {
      timeZone: timeZoneId,
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
      hourCycle: "h23",
    }).formatToParts(date);
    const value = (type: Intl.DateTimeFormatPartTypes) => parts.find((part) => part.type === type)?.value ?? "";
    const hour = value("hour") === "24" ? "00" : value("hour");
    return `${value("year")}-${value("month")}-${value("day")} ${hour}:${value("minute")}`;
  } catch {
    return instant;
  }
}

export function exclusiveStatementTo(through: string): string | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(through);
  if (!match) {
    return null;
  }

  const year = Number(match[1]);
  const month = Number(match[2]);
  const day = Number(match[3]);
  const date = new Date(Date.UTC(year, month - 1, day));
  if (date.getUTCFullYear() !== year || date.getUTCMonth() !== month - 1 || date.getUTCDate() !== day) {
    return null;
  }

  date.setUTCDate(date.getUTCDate() + 1);
  return date.toISOString().slice(0, 10);
}

export function getCustomerStatement(
  fetcher: Fetcher,
  apiBaseUrl: string,
  accessToken: string,
  customerId: string,
  from?: string,
  to?: string,
): Promise<Response> {
  const params = new URLSearchParams();
  if (from) {
    params.set("from", from);
  }
  if (to) {
    params.set("to", to);
  }
  const suffix = params.size > 0 ? `?${params.toString()}` : "";
  return fetcher(`${apiBaseUrl}/api/v1/customers/${customerId}/statement${suffix}`, {
    headers: { authorization: `Bearer ${accessToken}` },
  });
}

export function updateCustomer(
  fetcher: Fetcher,
  apiBaseUrl: string,
  accessToken: string,
  customerId: string,
  customer: CustomerInput,
): Promise<Response> {
  return fetcher(`${apiBaseUrl}/api/v1/customers/${customerId}`, {
    method: "PUT",
    headers: {
      authorization: `Bearer ${accessToken}`,
      "content-type": "application/json",
    },
    body: JSON.stringify(customer),
  });
}
