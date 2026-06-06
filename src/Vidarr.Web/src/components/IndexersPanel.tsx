import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type IndexerSchema, type IndexerTestResult } from "../api";

const blankSettings = (schema: IndexerSchema): Record<string, string> =>
  Object.fromEntries(schema.fields.map((f) => [f.name, ""]));

export function IndexersPanel(): JSX.Element {
  const queryClient = useQueryClient();
  const list = useQuery({ queryKey: ["indexers"], queryFn: api.listIndexers });
  const schemas = useQuery({
    queryKey: ["indexerSchemas"],
    queryFn: api.listIndexerSchemas,
  });

  const [selectedImpl, setSelectedImpl] = useState<string>("");
  const [name, setName] = useState("");
  const [settings, setSettings] = useState<Record<string, string>>({});
  const [testResult, setTestResult] = useState<IndexerTestResult | null>(null);

  const create = useMutation({
    mutationFn: () =>
      api.createIndexer({
        name,
        implementation: selectedImpl,
        settingsJson: JSON.stringify(settings),
        priority: 25,
        enableRss: true,
        enableAutomaticSearch: true,
        enableInteractiveSearch: true,
        preferredDownloadClientId: null,
        tags: [],
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["indexers"] });
      setName("");
      setSettings({});
      setSelectedImpl("");
      setTestResult(null);
    },
  });

  const remove = useMutation({
    mutationFn: api.deleteIndexer,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["indexers"] }),
  });

  const test = useMutation({
    mutationFn: () => api.testIndexer(selectedImpl, JSON.stringify(settings)),
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
      <h3>Indexers</h3>
      <ul className="profiles-list">
        {list.data?.map((ix) => (
          <li key={ix.id}>
            <strong>{ix.name}</strong>
            <span className="muted">
              · {ix.implementation} · priority {ix.priority} · rss{" "}
              {ix.enableRss ? "on" : "off"}
            </span>
            <button type="button" onClick={() => remove.mutate(ix.id)}>
              Delete
            </button>
          </li>
        ))}
      </ul>

      <h4>Add indexer</h4>
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
                {s.displayName}
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
