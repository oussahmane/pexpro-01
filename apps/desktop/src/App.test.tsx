import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import App from "./App";
import type { SecureStore } from "./secureStore";

afterEach(() => {
  document.documentElement.lang = "en";
  document.documentElement.dir = "ltr";
});

describe("App", () => {
  it("starts in French and switches Arabic to a right-to-left layout", async () => {
    const user = userEvent.setup();
    render(<App fetcher={fetcher(status(false))} store={memoryStore()} />);

    expect(await screen.findByRole("heading", { name: "Configuration de l'entreprise" })).toBeInTheDocument();
    expect(document.documentElement.lang).toBe("fr");
    expect(document.documentElement.dir).toBe("ltr");

    await user.click(screen.getByRole("button", { name: "العربية" }));

    expect(screen.getByRole("heading", { name: "إعداد الشركة" })).toBeInTheDocument();
    expect(document.documentElement.lang).toBe("ar");
    expect(document.documentElement.dir).toBe("rtl");
  });

  it("does not submit setup when the password is shorter than 12 characters", async () => {
    const user = userEvent.setup();
    const fetchMock = fetcher(status(false));
    render(<App fetcher={fetchMock} store={memoryStore()} />);
    await screen.findByRole("heading", { name: "Configuration de l'entreprise" });

    await user.type(screen.getByLabelText("Raison sociale"), "Pexilio");
    await user.type(screen.getByLabelText("Taux de taxe par défaut"), "0.19");
    await user.type(screen.getByLabelText("Adresse e-mail"), "owner@example.com");
    await user.type(screen.getByLabelText("Mot de passe"), "short");
    await user.click(screen.getByRole("button", { name: "Créer le compte administrateur" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Le mot de passe doit contenir au moins 12 caractères.",
    );
    expect(fetchMock).not.toHaveBeenCalledWith(
      expect.stringContaining("/api/v1/setup"),
      expect.objectContaining({ method: "POST" }),
    );
  });

  it("shows a failed login in French and a successful login keeps the refresh token in memory", async () => {
    const user = userEvent.setup();
    const save = vi.fn(async () => "memory" as const);
    const store: SecureStore = { load: async () => null, save, clear: async () => undefined };
    const fetchMock = vi.fn(async (input: string, init?: RequestInit) => {
      const url = String(input);
      if (url.endsWith("/health")) {
        return json({ status: "healthy", database: "up" });
      }
      if (url.endsWith("/api/v1/setup/status")) {
        return json({ complete: true });
      }
      if (url.endsWith("/api/v1/auth/login") && init?.body?.toString().includes("wrong-password-value")) {
        return json({ code: "auth.invalid_credentials" }, 401);
      }
      if (url.endsWith("/api/v1/auth/login")) {
        return json({ accessToken: "access-token", refreshToken: "refresh-token" });
      }
      if (url.endsWith("/api/v1/company")) {
        return json({ legalName: "Pexilio Démo" });
      }
      if (url.endsWith("/api/v1/products") && init?.method !== "POST") {
        return json([]);
      }
      if (url.endsWith("/api/v1/categories")) {
        return json([]);
      }
      if (url.includes("/api/v1/stock/")) {
        return json([]);
      }
      throw new Error(url);
    });
    render(<App fetcher={fetchMock} store={store} />);
    await screen.findByRole("heading", { name: "Connexion" });

    await user.type(screen.getByLabelText("Adresse e-mail"), "owner@example.com");
    await user.type(screen.getByLabelText("Mot de passe"), "wrong-password-value");
    await user.click(screen.getByRole("button", { name: "Se connecter" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("Adresse e-mail ou mot de passe incorrect.");

    await user.clear(screen.getByLabelText("Mot de passe"));
    await user.type(screen.getByLabelText("Mot de passe"), "correct-horse-battery");
    await user.click(screen.getByRole("button", { name: "Se connecter" }));

    expect(await screen.findByRole("heading", { name: "Pexilio Démo" })).toBeInTheDocument();
    expect(screen.getByText(/conservé en mémoire/)).toBeInTheDocument();
    expect(save).toHaveBeenCalledWith("refresh-token");
    expect(screen.getByTestId("overall-status")).toHaveTextContent("healthy");
    expect(screen.getByText("Aucun produit.")).toBeInTheDocument();
  });

  it("keeps a product price as a string and shows a duplicate SKU in French", async () => {
    const user = userEvent.setup();
    const products: { id: string; sku: string; name: string; unit: string; unitPrice: string }[] = [];
    const fetchMock = vi.fn(async (input: string, init?: RequestInit) => {
      const url = String(input);
      if (url.endsWith("/health")) {
        return json({ status: "healthy", database: "up" });
      }
      if (url.endsWith("/api/v1/setup/status")) {
        return json({ complete: true });
      }
      if (url.endsWith("/api/v1/auth/login")) {
        return json({ accessToken: "access-token", refreshToken: "refresh-token" });
      }
      if (url.endsWith("/api/v1/company")) {
        return json({ legalName: "Pexilio Démo" });
      }
      if (url.endsWith("/api/v1/products") && init?.method === "POST") {
        const body = JSON.parse(String(init.body)) as { sku: string; unitPrice: string; name: string; unit: string };
        if (products.some((product) => product.sku.toLowerCase() === body.sku.toLowerCase())) {
          return json({ code: "products.sku_taken" }, 409);
        }
        const created = { id: "p1", sku: body.sku, name: body.name, unit: body.unit, unitPrice: body.unitPrice };
        products.push(created);
        return json(created, 201);
      }
      if (url.endsWith("/api/v1/products")) {
        return json(products);
      }
      if (url.endsWith("/api/v1/categories") && init?.method === "POST") {
        const body = JSON.parse(String(init.body)) as { name: string };
        return json({ id: "c1", name: body.name, parentId: null }, 201);
      }
      if (url.endsWith("/api/v1/categories")) {
        return json([]);
      }
      if (url.includes("/api/v1/stock/")) {
        return json([]);
      }
      throw new Error(url);
    });
    render(<App fetcher={fetchMock} store={{ load: async () => null, save: async () => "memory", clear: async () => undefined }} />);
    await screen.findByRole("heading", { name: "Connexion" });
    await user.type(screen.getByLabelText("Adresse e-mail"), "owner@example.com");
    await user.type(screen.getByLabelText("Mot de passe"), "correct-horse-battery");
    await user.click(screen.getByRole("button", { name: "Se connecter" }));
    await screen.findByRole("heading", { name: "Produits" });
    await user.type(screen.getByLabelText("Nom de la catégorie"), "Boissons");
    await user.click(screen.getByRole("button", { name: "Ajouter la catégorie" }));
    expect(await screen.findByText("Boissons")).toBeInTheDocument();

    await user.type(screen.getByLabelText("Référence"), "SKU-1");
    await user.type(screen.getByLabelText("Nom"), "Eau");
    await user.type(screen.getByLabelText("Prix unitaire"), "19.5000");
    await user.click(screen.getByRole("button", { name: "Ajouter le produit" }));
    expect(await screen.findByText("SKU-1 · Eau · 19.5000")).toBeInTheDocument();

    await user.type(screen.getByLabelText("Référence"), "sku-1");
    await user.type(screen.getByLabelText("Nom"), "Eau");
    await user.type(screen.getByLabelText("Prix unitaire"), "19.5000");
    await user.click(screen.getByRole("button", { name: "Ajouter le produit" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("Cette référence existe déjà.");
  });

  it("shows on-hand quantity and does not post an adjustment without a reason", async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn(async (input: string, init?: RequestInit) => {
      const url = String(input);
      if (url.endsWith("/health")) {
        return json({ status: "healthy", database: "up" });
      }
      if (url.endsWith("/api/v1/setup/status")) {
        return json({ complete: true });
      }
      if (url.endsWith("/api/v1/auth/login")) {
        return json({ accessToken: tokenWithRole("Administrator"), refreshToken: "refresh-token" });
      }
      if (url.endsWith("/api/v1/company")) {
        return json({ legalName: "Pexilio Démo" });
      }
      if (url.endsWith("/api/v1/products") || url.endsWith("/api/v1/categories")) {
        return json([]);
      }
      if (url.includes("/api/v1/stock/movements")) {
        return json([]);
      }
      if (url.endsWith("/api/v1/stock/balances")) {
        return json([
          {
            productId: "p1",
            sku: "EAU-1",
            name: "Eau",
            unit: "u",
            quantity: "0.0000",
            minimumQuantity: "1.0000",
            belowMinimum: true,
            atMinimum: false,
          },
        ]);
      }
      throw new Error(url);
    });
    render(<App fetcher={fetchMock} store={memoryStore()} />);
    await screen.findByRole("heading", { name: "Connexion" });
    await user.type(screen.getByLabelText("Adresse e-mail"), "owner@example.com");
    await user.type(screen.getByLabelText("Mot de passe"), "correct-horse-battery");
    await user.click(screen.getByRole("button", { name: "Se connecter" }));

    const balance = await screen.findByText((_content, element) => {
      const text = element?.textContent?.replace(/\s+/g, " ").trim();
      return element?.tagName === "LI" && text === "EAU-1 · Eau · 0.0000 · Minimum 1.0000 · Sous le minimum";
    });
    expect(balance).toBeInTheDocument();
    expect(within(balance).getByText("0.0000")).toHaveAttribute("dir", "ltr");
    await user.click(screen.getByRole("button", { name: "Enregistrer le mouvement" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("Le motif est obligatoire.");
    expect(fetchMock).not.toHaveBeenCalledWith(
      expect.stringContaining("/api/v1/stock/"),
      expect.objectContaining({ method: "POST" }),
    );
  });

  it("shows the stock panel in Arabic with a right-to-left document", async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn(async (input: string) => {
      const url = String(input);
      if (url.endsWith("/health")) {
        return json({ status: "healthy", database: "up" });
      }
      if (url.endsWith("/api/v1/setup/status")) {
        return json({ complete: true });
      }
      if (url.endsWith("/api/v1/auth/login")) {
        return json({ accessToken: tokenWithRole("Cashier"), refreshToken: "refresh-token" });
      }
      if (url.endsWith("/api/v1/company")) {
        return json({ legalName: "Pexilio" });
      }
      if (
        url.endsWith("/api/v1/products") ||
        url.endsWith("/api/v1/categories") ||
        url.endsWith("/api/v1/stock/balances") ||
        url.includes("/api/v1/stock/movements")
      ) {
        return json([]);
      }
      throw new Error(url);
    });
    render(<App initialLocale="ar" fetcher={fetchMock} store={memoryStore()} />);
    await screen.findByRole("heading", { name: "تسجيل الدخول" });
    await user.type(screen.getByLabelText("البريد الإلكتروني"), "owner@example.com");
    await user.type(screen.getByLabelText("كلمة المرور"), "correct-horse-battery");
    await user.click(screen.getByRole("button", { name: "دخول" }));

    expect(await screen.findByRole("heading", { name: "المخزون" })).toBeInTheDocument();
    expect(document.documentElement.lang).toBe("ar");
    expect(document.documentElement.dir).toBe("rtl");
    expect(screen.queryByRole("button", { name: "تسجيل الحركة" })).not.toBeInTheDocument();
  });
});

function tokenWithRole(role: string): string {
  return `e.${btoa(JSON.stringify({ role }))}.s`;
}

function memoryStore(): SecureStore {
  return { load: async () => null, save: async () => "memory", clear: async () => undefined };
}

function status(complete: boolean) {
  return async (input: string) => {
    const url = String(input);
    if (url.endsWith("/health")) {
      return json({ status: "healthy", database: "up" });
    }
    if (url.endsWith("/api/v1/setup/status")) {
      return json({ complete });
    }
    throw new Error(url);
  };
}

function fetcher(impl: (input: string, init?: RequestInit) => Promise<Response>) {
  return vi.fn(impl);
}

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json" },
  });
}
