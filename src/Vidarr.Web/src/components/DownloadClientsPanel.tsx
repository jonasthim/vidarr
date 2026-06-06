import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type DownloadClientSchema, type DownloadClientTestResult } from "../api";

const blankSettings = (schema: DownloadClientSchema): Record<string, string> =>
  Object.fromEntries(schema.fields.map((f) => [f.name, ""]));

export function DownloadClientsPanel(): JSX.Element {
  const queryClient = useQueryClient();
  const list = useQuery({
    queryKey: ["downloadClients"],
    queryFn: api.listDownloadClients,
  });
  const schemas = useQuery({
    queryKey: ["downloadClientSchemas"],
    queryFn: api.listDownloadClientSchemas,
  });

  const [selectedImpl, setSelectedImpl] = useState<string>("");
  const [name, setName] = useState("");
  const [settings, setSettings] = useState<Record<string, string>>({});
  const [testResult, setTestResult] = useState<DownloadClientTestResult | null>(null);

  const create = useMutation({
    mutationFn: () =>
      api.createDownloadClient({
        name,
        implementation: selectedImpl,
        settingsJson: JSON.stringify(settings),
        priority: 1,
        enable: true,
        category: null,
        removesCompletedDownloads: false,
        tags: [],
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["downloadClients"] });
      setName("");
      setSettings({});
      setSelectedImpl("");
      setTestResult(null);
    },
  });

  const remove = useMutation({
    mutationFn: api.deleteDownloadClient,
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ["downloadClients"] }),
  });

  const test = useMutation({
    mutationFn: () => api.testDownloadClient(selectedImpl, JSON.stringify(settings)),
    onSuccess: setTestResult,
  });

  const onPickImpl = (impl: string) => {
    setSelectedImpl(impl);
    const schema = schemas.data?.find((s) => s.implementation === impl);
    setSettings(schema ? blankSettings(schema) : {});
    setTestResult(null);
  };

  const activeSchema = schemas.data?.find((s) => s.implementation === selectedImpl);

  return (
    <div>
      <h3>Download Clients</h3>
      <ul className="profiles-list">
        {list.data?.map((dc) => (
          <li key={dc.id}>
            <strong>{dc.name}</strong>
            <span className="muted">
              · {dc.implementation} · priority {dc.priority} ·{" "}
              {dc.enable ? "enabled" : "disabled"}
            </span>
            <button type="button" onClick={() => remove.mutate(dc.id)}>
              Delete
            </button>
          </li>
        ))}
      </ul>

      <h4>Add download client</h4>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          if (!selectedImpl || !name.trim()) return;
          create.mutate();
        }}
      >
        <label>
          Implementation
          <select
            value={selectedImpl}
            onChange={(e) => onPickImpl(e.target.value)}
          >
            <option value="">—</option>
            {schemas.data?.map((s) => (
              <option key={s.implementation} value={s.implementation}>
                {s.displayName} ({s.protocol})
              </option>
            ))}
          </select>
        </label>

        {activeSchema && (
          <>
            <label>
              Name
              <input
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="Display name"
              />
            </label>
            {activeSchema.fields.map((f) => (
              <label key={f.name}>
                {f.label} {f.required && <span className="muted">*</span>}
                <input
                  type={f.type === "number" ? "number" : f.type === "password" ? "password" : "text"}
                  value={settings[f.name] ?? ""}
                  onChange={(e) =>
                    setSettings({ ...settings, [f.name]: e.target.value })
                  }
                  placeholder={f.helpText ?? ""}
                />
              </label>
            ))}

            <div style={{ display: "flex", gap: "0.5rem" }}>
              <button
                type="button"
                disabled={test.isPending}
                onClick={() => test.mutate()}
              >
                Test connection
              </button>
              <button
                type="submit"
                disabled={create.isPending || !name.trim()}
              >
                Add
              </button>
            </div>

            {testResult && (
              <div className={testResult.success ? "muted" : "error"}>
                {testResult.success ? "✓" : "✗"} {testResult.message ?? "(no message)"}
              </div>
            )}
          </>
        )}
      </form>
    </div>
  );
}
