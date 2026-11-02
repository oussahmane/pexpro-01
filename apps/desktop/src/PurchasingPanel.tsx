import { useEffect, useState, type FormEvent } from "react";
import { translate, type Locale } from "./i18n";
import { LtrValue } from "./StockHistory";

type Fetcher = (input: string, init?: RequestInit) => Promise<Response>;

type ProductRow = {
  id: string;
  sku: string;
  name: string;
  unit: string;
  unitPrice: string;
  isActive?: boolean;
  trackStock?: boolean;
};

type SupplierRow = {
  id: string;
  name: string;
  contactName: string | null;
  phone: string | null;
  email: string | null;
  address: string | null;
  taxId: string | null;
  notes: string | null;
  isActive: boolean;
};

type PurchaseSummary = {
  id: string;
  number: string;
  status: string;
  supplierId: string;
  supplierName: string;
  supplierDocumentRef: string | null;
};

type PurchaseLine = {
  id: string;
  productId: string;
  productSku: string;
  productName: string;
  productUnit: string;
  quantityOrdered: string;
  quantityReceived: string;
  unitCost: string;
  sellingPrice: string;
};

type PurchaseReceiptLine = {
  id: string;
  purchaseLineId: string;
  quantity: string;
};

type PurchaseReceipt = {
  id: string;
  number: string;
  documentRef: string;
  lines: PurchaseReceiptLine[];
};

type PurchaseDetail = PurchaseSummary & {
  supplierTaxId: string | null;
  notes: string | null;
  version: number;
  lines: PurchaseLine[];
  receipts?: PurchaseReceipt[];
};

type DraftLine = {
  productId: string;
  quantity: string;
  unitCost: string;
};

type PurchasingPanelProps = {
  apiBaseUrl: string;
  accessToken: string;
  fetcher: Fetcher;
  locale: Locale;
};

type ProblemBody = {
  code?: string;
  errors?: Record<string, string[]>;
};

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

export function PurchasingPanel({ apiBaseUrl, accessToken, fetcher, locale }: PurchasingPanelProps) {
  const text = (key: string) => translate(locale, key);
  const canWrite = accessRole(accessToken) === "Administrator";
  const headers = { authorization: `Bearer ${accessToken}` };
  const [products, setProducts] = useState<ProductRow[]>([]);
  const [suppliers, setSuppliers] = useState<SupplierRow[]>([]);
  const [purchases, setPurchases] = useState<PurchaseSummary[]>([]);
  const [supplierName, setSupplierName] = useState("");
  const [taxId, setTaxId] = useState("");
  const [supplierActive, setSupplierActive] = useState(true);
  const [editingSupplierId, setEditingSupplierId] = useState<string | null>(null);
  const [supplierId, setSupplierId] = useState("");
  const [documentRef, setDocumentRef] = useState("");
  const [notes, setNotes] = useState("");
  const [lines, setLines] = useState<DraftLine[]>([]);
  const [editingPurchase, setEditingPurchase] = useState<PurchaseDetail | null>(null);
  const [receiveDocument, setReceiveDocument] = useState("");
  const [receiveQuantities, setReceiveQuantities] = useState<Record<string, string>>({});
  const [errorCode, setErrorCode] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    async function load() {
      const [productResponse, supplierResponse, purchaseResponse] = await Promise.all([
        fetcher(`${apiBaseUrl}/api/v1/products`, { headers }),
        fetcher(`${apiBaseUrl}/api/v1/suppliers`, { headers }),
        fetcher(`${apiBaseUrl}/api/v1/purchases`, { headers }),
      ]);
      if (cancelled) {
        return;
      }
      if (productResponse.ok) {
        setProducts((await productResponse.json()) as ProductRow[]);
      }
      if (supplierResponse.ok) {
        setSuppliers((await supplierResponse.json()) as SupplierRow[]);
      }
      if (purchaseResponse.ok) {
        setPurchases((await purchaseResponse.json()) as PurchaseSummary[]);
      }
    }

    void load();
    return () => {
      cancelled = true;
    };
  }, [accessToken, apiBaseUrl, fetcher]);

  async function reloadLists() {
    const [supplierResponse, purchaseResponse] = await Promise.all([
      fetcher(`${apiBaseUrl}/api/v1/suppliers`, { headers }),
      fetcher(`${apiBaseUrl}/api/v1/purchases`, { headers }),
    ]);
    if (supplierResponse.ok) {
      setSuppliers((await supplierResponse.json()) as SupplierRow[]);
    }
    if (purchaseResponse.ok) {
      setPurchases((await purchaseResponse.json()) as PurchaseSummary[]);
    }
  }

  async function saveSupplier(event: FormEvent) {
    event.preventDefault();
    setErrorCode(null);
    const body = { name: supplierName, taxId, isActive: supplierActive };
    const response = editingSupplierId
      ? await fetcher(`${apiBaseUrl}/api/v1/suppliers/${editingSupplierId}`, {
          method: "PUT",
          headers: { ...headers, "content-type": "application/json" },
          body: JSON.stringify(body),
        })
      : await fetcher(`${apiBaseUrl}/api/v1/suppliers`, {
          method: "POST",
          headers: {
            ...headers,
            "content-type": "application/json",
            "Idempotency-Key": crypto.randomUUID(),
          },
          body: JSON.stringify(body),
        });
    if (!response.ok) {
      setErrorCode((await problemCode(response)) ?? "validation.failed");
      return;
    }
    setSupplierName("");
    setTaxId("");
    setSupplierActive(true);
    setEditingSupplierId(null);
    await reloadLists();
  }

  function beginSupplierEdit(supplier: SupplierRow) {
    setEditingSupplierId(supplier.id);
    setSupplierName(supplier.name);
    setTaxId(supplier.taxId ?? "");
    setSupplierActive(supplier.isActive);
    setErrorCode(null);
  }

  async function openPurchase(id: string) {
    setErrorCode(null);
    const response = await fetcher(`${apiBaseUrl}/api/v1/purchases/${id}`, { headers });
    if (!response.ok) {
      setErrorCode((await problemCode(response)) ?? "purchases.not_found");
      return;
    }
    const detail = (await response.json()) as PurchaseDetail;
    setEditingPurchase(detail);
    setSupplierId(detail.supplierId);
    setDocumentRef(detail.supplierDocumentRef ?? "");
    setNotes(detail.notes ?? "");
    setLines(
      detail.lines.map((line) => ({
        productId: line.productId,
        quantity: line.quantityOrdered,
        unitCost: line.unitCost,
      })),
    );
    setReceiveDocument("");
    setReceiveQuantities({});
  }

  function statusLabel(status: string): string {
    switch (status) {
      case "ordered":
        return text("purchasing.ordered");
      case "partially_received":
        return text("purchasing.partiallyReceived");
      case "received":
        return text("purchasing.received");
      case "cancelled":
        return text("purchasing.cancelled");
      default:
        return text("purchasing.draft");
    }
  }

  function remainingQuantity(line: PurchaseLine): string {
    const scale = 10000;
    const ordered = Math.round(Number(line.quantityOrdered) * scale);
    const received = Math.round(Number(line.quantityReceived ?? "0") * scale);
    return ((ordered - received) / scale).toFixed(4);
  }

  async function orderPurchase() {
    if (!editingPurchase) {
      return;
    }
    setErrorCode(null);
    const response = await fetcher(`${apiBaseUrl}/api/v1/purchases/${editingPurchase.id}/order`, {
      method: "POST",
      headers: {
        ...headers,
        "content-type": "application/json",
        "Idempotency-Key": crypto.randomUUID(),
      },
      body: JSON.stringify({ version: editingPurchase.version }),
    });
    if (!response.ok) {
      setErrorCode((await problemCode(response)) ?? "purchase.not_draft");
      return;
    }
    setEditingPurchase((await response.json()) as PurchaseDetail);
    await reloadLists();
  }

  async function receivePurchase(event: FormEvent) {
    event.preventDefault();
    if (!editingPurchase) {
      return;
    }
    const lines = editingPurchase.lines.flatMap((line) => {
      const quantity = receiveQuantities[line.id]?.trim() ?? "";
      return quantity === "" ? [] : [{ purchaseLineId: line.id, quantity }];
    });
    if (receiveDocument.trim() === "") {
      setErrorCode("receiptDocumentRef.required");
      return;
    }
    if (lines.length === 0) {
      setErrorCode("lines.required");
      return;
    }
    setErrorCode(null);
    const response = await fetcher(`${apiBaseUrl}/api/v1/purchases/${editingPurchase.id}/receipts`, {
      method: "POST",
      headers: {
        ...headers,
        "content-type": "application/json",
        "Idempotency-Key": crypto.randomUUID(),
      },
      body: JSON.stringify({
        version: editingPurchase.version,
        documentRef: receiveDocument,
        lines,
      }),
    });
    if (!response.ok) {
      setErrorCode((await problemCode(response)) ?? "purchase.not_receivable");
      return;
    }
    await openPurchase(editingPurchase.id);
    await reloadLists();
  }

  async function cancelPurchase() {
    if (!editingPurchase) {
      return;
    }
    setErrorCode(null);
    const response = await fetcher(`${apiBaseUrl}/api/v1/purchases/${editingPurchase.id}/cancel`, {
      method: "POST",
      headers: {
        ...headers,
        "content-type": "application/json",
        "Idempotency-Key": crypto.randomUUID(),
      },
      body: JSON.stringify({ version: editingPurchase.version }),
    });
    if (!response.ok) {
      setErrorCode((await problemCode(response)) ?? "purchase.not_cancellable");
      return;
    }
    setEditingPurchase((await response.json()) as PurchaseDetail);
    await reloadLists();
  }

  function newDraft() {
    setEditingPurchase(null);
    setSupplierId("");
    setDocumentRef("");
    setNotes("");
    setLines([]);
    setErrorCode(null);
  }

  async function saveDraft(event: FormEvent) {
    event.preventDefault();
    if (supplierId === "") {
      setErrorCode("supplier.required");
      return;
    }
    if (lines.some((line) => line.productId === "")) {
      setErrorCode("lines.product");
      return;
    }
    setErrorCode(null);
    const body = {
      supplierId,
      supplierDocumentRef: documentRef,
      notes,
      version: editingPurchase?.version,
      lines: lines.map((line) => ({
        productId: line.productId,
        quantity: line.quantity,
        unitCost: line.unitCost,
      })),
    };
    const response = editingPurchase
      ? await fetcher(`${apiBaseUrl}/api/v1/purchases/${editingPurchase.id}`, {
          method: "PUT",
          headers: { ...headers, "content-type": "application/json" },
          body: JSON.stringify(body),
        })
      : await fetcher(`${apiBaseUrl}/api/v1/purchases`, {
          method: "POST",
          headers: {
            ...headers,
            "content-type": "application/json",
            "Idempotency-Key": crypto.randomUUID(),
          },
          body: JSON.stringify(body),
        });
    if (!response.ok) {
      setErrorCode((await problemCode(response)) ?? "validation.failed");
      return;
    }
    const saved = (await response.json()) as PurchaseDetail;
    setEditingPurchase(saved);
    setLines(
      saved.lines.map((line) => ({
        productId: line.productId,
        quantity: line.quantityOrdered,
        unitCost: line.unitCost,
      })),
    );
    await reloadLists();
  }

  async function deleteDraft() {
    if (!editingPurchase) {
      return;
    }
    setErrorCode(null);
    const response = await fetcher(
      `${apiBaseUrl}/api/v1/purchases/${editingPurchase.id}?version=${editingPurchase.version}`,
      {
        method: "DELETE",
        headers,
      },
    );
    if (!response.ok) {
      setErrorCode((await problemCode(response)) ?? "purchase.not_draft");
      return;
    }
    newDraft();
    await reloadLists();
  }

  function sellingPrice(productId: string): string {
    return products.find((product) => product.id === productId)?.unitPrice ?? "";
  }

  return (
    <section className="flex flex-col gap-3" data-testid="purchasing">
      <h2 className="text-xl font-semibold">{text("purchasing.suppliers")}</h2>
      {suppliers.length === 0 ? <p>{text("purchasing.emptySuppliers")}</p> : null}
      <ul className="flex flex-col gap-2">
        {suppliers.map((supplier) => (
          <li key={supplier.id}>
            {canWrite ? (
              <button type="button" className="text-start" onClick={() => beginSupplierEdit(supplier)}>
                {supplier.name}
                {supplier.isActive ? "" : ` · ${text("purchasing.inactive")}`}
              </button>
            ) : (
              <span>
                {supplier.name}
                {supplier.isActive ? "" : ` · ${text("purchasing.inactive")}`}
              </span>
            )}
          </li>
        ))}
      </ul>
      {canWrite ? (
        <form className="flex flex-col gap-3" onSubmit={(event) => void saveSupplier(event)}>
          <label className="flex flex-col gap-1 text-sm">
            <span>{text("purchasing.supplierName")}</span>
            <input
              className="w-full border border-stone-400 px-2 py-1"
              value={supplierName}
              onChange={(event) => setSupplierName(event.target.value)}
            />
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span>{text("purchasing.taxId")}</span>
            <input
              dir="ltr"
              className="w-full border border-stone-400 px-2 py-1"
              value={taxId}
              onChange={(event) => setTaxId(event.target.value)}
            />
          </label>
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={supplierActive}
              onChange={(event) => setSupplierActive(event.target.checked)}
            />
            <span>{text("purchasing.active")}</span>
          </label>
          <button type="submit" className="w-fit border border-stone-500 bg-white px-3 py-1.5 text-sm">
            {text("purchasing.saveSupplier")}
          </button>
        </form>
      ) : null}

      <h2 className="text-xl font-semibold">{text("purchasing.purchases")}</h2>
      {purchases.length === 0 ? <p>{text("purchasing.emptyPurchases")}</p> : null}
      <ul className="flex flex-col gap-2" data-testid="purchase-list">
        {purchases.map((purchase) => (
          <li key={purchase.id}>
            <button type="button" className="text-start" onClick={() => void openPurchase(purchase.id)}>
              <LtrValue>{purchase.number}</LtrValue>
              {" · "}
              {statusLabel(purchase.status)}
              {" · "}
              {purchase.supplierName}
              {purchase.supplierDocumentRef ? (
                <>
                  {" · "}
                  <LtrValue>{purchase.supplierDocumentRef}</LtrValue>
                </>
              ) : null}
            </button>
          </li>
        ))}
      </ul>
      {canWrite && (editingPurchase === null || editingPurchase.status === "draft") ? (
        <form className="flex flex-col gap-3" onSubmit={(event) => void saveDraft(event)} data-testid="purchase-draft">
          <button type="button" className="w-fit border border-stone-400 bg-white px-2 py-1 text-sm" onClick={newDraft}>
            {text("purchasing.newDraft")}
          </button>
          {editingPurchase ? (
            <p>
              {text("purchasing.snapshot")}: {editingPurchase.supplierName}
              {editingPurchase.supplierTaxId ? (
                <>
                  {" · "}
                  <LtrValue>{editingPurchase.supplierTaxId}</LtrValue>
                </>
              ) : null}
            </p>
          ) : null}
          <label className="flex flex-col gap-1 text-sm">
            <span>{text("purchasing.supplier")}</span>
            <select
              className="w-full border border-stone-400 px-2 py-1"
              value={supplierId}
              onChange={(event) => setSupplierId(event.target.value)}
            >
              <option value="">{text("purchasing.supplier")}</option>
              {suppliers.map((supplier) => (
                <option key={supplier.id} value={supplier.id}>
                  {supplier.name}
                </option>
              ))}
            </select>
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span>{text("purchasing.documentRef")}</span>
            <input
              dir="ltr"
              className="w-full border border-stone-400 px-2 py-1"
              value={documentRef}
              onChange={(event) => setDocumentRef(event.target.value)}
            />
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span>{text("purchasing.notes")}</span>
            <input
              className="w-full border border-stone-400 px-2 py-1"
              value={notes}
              onChange={(event) => setNotes(event.target.value)}
            />
          </label>
          {lines.map((line, index) => (
            <div key={index} className="flex flex-col gap-2 border border-stone-300 p-2">
              <label className="flex flex-col gap-1 text-sm">
                <span>{text("purchasing.lineProduct")}</span>
                <select
                  className="w-full border border-stone-400 px-2 py-1"
                  value={line.productId}
                  onChange={(event) =>
                    setLines((current) =>
                      current.map((item, itemIndex) =>
                        itemIndex === index ? { ...item, productId: event.target.value } : item,
                      ),
                    )
                  }
                >
                  <option value="">{text("purchasing.lineProduct")}</option>
                  {products.map((product) => (
                    <option key={product.id} value={product.id}>
                      {product.sku} · {product.name}
                    </option>
                  ))}
                </select>
              </label>
              <label className="flex flex-col gap-1 text-sm">
                <span>{text("purchasing.quantity")}</span>
                <input
                  dir="ltr"
                  className="w-full border border-stone-400 px-2 py-1"
                  value={line.quantity}
                  onChange={(event) =>
                    setLines((current) =>
                      current.map((item, itemIndex) =>
                        itemIndex === index ? { ...item, quantity: event.target.value } : item,
                      ),
                    )
                  }
                />
              </label>
              <label className="flex flex-col gap-1 text-sm">
                <span>{text("purchasing.unitCost")}</span>
                <input
                  dir="ltr"
                  className="w-full border border-stone-400 px-2 py-1"
                  value={line.unitCost}
                  onChange={(event) =>
                    setLines((current) =>
                      current.map((item, itemIndex) =>
                        itemIndex === index ? { ...item, unitCost: event.target.value } : item,
                      ),
                    )
                  }
                />
              </label>
              <p>
                {text("purchasing.sellingPrice")}: <LtrValue>{sellingPrice(line.productId)}</LtrValue>
              </p>
              <button
                type="button"
                className="w-fit border border-stone-400 bg-white px-2 py-1 text-sm"
                onClick={() => setLines((current) => current.filter((_, itemIndex) => itemIndex !== index))}
              >
                {text("purchasing.removeLine")}
              </button>
            </div>
          ))}
          <button
            type="button"
            className="w-fit border border-stone-400 bg-white px-2 py-1 text-sm"
            onClick={() => setLines((current) => [...current, { productId: "", quantity: "", unitCost: "" }])}
          >
            {text("purchasing.addLine")}
          </button>
          {errorCode ? <p role="alert">{text(errorCode)}</p> : null}
          <button type="submit" className="w-fit border border-stone-500 bg-white px-3 py-1.5 text-sm">
            {text("purchasing.saveDraft")}
          </button>
          {editingPurchase ? (
            <button
              type="button"
              className="w-fit border border-stone-500 bg-white px-3 py-1.5 text-sm"
              onClick={() => void deleteDraft()}
            >
              {text("purchasing.deleteDraft")}
            </button>
          ) : null}
          {editingPurchase?.status === "draft" ? (
            <button
              type="button"
              className="w-fit border border-stone-500 bg-white px-3 py-1.5 text-sm"
              onClick={() => void orderPurchase()}
            >
              {text("purchasing.order")}
            </button>
          ) : null}
        </form>
      ) : null}
      {editingPurchase && (editingPurchase.status !== "draft" || !canWrite) ? (
        <section className="flex flex-col gap-3" data-testid="purchase-detail">
          <p>
            {statusLabel(editingPurchase.status)}
            {" · "}
            {editingPurchase.supplierName}
          </p>
          <ul className="flex flex-col gap-2">
            {editingPurchase.lines.map((line) => (
              <li key={line.id}>
                <LtrValue>{line.productSku}</LtrValue>
                {" · "}
                {line.productName}
                {" · "}
                <LtrValue>{line.quantityOrdered}</LtrValue>
                {" · "}
                {text("purchasing.remaining")} <LtrValue>{remainingQuantity(line)}</LtrValue>
              </li>
            ))}
          </ul>
          <h3 className="text-lg font-semibold">{text("purchasing.history")}</h3>
          {(editingPurchase.receipts ?? []).length === 0 ? <p>{text("purchasing.emptyHistory")}</p> : null}
          <ul className="flex flex-col gap-2">
            {(editingPurchase.receipts ?? []).map((receipt) => (
              <li key={receipt.id}>
                <LtrValue>{receipt.number}</LtrValue>
                {" · "}
                <LtrValue>{receipt.documentRef}</LtrValue>
                {" · "}
                <LtrValue>{receipt.lines.map((line) => line.quantity).join(", ")}</LtrValue>
              </li>
            ))}
          </ul>
          {canWrite && (editingPurchase.status === "ordered" || editingPurchase.status === "partially_received") ? (
            <form className="flex flex-col gap-3" onSubmit={(event) => void receivePurchase(event)} data-testid="purchase-receipt">
              <label className="flex flex-col gap-1 text-sm">
                <span>{text("purchasing.receiptDocument")}</span>
                <input
                  dir="ltr"
                  className="w-full border border-stone-400 px-2 py-1"
                  value={receiveDocument}
                  onChange={(event) => setReceiveDocument(event.target.value)}
                />
              </label>
              {editingPurchase.lines.map((line) => (
                <label key={line.id} className="flex flex-col gap-1 text-sm">
                  <span>
                    {text("purchasing.receiveQuantity")} · {line.productName}
                  </span>
                  <input
                    dir="ltr"
                    className="w-full border border-stone-400 px-2 py-1"
                    value={receiveQuantities[line.id] ?? ""}
                    onChange={(event) =>
                      setReceiveQuantities((current) => ({ ...current, [line.id]: event.target.value }))
                    }
                  />
                </label>
              ))}
              <button type="submit" className="w-fit border border-stone-500 bg-white px-3 py-1.5 text-sm">
                {text("purchasing.receive")}
              </button>
            </form>
          ) : null}
          {canWrite &&
          editingPurchase.status === "ordered" &&
          editingPurchase.lines.every((line) => Number(line.quantityReceived ?? "0") === 0) &&
          (editingPurchase.receipts ?? []).length === 0 ? (
            <button
              type="button"
              className="w-fit border border-stone-500 bg-white px-3 py-1.5 text-sm"
              onClick={() => void cancelPurchase()}
            >
              {text("purchasing.cancelOrder")}
            </button>
          ) : null}
          {errorCode ? <p role="alert">{text(errorCode)}</p> : null}
        </section>
      ) : null}
      {!canWrite && errorCode && editingPurchase === null ? <p role="alert">{text(errorCode)}</p> : null}
    </section>
  );
}

async function problemCode(response: Response): Promise<string | null> {
  const body = (await response.json()) as ProblemBody;
  const fieldCode = body.errors ? Object.values(body.errors)[0]?.[0] : undefined;
  return fieldCode ?? body.code ?? null;
}
