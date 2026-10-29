import { useEffect, useState, type FormEvent } from "react";
import { translate, type Locale } from "./i18n";

type Fetcher = (input: string, init?: RequestInit) => Promise<Response>;

type ProductRow = {
  id: string;
  sku: string;
  name: string;
  unit: string;
  unitPrice: string;
};

type CategoryRow = {
  id: string;
  name: string;
  parentId: string | null;
};

type CatalogPanelProps = {
  apiBaseUrl: string;
  accessToken: string;
  fetcher: Fetcher;
  locale: Locale;
};

export function CatalogPanel({ apiBaseUrl, accessToken, fetcher, locale }: CatalogPanelProps) {
  const text = (key: string) => translate(locale, key);
  const [products, setProducts] = useState<ProductRow[]>([]);
  const [categories, setCategories] = useState<CategoryRow[]>([]);
  const [categoryName, setCategoryName] = useState("");
  const [sku, setSku] = useState("");
  const [name, setName] = useState("");
  const [unit, setUnit] = useState("u");
  const [unitPrice, setUnitPrice] = useState("");
  const [errorCode, setErrorCode] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    async function load() {
      const headers = { authorization: `Bearer ${accessToken}` };
      const [productResponse, categoryResponse] = await Promise.all([
        fetcher(`${apiBaseUrl}/api/v1/products`, { headers }),
        fetcher(`${apiBaseUrl}/api/v1/categories`, { headers }),
      ]);
      if (cancelled) {
        return;
      }
      if (productResponse.ok) {
        setProducts((await productResponse.json()) as ProductRow[]);
      }
      if (categoryResponse.ok) {
        setCategories((await categoryResponse.json()) as CategoryRow[]);
      }
    }
    void load();
    return () => {
      cancelled = true;
    };
  }, [accessToken, apiBaseUrl, fetcher]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setErrorCode(null);
    const response = await fetcher(`${apiBaseUrl}/api/v1/products`, {
      method: "POST",
      headers: {
        authorization: `Bearer ${accessToken}`,
        "content-type": "application/json",
      },
      body: JSON.stringify({ sku, name, unit, unitPrice, trackStock: true, isActive: true }),
    });
    if (!response.ok) {
      const problem = (await response.json()) as { code?: string; errors?: Record<string, string[]> };
      const fieldCode = problem.errors ? Object.values(problem.errors)[0]?.[0] : undefined;
      setErrorCode(fieldCode ?? problem.code ?? "validation.failed");
      return;
    }

    const created = (await response.json()) as ProductRow;
    setProducts((current) => [...current, created].sort((left, right) => left.name.localeCompare(right.name)));
    setSku("");
    setName("");
    setUnitPrice("");
  }

  async function submitCategory(event: FormEvent) {
    event.preventDefault();
    setErrorCode(null);
    const response = await fetcher(`${apiBaseUrl}/api/v1/categories`, {
      method: "POST",
      headers: {
        authorization: `Bearer ${accessToken}`,
        "content-type": "application/json",
      },
      body: JSON.stringify({ name: categoryName }),
    });
    if (!response.ok) {
      const problem = (await response.json()) as { code?: string };
      setErrorCode(problem.code ?? "validation.failed");
      return;
    }

    const created = (await response.json()) as CategoryRow;
    setCategories((current) => [...current, created].sort((left, right) => left.name.localeCompare(right.name)));
    setCategoryName("");
  }

  return (
    <section className="flex flex-col gap-3" data-testid="catalog">
      <h2 className="text-xl font-semibold">{text("catalog.categories")}</h2>
      {categories.length === 0 ? <p>{text("catalog.noCategories")}</p> : null}
      <ul className="flex flex-col gap-1">
        {categories.map((category) => (
          <li key={category.id}>{category.name}</li>
        ))}
      </ul>
      <form className="flex flex-col gap-3" onSubmit={(event) => void submitCategory(event)}>
        <label className="flex flex-col gap-1 text-sm">
          <span>{text("catalog.categoryName")}</span>
          <input className="w-full border border-stone-400 px-2 py-1" value={categoryName} onChange={(event) => setCategoryName(event.target.value)} />
        </label>
        <button type="submit" className="w-fit border border-stone-500 bg-white px-3 py-1.5 text-sm">
          {text("catalog.addCategory")}
        </button>
      </form>
      <h2 className="text-xl font-semibold">{text("catalog.products")}</h2>
      {products.length === 0 ? <p>{text("catalog.empty")}</p> : null}
      <ul className="flex flex-col gap-1">
        {products.map((product) => (
          <li key={product.id}>
            {product.sku} · {product.name} · {product.unitPrice}
          </li>
        ))}
      </ul>
      <form className="flex flex-col gap-3" onSubmit={(event) => void submit(event)}>
        <label className="flex flex-col gap-1 text-sm">
          <span>{text("catalog.sku")}</span>
          <input className="w-full border border-stone-400 px-2 py-1" value={sku} onChange={(event) => setSku(event.target.value)} />
        </label>
        <label className="flex flex-col gap-1 text-sm">
          <span>{text("catalog.productName")}</span>
          <input className="w-full border border-stone-400 px-2 py-1" value={name} onChange={(event) => setName(event.target.value)} />
        </label>
        <label className="flex flex-col gap-1 text-sm">
          <span>{text("catalog.unit")}</span>
          <input className="w-full border border-stone-400 px-2 py-1" value={unit} onChange={(event) => setUnit(event.target.value)} />
        </label>
        <label className="flex flex-col gap-1 text-sm">
          <span>{text("catalog.price")}</span>
          <input className="w-full border border-stone-400 px-2 py-1" value={unitPrice} onChange={(event) => setUnitPrice(event.target.value)} />
        </label>
        {errorCode ? <p role="alert">{text(errorCode)}</p> : null}
        <button type="submit" className="w-fit border border-stone-500 bg-white px-3 py-1.5 text-sm">
          {text("catalog.add")}
        </button>
      </form>
    </section>
  );
}
