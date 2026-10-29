export type CredentialStore = "os" | "memory";

export type SecureStore = {
  load: () => Promise<string | null>;
  save: (token: string) => Promise<CredentialStore>;
  clear: () => Promise<void>;
};

let memoryToken: string | null = null;

async function invokeCommand(command: string, args?: Record<string, unknown>): Promise<unknown> {
  const { invoke } = await import("@tauri-apps/api/core");
  return invoke(command, args);
}

export const secureStore: SecureStore = {
  async load() {
    try {
      const value = await invokeCommand("load_refresh_token");
      if (typeof value === "string" && value.length > 0) {
        memoryToken = null;
        return value;
      }
    } catch {
      // The OS credential store is unavailable in this process.
    }

    return memoryToken;
  },

  async save(token) {
    try {
      await invokeCommand("store_refresh_token", { token });
      memoryToken = null;
      return "os";
    } catch {
      memoryToken = token;
      return "memory";
    }
  },

  async clear() {
    memoryToken = null;
    try {
      await invokeCommand("clear_refresh_token");
    } catch {
      // Nothing persisted in the OS store.
    }
  },
};
