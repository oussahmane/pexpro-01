const SCALE_FACTOR = 10_000n;
const MAXIMUM = 999_999_999_999_999_9999n;

export function parseAmount(text: string): bigint | null {
  const trimmed = text.trim();
  if (!/^\d+(\.\d{1,4})?$/.test(trimmed)) {
    return null;
  }

  const [whole, fraction = ""] = trimmed.split(".");
  const scaled = BigInt(whole + fraction.padEnd(4, "0"));
  if (scaled > MAXIMUM) {
    return null;
  }

  return scaled;
}

export function formatAmount(value: bigint): string {
  const negative = value < 0n;
  const absolute = (negative ? -value : value).toString().padStart(5, "0");
  const whole = absolute.slice(0, -4);
  const fraction = absolute.slice(-4);
  return `${negative ? "-" : ""}${whole}.${fraction}`;
}

export function lineGross(quantityText: string, priceText: string): string | null {
  const quantity = parseAmount(quantityText);
  const price = parseAmount(priceText);
  if (quantity === null || price === null || quantity <= 0n) {
    return null;
  }

  const product = quantity * price;
  if (product % SCALE_FACTOR !== 0n) {
    return null;
  }

  const gross = product / SCALE_FACTOR;
  if (gross < 0n || gross > MAXIMUM) {
    return null;
  }

  return formatAmount(gross);
}

export function addAmounts(leftText: string, rightText: string): string | null {
  const left = parseAmount(leftText);
  const right = parseAmount(rightText);
  if (left === null || right === null) {
    return null;
  }

  const sum = left + right;
  if (sum < 0n || sum > MAXIMUM) {
    return null;
  }

  return formatAmount(sum);
}

export function subtractAmount(leftText: string, rightText: string): string | null {
  const left = parseAmount(leftText);
  const right = parseAmount(rightText);
  if (left === null || right === null) {
    return null;
  }

  const difference = left - right;
  if (difference <= 0n) {
    return null;
  }

  return formatAmount(difference);
}

export function sumAmounts(values: string[]): string | null {
  let total = 0n;
  for (const value of values) {
    const parsed = parseAmount(value);
    if (parsed === null) {
      return null;
    }

    total += parsed;
    if (total > MAXIMUM) {
      return null;
    }
  }

  return formatAmount(total);
}

export function subtractToZero(leftText: string, rightText: string): string | null {
  const left = parseAmount(leftText);
  const right = parseAmount(rightText);
  if (left === null || right === null) {
    return null;
  }

  const difference = left - right;
  if (difference < 0n || difference > MAXIMUM) {
    return null;
  }

  return formatAmount(difference);
}

export function percentAmount(basisText: string, rateText: string): string | null {
  const basis = parseAmount(basisText);
  const rate = parseAmount(rateText);
  if (basis === null || rate === null || rate > 1_000_000n) {
    return null;
  }

  const product = basis * rate;
  const divisor = 1_000_000n;
  const quotient = product / divisor;
  const remainder = product % divisor;
  const rounded = remainder * 2n >= divisor ? quotient + 1n : quotient;
  if (rounded > MAXIMUM) {
    return null;
  }

  return formatAmount(rounded);
}

export function allocateDiscount(lineTotals: string[], discountText: string): string[] | null {
  const parsed = lineTotals.map((value) => parseAmount(value));
  if (parsed.some((value) => value === null)) {
    return null;
  }

  const totals = parsed as bigint[];
  const discount = parseAmount(discountText);
  if (discount === null) {
    return null;
  }

  const subtotal = totals.reduce((sum, value) => sum + value, 0n);
  if (discount > subtotal || (subtotal === 0n && discount !== 0n)) {
    return null;
  }

  if (discount === 0n) {
    return totals.map(() => formatAmount(0n));
  }

  const quotients = totals.map((total) => (total * discount) / subtotal);
  const remainders = totals.map((total) => (total * discount) % subtotal);
  let leftover = discount - quotients.reduce((sum, value) => sum + value, 0n);
  const order = totals
    .map((_, index) => index)
    .sort((left, right) => (remainders[left] === remainders[right] ? left - right : remainders[left] > remainders[right] ? -1 : 1));
  const shares = [...quotients];
  for (const index of order) {
    if (leftover === 0n) {
      break;
    }

    shares[index] += 1n;
    leftover -= 1n;
  }

  if (leftover !== 0n || shares.some((share, index) => share > totals[index])) {
    return null;
  }

  if (shares.reduce((sum, value) => sum + value, 0n) !== discount) {
    return null;
  }

  return shares.map((share) => formatAmount(share));
}

export function belowFloor(netText: string, quantityText: string, floorText: string): boolean {
  const net = parseAmount(netText);
  const quantity = parseAmount(quantityText);
  const floor = parseAmount(floorText);
  if (net === null || quantity === null || floor === null) {
    return false;
  }

  return net * SCALE_FACTOR < quantity * floor;
}

export function exceedsAmount(quantityText: string, availableText: string): boolean {
  const quantity = parseAmount(quantityText);
  const available = parseAmount(availableText);
  if (quantity === null || available === null) {
    return false;
  }

  return quantity > available;
}
