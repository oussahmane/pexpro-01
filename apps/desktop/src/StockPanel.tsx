import { useEffect, useState, type FormEvent } from "react";
import { translate, type Locale } from "./i18n";
import { LtrValue, StockHistory } from "./StockHistory";

type Fetcher = (input: string, init?: RequestInit) => Promise<Response>;

type BalanceRow = {
  productId: string;
  sku: string;
  name: string;
  unit: string;
  quantity: string;
  minimumQuantity: string | null;
  belowMinimum: boolean;
  atMinimum: boolean;
};

type StockPanelProps = {
  apiBaseUrl: string;
  accessToken: string;
  fetcher: Fetcher;
  locale: Locale;
};

type MovementType = "opening" | "receipt" | "adjustment";

function accessRole(token: string): string | null {
  const segment = token.split(".")[1];
  if (!segment) {
    return null;
  }

  try {
    const base64 = segment.replace(/-/g, "+").replace(/_/g, "/");
    const padded = base64 + "=".repeat((4 - (base64.length % 4)) % 4);
    const payload = JSON.parse(atob(padded)) as { role?: string | string[] };
    if (typeof payload.role === "string") {
      return payload.role;
    }
    return payload.role?.[0] ?? null;
  } catch {
    return null;
  }
}

export function StockPanel({ apiBaseUrl, accessToken, fetcher, locale }: StockPanelProps) {
  const text = (key: string) => translate(locale, key);
  const canAdjust = accessRole(accessToken) === "Administrator";
  const [balances, setBalances] = useState<BalanceRow[]>([]);
  const [productId, setProductId] = useState("");
  const [movementType, setMovementType] = useState<MovementType>("adjustment");
  const [quantity, setQuantity] = useState("");
  const [reason, setReason] = useState("");
  const [minimum, setMinimum] = useState("");
  const [errorCode, setErrorCode] = useState<string | null>(null);
  const [historyVersion, setHistoryVersion] = useState(0);

  useEffect(() => {
    let cancelled = false;
    async function load() {
      const response = await fetcher(`${apiBaseUrl}/api/v1/stock/balances`, {
        headers: { authorization: `Bearer ${accessToken}` },
      });
      if (cancelled || !response.ok) {
        return;
      }
      const rows = (await response.json()) as BalanceRow[];
      setBalances(rows);
      setProductId((current) => current || rows[0]?.productId || "");
    }
    void load();
    return () => {
      cancelled = true;
    };
  }, [accessToken, apiBaseUrl, fetcher]);

  async function reload() {
    const response = await fetcher(`${apiBaseUrl}/api/v1/stock/balances`, {
      headers: { authorization: `Bearer ${accessToken}` },
    });
    if (response.ok) {
      setBalances((await response.json()) as BalanceRow[]);
    }
  }

  async function submitMovement(event: FormEvent) {
    event.preventDefault();
    setErrorCode(null);
    if (reason.trim().length === 0) {
      setErrorCode("reason.required");
      return;
    }
    if (quantity.trim().length === 0 || productId.length === 0) {
      setErrorCode("quantity.required");
      return;
    }

    const path =
      movementType === "opening"
        ? "/api/v1/stock/opening"
        : movementType === "receipt"
          ? "/api/v1/stock/receipts"
          : "/api/v1/stock/adjustments";
    const response = await fetcher(`${apiBaseUrl}${path}`, {
      method: "POST",
      headers: {
        authorization: `Bearer ${accessToken}`,
        "content-type": "application/json",
        "Idempotency-Key": crypto.randomUUID(),
      },
      body: JSON.stringify({ productId, quantity, reason }),
    });
    if (!response.ok) {
      const problem = (await response.json()) as { code?: string; errors?: Record<string, string[]> };
      const fieldCode = problem.errors ? Object.values(problem.errors)[0]?.[0] : undefined;
      setErrorCode(fieldCode ?? problem.code ?? "validation.failed");
      return;
    }

    setReason("");
    setQuantity("");
    setHistoryVersion((current) => current + 1);
    await reload();
  }

  async function submitMinimum(event: FormEvent) {
    event.preventDefault();
    setErrorCode(null);
    if (minimum.trim().length === 0 || productId.length === 0) {
      setErrorCode("quantity.required");
      return;
    }

    const response = await fetcher(`${apiBaseUrl}/api/v1/stock/balances/${productId}/minimum`, {
      method: "PATCH",
      headers: {
        authorization: `Bearer ${accessToken}`,
        "content-type": "application/json",
      },
      body: JSON.stringify({ minimumQuantity: minimum }),
    });
    if (!response.ok) {
      const problem = (await response.json()) as { code?: string; errors?: Record<string, string[]> };
      const fieldCode = problem.errors ? Object.values(problem.errors)[0]?.[0] : undefined;
      setErrorCode(fieldCode ?? problem.code ?? "validation.failed");
      return;
    }

    setMinimum("");
    await reload();
  }

  return (
    <section className="flex flex-col gap-3" data-testid="stock">
      <h2 className="text-xl font-semibold">{text("stock.title")}</h2>
      {balances.length === 0 ? <p>{text("stock.empty")}</p> : null}
      <ul className="flex flex-col gap-1">
        {balances.map((balance) => (
          <li key={balance.productId}>
            {balance.sku} · {balance.name} · <LtrValue>{balance.quantity}</LtrValue>
            {balance.minimumQuantity ? (
              <>
                {" "}
                · {text("stock.minimum")} <LtrValue>{balance.minimumQuantity}</LtrValue>
              </>
            ) : null}
            {balance.belowMinimum ? ` · ${text("stock.belowMinimum")}` : ""}
            {balance.atMinimum ? ` · ${text("stock.atMinimum")}` : ""}
          </li>
        ))}
      </ul>
      {canAdjust ? (
        <>
          <form className="flex flex-col gap-3" onSubmit={(event) => void submitMovement(event)}>
            <label className="flex flex-col gap-1 text-sm">
              <span>{text("stock.product")}</span>
              <select className="w-full border border-stone-400 px-2 py-1" value={productId} onChange={(event) => setProductId(event.target.value)}>
                {balances.map((balance) => (
                  <option key={balance.productId} value={balance.productId}>
                    {balance.sku} · {balance.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="flex flex-col gap-1 text-sm">
              <span>{text("stock.type")}</span>
              <select
                className="w-full border border-stone-400 px-2 py-1"
                value={movementType}
                onChange={(event) => setMovementType(event.target.value as MovementType)}
              >
                <option value="opening">{text("stock.opening")}</option>
                <option value="receipt">{text("stock.receipt")}</option>
                <option value="adjustment">{text("stock.adjustment")}</option>
              </select>
            </label>
            <label className="flex flex-col gap-1 text-sm">
              <span>{text("stock.quantity")}</span>
              <input
                dir="ltr"
                className="w-full border border-stone-400 px-2 py-1 text-start"
                value={quantity}
                onChange={(event) => setQuantity(event.target.value)}
              />
            </label>
            <label className="flex flex-col gap-1 text-sm">
              <span>{text("stock.reason")}</span>
              <input className="w-full border border-stone-400 px-2 py-1" value={reason} onChange={(event) => setReason(event.target.value)} />
            </label>
            {errorCode ? <p role="alert">{text(errorCode)}</p> : null}
            <button type="submit" className="w-fit border border-stone-500 bg-white px-3 py-1.5 text-sm">
              {text("stock.save")}
            </button>
          </form>
          <form className="flex flex-col gap-3" onSubmit={(event) => void submitMinimum(event)}>
            <label className="flex flex-col gap-1 text-sm">
              <span>{text("stock.minimum")}</span>
              <input
                dir="ltr"
                className="w-full border border-stone-400 px-2 py-1 text-start"
                value={minimum}
                onChange={(event) => setMinimum(event.target.value)}
              />
            </label>
            <button type="submit" className="w-fit border border-stone-500 bg-white px-3 py-1.5 text-sm">
              {text("stock.saveMinimum")}
            </button>
          </form>
        </>
      ) : null}
      <StockHistory
        apiBaseUrl={apiBaseUrl}
        accessToken={accessToken}
        fetcher={fetcher}
        locale={locale}
        products={balances}
        refreshKey={historyVersion}
      />
    </section>
  );
}
