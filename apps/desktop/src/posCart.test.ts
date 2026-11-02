import { describe, expect, it } from "vitest";
import { addProduct, quantityIssue, quoteSale, removeProduct, resolveScan, withQuantity, type CartLine, type CatalogProduct } from "./posCart";

const eau: CatalogProduct = {
  id: "p1",
  sku: "EAU-1",
  name: "Eau",
  unit: "u",
  unitPrice: "10.0000",
  trackStock: true,
  isActive: true,
};

const inactive: CatalogProduct = { ...eau, id: "p2", sku: "OLD-1", name: "Ancien", isActive: false };

describe("posCart", () => {
  it("matches a scanner code to the SKU and rejects an unknown or inactive product", () => {
    expect(resolveScan([eau, inactive], "eau-1")).toEqual({ kind: "add", product: eau });
    expect(resolveScan([eau, inactive], "OLD-1").kind).toBe("inactive");
    expect(resolveScan([eau], "ABSENT").kind).toBe("unknown");
    expect(resolveScan([eau, { ...eau, id: "p3", sku: "EAU-2", name: "Eau gazeuse" }], "eau").kind).toBe("ambiguous");
  });

  it("increases, replaces, and removes quantities without floating-point totals", () => {
    const first = addProduct([], eau);
    expect(first).toEqual({
      lines: [expect.objectContaining({ quantity: "1.0000", lineTotal: "10.0000" })],
    });
    if (!("lines" in first)) {
      throw new Error("expected a line");
    }

    const second = addProduct(first.lines, eau);
    expect(second).toEqual({
      lines: [expect.objectContaining({ quantity: "2.0000", lineTotal: "20.0000" })],
    });
    const edited = withQuantity(first.lines, eau.id, "1.5000");
    expect(edited[0]?.lineTotal).toBe("15.0000");
    expect(quantityIssue("0", "10.0000")).toBe("quantity.positive");
    expect(quantityIssue("1.00001", "10.0000")).toBe("quantity.scale");
    expect(removeProduct(first.lines, eau.id)).toEqual([]);
  });

  it("keeps a cashier at the floor when quantity times the minimum is not an exact scale-4 gross", () => {
    // 1.5000 × 0.3333 = 0.49995. The first scale-4 net that is not below that floor is 0.5000.
    const clear = quoteSale([priced()], "none", "", false);
    const atFloor = quoteSale([priced({ discountKind: "amount", discountInput: "14.5000" })], "none", "", false);
    const below = quoteSale([priced({ discountKind: "amount", discountInput: "14.5001" })], "none", "", false);
    const fractional = quoteSale(
      [priced({ retailPrice: "2.0000", unitPrice: "2.0000", minimumPrice: "1.0001", quantity: "1.0001" })],
      "none",
      "",
      false,
    );
    const uneven = quoteSale(
      [priced({ retailPrice: "1.0001", unitPrice: "1.0001", minimumPrice: "0.0000", quantity: "1.0001" })],
      "none",
      "",
      false,
    );
    const administrator = quoteSale([priced({ discountKind: "amount", discountInput: "14.5001" })], "none", "", true);
    const missingFloor = quoteSale(
      [priced({ minimumPrice: null, quantity: "1.0000", discountKind: "amount", discountInput: "0.0001" })],
      "none",
      "",
      false,
    );

    expect(clear.issue).toBeNull();
    expect(clear.total).toBe("15.0000");
    expect(atFloor.issue).toBeNull();
    expect(atFloor.total).toBe("0.5000");
    expect(below.issue).toBe("discount.below_minimum");
    expect(fractional.issue).toBeNull();
    expect(fractional.total).toBe("2.0002");
    expect(uneven.issue).toBe("line.scale");
    expect(administrator.issue).toBeNull();
    expect(administrator.total).toBe("0.4999");
    expect(missingFloor.issue).toBe("discount.below_minimum");
    expect(quoteSale([priced({ minimumPrice: null, quantity: "1.0000" })], "none", "", false).issue).toBeNull();
  });
});

function priced(overrides: Partial<CartLine> = {}): CartLine {
  return {
    productId: "p1",
    sku: "EAU-1",
    name: "Eau",
    unit: "u",
    retailPrice: "10.0000",
    wholesalePrice: null,
    minimumPrice: "0.3333",
    priceKind: "retail",
    unitPrice: "10.0000",
    quantity: "1.5000",
    discountKind: "none",
    discountInput: "",
    lineDiscount: null,
    lineTotal: null,
    trackStock: true,
    priceOverride: false,
    ...overrides,
  };
}
