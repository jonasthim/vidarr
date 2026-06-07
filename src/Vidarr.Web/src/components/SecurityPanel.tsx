import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { api } from "../api";

export function SecurityPanel(): JSX.Element {
  const queryClient = useQueryClient();
  const [revealed, setRevealed] = useState(false);
  const [copied, setCopied] = useState(false);

  const apiKeyQuery = useQuery({
    queryKey: ["api-key"],
    queryFn: api.getApiKey,
    refetchInterval: false,
  });

  const rotateMutation = useMutation({
    mutationFn: api.rotateApiKey,
    onSuccess: (data) => {
      // Update the in-memory key so subsequent /api/v1 calls authenticate.
      (window as { VIDARR_API_KEY?: string }).VIDARR_API_KEY = data.apiKey;
      queryClient.invalidateQueries({ queryKey: ["api-key"] });
    },
  });

  const onCopy = async () => {
    if (!apiKeyQuery.data?.apiKey) return;
    await navigator.clipboard.writeText(apiKeyQuery.data.apiKey);
    setCopied(true);
    window.setTimeout(() => setCopied(false), 1500);
  };

  const onRotate = () => {
    if (
      window.confirm(
        "Generate a new API key? Any clients, webhooks, or scripts using the old key will need to be updated.",
      )
    ) {
      rotateMutation.mutate();
    }
  };

  if (apiKeyQuery.isLoading) return <p>Loading API key…</p>;
  if (apiKeyQuery.error)
    return <p className="error">Failed to load: {(apiKeyQuery.error as Error).message}</p>;

  const value = apiKeyQuery.data!.apiKey;
  const masked = "•".repeat(Math.max(value.length, 8));

  return (
    <section>
      <div className="page-header">
        <h2>Security</h2>
      </div>
      <p className="muted">
        Vidarr's REST API authenticates every request with this key. Pass it as the
        <code> X-Api-Key </code> header (or <code>?apikey=</code> query parameter). The
        web UI uses it automatically.
      </p>

      <div className="security-row">
        <label>
          <span>API key</span>
          <input
            type="text"
            readOnly
            value={revealed ? value : masked}
            onFocus={(e) => e.currentTarget.select()}
          />
        </label>
        <div className="security-actions">
          <button type="button" onClick={() => setRevealed((v) => !v)}>
            {revealed ? "Hide" : "Show"}
          </button>
          <button type="button" onClick={onCopy} disabled={!revealed && !copied}>
            {copied ? "Copied" : "Copy"}
          </button>
          <button
            type="button"
            className="danger"
            onClick={onRotate}
            disabled={rotateMutation.isPending}
          >
            {rotateMutation.isPending ? "Regenerating…" : "Regenerate"}
          </button>
        </div>
      </div>

      {rotateMutation.error && (
        <p className="error">{(rotateMutation.error as Error).message}</p>
      )}
      {rotateMutation.isSuccess && (
        <p className="ok">
          New key active. The web UI is already using it; update any external
          scripts or webhooks with the new value.
        </p>
      )}
    </section>
  );
}
