import { describe, expect, it } from "vitest";
import { addAmounts, allocateDiscount, formatAmount, lineGross, parseAmount, percentAmount, sumAmounts } from "./money";

describe("money", () => {
  it("keeps exact scale-4 products and rejects a finer product", () => {
    expect(lineGross("2.0000", "10.5000")).toBe("21.0000");
    expect(lineGross("1.0001", "1.0001")).toBeNull();
    expect(lineGross("0.0000", "10.0000")).toBeNull();
    expect(parseAmount("1,50")).toBeNull();
    expect(parseAmount("1e2")).toBeNull();
    expect(formatAmount(parseAmount("2") ?? 0n)).toBe("2.0000");
    expect(addAmounts("10.0000", "5.5000")).toBe("15.5000");
    expect(sumAmounts(["20.0000", "10.0000"])).toBe("30.0000");
    expect(percentAmount("10.0000", "33.3333")).toBe("3.3333");
    expect(percentAmount("0.0001", "50")).toBe("0.0001");
    expect(allocateDiscount(["10.0000", "20.0000", "30.0000"], "1.0000")).toEqual(["0.1667", "0.3333", "0.5000"]);
    expect(allocateDiscount(["10.0000", "10.0000", "10.0000"], "0.0002")).toEqual(["0.0001", "0.0001", "0.0000"]);
  });
});
