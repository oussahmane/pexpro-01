import {
  addAmounts,
  allocateDiscount,
  belowFloor,
  formatAmount,
  lineGross,
  parseAmount,
  percentAmount,
  subtractToZero,
  sumAmounts,
} from "./money";

export type PriceKind = "retail" | "wholesale";
export type DiscountKind = "none" | "amount" | "percent";

export type CatalogProduct = {
  id: string;
  sku: string;
  name: string;
  unit: string;
  unitPrice: string;
  wholesaleUnitPrice?: string | null;
  minimumUnitPrice?: string | null;
  trackStock: boolean;
  isActive: boolean;
};

export type CartLine = {
  productId: string;
  sku: string;
  name: string;
  unit: string;
  retailPrice: string;
  wholesalePrice: string | null;
  minimumPrice: string | null;
  priceKind: PriceKind;
  unitPrice: string;
  quantity: string;
  discountKind: DiscountKind;
  discountInput: string;
  lineDiscount: string | null;
  lineTotal: string | null;
  trackStock: boolean;
  priceOverride: boolean;
};

export type ScanResult =
  | { kind: "empty" }
  | { kind: "unknown" }
  | { kind: "inactive" }
  | { kind: "ambiguous" }
  | { kind: "add"; product: CatalogProduct };

export type SaleQuote = {
  lines: CartLine[];
  subtotal: string | null;
  discount: string | null;
  total: string | null;
  issue: string | null;
};

export function catalogKey(value: string): string {
  return value.trim().toUpperCase();
}

export function filterProducts(products: CatalogProduct[], query: string): CatalogProduct[] {
  const needle = query.trim().toLowerCase();
  if (needle === "") {
    return products;
  }

  return products.filter(
    (product) => product.name.toLowerCase().includes(needle) || product.sku.toLowerCase().includes(needle),
  );
}

export function resolveScan(products: CatalogProduct[], query: string): ScanResult {
  const trimmed = query.trim();
  if (trimmed === "") {
    return { kind: "empty" };
  }

  const key = catalogKey(trimmed);
  const exact = products.filter((product) => catalogKey(product.sku) === key);
  if (exact.length === 1) {
    return exact[0].isActive ? { kind: "add", product: exact[0] } : { kind: "inactive" };
  }

  if (exact.length > 1) {
    return { kind: "ambiguous" };
  }

  const matches = filterProducts(products, trimmed);
  if (matches.length === 0) {
    return { kind: "unknown" };
  }

  if (matches.length === 1) {
    return matches[0].isActive ? { kind: "add", product: matches[0] } : { kind: "inactive" };
  }

  return { kind: "ambiguous" };
}

export function addProduct(lines: CartLine[], product: CatalogProduct): { lines: CartLine[] } | { error: string } {
  const current = lines.find((line) => line.productId === product.id);
  const quantity = current ? addAmounts(current.quantity, "1.0000") : "1.0000";
  if (quantity === null) {
    return { error: current && parseAmount(current.quantity) === null ? "quantity.invalid" : "quantity.range" };
  }

  return replaceQuantity(lines, product, quantity);
}

export function withQuantity(lines: CartLine[], productId: string, quantity: string): CartLine[] {
  return lines.map((line) => (line.productId === productId ? reprice({ ...line, quantity }) : line));
}

export function quantityIssue(quantity: string, price: string): string | null {
  const trimmed = quantity.trim();
  if (trimmed === "") {
    return "quantity.required";
  }

  if (trimmed.startsWith("-")) {
    return "quantity.sign";
  }

  if (/^\d+\.\d{5,}$/.test(trimmed)) {
    return "quantity.scale";
  }

  const parsed = parseAmount(trimmed);
  if (parsed === null) {
    return "quantity.invalid";
  }

  if (parsed === 0n) {
    return "quantity.positive";
  }

  if (lineGross(trimmed, price) === null) {
    return "line.scale";
  }

  return null;
}

export function removeProduct(lines: CartLine[], productId: string): CartLine[] {
  return lines.filter((line) => line.productId !== productId);
}

export function applyCatalog(lines: CartLine[], products: CatalogProduct[]): CartLine[] {
  return lines.map((line) => {
    const product = products.find((item) => item.id === line.productId);
    if (!product) {
      return line;
    }

    const next: CartLine = {
      ...line,
      sku: product.sku,
      name: product.name,
      unit: product.unit,
      retailPrice: product.unitPrice,
      wholesalePrice: product.wholesaleUnitPrice ?? null,
      minimumPrice: product.minimumUnitPrice ?? null,
      trackStock: product.trackStock,
    };
    if (!line.priceOverride) {
      next.unitPrice = selectedList(next) ?? product.unitPrice;
    }

    return reprice(next);
  });
}

export function quoteSale(
  lines: CartLine[],
  ticketKind: DiscountKind,
  ticketInput: string,
  administrator: boolean,
): SaleQuote {
  const priced = lines.map((line) => reprice(line, administrator));
  const issue = priced.map(lineIssue).find((value) => value !== null) ?? null;
  if (issue || priced.some((line) => line.lineTotal === null)) {
    return { lines: priced, subtotal: null, discount: null, total: null, issue };
  }

  const subtotal = sumAmounts(priced.map((line) => line.lineTotal as string));
  if (subtotal === null) {
    return { lines: priced, subtotal: null, discount: null, total: null, issue: "total.mismatch" };
  }

  const discount = discountMoney(subtotal, ticketKind, ticketInput);
  if (discount === null) {
    return { lines: priced, subtotal, discount: null, total: null, issue: "discount.mismatch" };
  }

  const total = subtractToZero(subtotal, discount);
  if (total === null) {
    return { lines: priced, subtotal, discount, total: null, issue: "discount.exceeds" };
  }

  const shares = allocateDiscount(priced.map((line) => line.lineTotal as string), discount);
  if (!shares) {
    return { lines: priced, subtotal, discount, total: null, issue: "discount.allocation" };
  }

  if (!administrator) {
    for (const [index, line] of priced.entries()) {
      const net = subtractToZero(line.lineTotal as string, shares[index] ?? "0.0000");
      const floor = line.minimumPrice ?? selectedList(line);
      if (net === null || floor === null) {
        return { lines: priced, subtotal, discount, total: null, issue: "line.scale" };
      }

      if (belowFloor(net, line.quantity, floor)) {
        return { lines: priced, subtotal, discount, total: null, issue: "discount.below_minimum" };
      }
    }
  }

  return { lines: priced, subtotal, discount, total, issue: null };
}

export function selectedList(line: Pick<CartLine, "priceKind" | "retailPrice" | "wholesalePrice">): string | null {
  return line.priceKind === "wholesale" ? line.wholesalePrice : line.retailPrice;
}

function replaceQuantity(
  lines: CartLine[],
  product: CatalogProduct,
  quantity: string,
): { lines: CartLine[] } | { error: string } {
  const current = lines.find((line) => line.productId === product.id);
  const issue = quantityIssue(quantity, current?.unitPrice ?? product.unitPrice);
  if (issue) {
    return { error: issue };
  }

  const next = reprice({
    productId: product.id,
    sku: product.sku,
    name: product.name,
    unit: product.unit,
    retailPrice: product.unitPrice,
    wholesalePrice: product.wholesaleUnitPrice ?? null,
    minimumPrice: product.minimumUnitPrice ?? null,
    priceKind: current?.priceKind ?? "retail",
    unitPrice: current?.priceOverride ? current.unitPrice : product.unitPrice,
    quantity,
    discountKind: current?.discountKind ?? "none",
    discountInput: current?.discountInput ?? "",
    lineDiscount: null,
    lineTotal: null,
    trackStock: product.trackStock,
    priceOverride: current?.priceOverride ?? false,
  });
  if (next.lineTotal === null) {
    return { error: "line.scale" };
  }

  const exists = lines.some((line) => line.productId === product.id);
  return { lines: exists ? lines.map((line) => (line.productId === product.id ? next : line)) : [...lines, next] };
}

function reprice(line: CartLine, administrator = line.priceOverride): CartLine {
  const list = selectedList(line);
  if (line.priceKind === "wholesale" && list === null) {
    return { ...line, lineDiscount: null, lineTotal: null };
  }

  const charged = administrator && line.priceOverride ? line.unitPrice : (list ?? line.unitPrice);
  const gross = lineGross(line.quantity, charged);
  if (gross === null) {
    return { ...line, unitPrice: charged, lineDiscount: null, lineTotal: null };
  }

  const lineDiscount = discountMoney(gross, line.discountKind, line.discountInput);
  const lineTotal = lineDiscount === null ? null : subtractToZero(gross, lineDiscount);
  return { ...line, unitPrice: charged, lineDiscount, lineTotal };
}

function discountMoney(basis: string, kind: DiscountKind, input: string): string | null {
  if (kind === "none") {
    return "0.0000";
  }

  if (kind === "amount") {
    const amount = parseAmount(input);
    const base = parseAmount(basis);
    if (amount === null || base === null || amount > base) {
      return null;
    }

    return formatAmount(amount);
  }

  return percentAmount(basis, input);
}

function lineIssue(line: CartLine): string | null {
  if (line.priceKind === "wholesale" && line.wholesalePrice === null) {
    return "price.wholesale_missing";
  }

  const charged = line.unitPrice;
  const quantity = quantityIssue(line.quantity, charged);
  if (quantity) {
    return quantity;
  }

  if (line.lineDiscount === null || line.lineTotal === null) {
    return line.discountKind === "percent" ? "discount.mismatch" : "discount.exceeds";
  }

  return null;
}
