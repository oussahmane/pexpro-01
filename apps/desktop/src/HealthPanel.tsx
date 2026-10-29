import { useEffect, useState } from "react";

type HealthFetcher = (input: string) => Promise<Response>;

type HealthView =
  | { kind: "loading" }
  | { kind: "result"; api: string; status: string; database: string };

type HealthCopy = {
  intro: string;
  api: string;
  database: string;
  status: string;
  checking: string;
  checkAgain: string;
};

const englishCopy: HealthCopy = {
  intro: "Development window. The API is checked at {url}.",
  api: "API",
  database: "Database",
  status: "Status",
  checking: "checking",
  checkAgain: "Check again",
};

type HealthPanelProps = {
  apiBaseUrl: string;
  fetcher?: HealthFetcher;
  embedded?: boolean;
  copy?: HealthCopy;
};

export function HealthPanel({
  apiBaseUrl,
  fetcher = fetch,
  embedded = false,
  copy = englishCopy,
}: HealthPanelProps) {
  const [view, setView] = useState<HealthView>({ kind: "loading" });
  const [requestId, setRequestId] = useState(0);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      setView({ kind: "loading" });
      try {
        const response = await fetcher(`${apiBaseUrl}/health`);
        const body = (await response.json()) as { status?: string; database?: string };
        if (cancelled) {
          return;
        }

        setView({
          kind: "result",
          api: "reachable",
          status: body.status ?? "unhealthy",
          database: body.database ?? "down",
        });
      } catch {
        if (!cancelled) {
          setView({
            kind: "result",
            api: "unreachable",
            status: "unhealthy",
            database: "unknown",
          });
        }
      }
    }

    void load();
    return () => {
      cancelled = true;
    };
  }, [apiBaseUrl, fetcher, requestId]);

  const shellClass = embedded
    ? "flex flex-col gap-4 text-start"
    : "mx-auto flex min-h-screen max-w-lg flex-col gap-6 bg-stone-100 px-6 py-16 text-start text-stone-900";
  const Root = embedded ? "section" : "main";

  return (
    <Root className={shellClass}>
      <header className="flex flex-col gap-2">
        {embedded ? null : <h1 className="text-2xl font-semibold">Pexilio</h1>}
        <p className="text-sm text-stone-600">{copy.intro.replace("{url}", apiBaseUrl)}</p>
      </header>
      <dl className="grid grid-cols-[8rem_1fr] gap-y-3 border border-stone-300 bg-white p-4 text-sm">
        <dt>{copy.api}</dt>
        <dd data-testid="api-status">{view.kind === "loading" ? copy.checking : view.api}</dd>
        <dt>{copy.database}</dt>
        <dd data-testid="database-status">
          {view.kind === "loading" ? copy.checking : view.database}
        </dd>
        <dt>{copy.status}</dt>
        <dd data-testid="overall-status">
          {view.kind === "loading" ? copy.checking : view.status}
        </dd>
      </dl>
      <button
        type="button"
        className="w-fit border border-stone-400 bg-white px-3 py-1.5 text-sm"
        onClick={() => setRequestId((current) => current + 1)}
      >
        {copy.checkAgain}
      </button>
    </Root>
  );
}
