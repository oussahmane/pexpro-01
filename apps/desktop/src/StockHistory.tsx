import { useEffect, useState, type ReactNode } from "react";
import { translate, type Locale } from "./i18n";

type Fetcher = (input: string, init?: RequestInit) => Promise<Response>;

export type StockHistoryProduct = {
  productId: string;
  sku: string;
  name: string;
};

type MovementRow = {
  id: string;
  productId: string;
  movementType: string;
  quantityDelta: string;
  reason: string;
  balanceAfter: string;
  createdAt: string;
  createdBy: string;
};

type StockHistoryProps = {
  apiBaseUrl: string;
  accessToken: string;
  fetcher: Fetcher;
  locale: Locale;
  products: StockHistoryProduct[];
  refreshKey?: number;
};

const knownTypes = ["opening", "receipt", "adjustment", "sale", "sale_return"] as const;

export function LtrValue({ children }: { children: ReactNode }) {
  return (
    <bdi dir="ltr" style={{ unicodeBidi: "isolate" }}>
      {children}
    </bdi>
  );
}

function movementTypeLabel(type: string, text: (key: string) => string): string {
  if (type === "opening") return text("stock.opening");
  if (type === "receipt") return text("stock.receipt");
  if (type === "adjustment") return text("stock.adjustment");
  if (type === "sale") return text("stock.sale");
  if (type === "sale_return") return text("stock.saleReturn");
  return type;
}

function productLabel(productId: string, products: StockHistoryProduct[]): string {
  const product = products.find((item) => item.productId === productId);
  return product ? `${product.sku} · ${product.name}` : productId;
}

function formatTimestamp(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }
  const pad = (part: number) => String(part).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

export function StockHistory({
  apiBaseUrl,
  accessToken,
  fetcher,
  locale,
  products,
  refreshKey = 0,
}: StockHistoryProps) {
  const text = (key: string) => translate(locale, key);
  const [productFilter, setProductFilter] = useState("");
  const [typeFilter, setTypeFilter] = useState("");
  const [movements, setMovements] = useState<MovementRow[]>([]);
  const [status, setStatus] = useState<"loading" | "ready" | "error">("loading");

  useEffect(() => {
    let cancelled = false;
    async function load() {
      setStatus("loading");
      const query = productFilter ? `?productId=${encodeURIComponent(productFilter)}` : "";
      try {
        const response = await fetcher(`${apiBaseUrl}/api/v1/stock/movements${query}`, {
          headers: { authorization: `Bearer ${accessToken}` },
        });
        if (cancelled) {
          return;
        }
        if (!response.ok) {
          setMovements([]);
          setStatus("error");
          return;
        }
        setMovements((await response.json()) as MovementRow[]);
        setStatus("ready");
      } catch {
        if (!cancelled) {
          setMovements([]);
          setStatus("error");
        }
      }
    }
    void load();
    return () => {
      cancelled = true;
    };
  }, [accessToken, apiBaseUrl, fetcher, productFilter, refreshKey]);

  const visible = movements.filter((movement) => typeFilter === "" || movement.movementType === typeFilter);

  return (
    <section className="flex flex-col gap-3" data-testid="stock-history">
      <h3 className="text-lg font-semibold">{text("stock.history")}</h3>
      <div className="flex flex-col gap-3 sm:flex-row">
        <label className="flex flex-1 flex-col gap-1 text-sm">
          <span>{text("stock.product")}</span>
          <select
            className="w-full border border-stone-400 bg-white px-2 py-1"
            value={productFilter}
            onChange={(event) => setProductFilter(event.target.value)}
          >
            <option value="">{text("stock.history.allProducts")}</option>
            {products.map((product) => (
              <option key={product.productId} value={product.productId}>
                {product.sku} · {product.name}
              </option>
            ))}
          </select>
        </label>
        <label className="flex flex-1 flex-col gap-1 text-sm">
          <span>{text("stock.type")}</span>
          <select
            className="w-full border border-stone-400 bg-white px-2 py-1"
            value={typeFilter}
            onChange={(event) => setTypeFilter(event.target.value)}
          >
            <option value="">{text("stock.history.allTypes")}</option>
            {knownTypes.map((type) => (
              <option key={type} value={type}>
                {movementTypeLabel(type, text)}
              </option>
            ))}
          </select>
        </label>
      </div>
      {status === "loading" ? <p>{text("stock.history.loading")}</p> : null}
      {status === "error" ? <p role="alert">{text("stock.history.error")}</p> : null}
      {status === "ready" && visible.length === 0 ? <p>{text("stock.history.empty")}</p> : null}
      {status === "ready" && visible.length > 0 ? (
        <ul className="flex flex-col gap-3" data-testid="stock-history-list">
          {visible.map((movement) => (
            <li key={movement.id} className="border border-stone-300 bg-white px-3 py-2 text-sm">
              <p>
                {text("stock.product")}: <span>{productLabel(movement.productId, products)}</span>
              </p>
              <p>
                {text("stock.type")}: <span>{movementTypeLabel(movement.movementType, text)}</span>
              </p>
              <p>
                {text("stock.quantity")}: <LtrValue>{movement.quantityDelta}</LtrValue>
              </p>
              <p>
                {text("stock.history.balance")}: <LtrValue>{movement.balanceAfter}</LtrValue>
              </p>
              <p>
                {text("stock.reason")}: <span>{movement.reason}</span>
              </p>
              <p>
                {text("stock.history.user")}: <LtrValue>{movement.createdBy}</LtrValue>
              </p>
              <p>
                {text("stock.history.when")}: <LtrValue>{formatTimestamp(movement.createdAt)}</LtrValue>
              </p>
            </li>
          ))}
        </ul>
      ) : null}
    </section>
  );
}
