import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";

type Condition = { type: string; values?: string[]; value?: number };
type Action = { qualityProfileId?: number; rootFolderPath?: string; monitorMode?: string; tags?: number[] };

const CONDITION_TYPES = [
  { value: "GenreIn", label: "Genre in (CSV)", needsValues: true },
  { value: "TypeIn", label: "Type in (CSV: Official|Live|Lyric|Acoustic|...)", needsValues: true },
  { value: "CountryIn", label: "Country in (CSV)", needsValues: true },
  { value: "YearGte", label: "Year ≥", needsValues: false },
  { value: "YearLte", label: "Year ≤", needsValues: false },
  { value: "DecadeEq", label: "Decade =", needsValues: false },
];

export function DiscoveryRulesPanel(): JSX.Element {
  const queryClient = useQueryClient();
  const rules = useQuery({ queryKey: ["discoveryRules"], queryFn: api.listDiscoveryRules });

  const [name, setName] = useState("");
  const [conditions, setConditions] = useState<Condition[]>([]);
  const [action, setAction] = useState<Action>({ monitorMode: "All" });

  const create = useMutation({
    mutationFn: () =>
      api.createDiscoveryRule({
        name,
        enabled: true,
        conditionsJson: JSON.stringify(conditions),
        actionJson: JSON.stringify(action),
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["discoveryRules"] });
      setName("");
      setConditions([]);
    },
  });

  const remove = useMutation({
    mutationFn: api.deleteDiscoveryRule,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["discoveryRules"] }),
  });

  const evaluate = useMutation({
    mutationFn: api.evaluateDiscoveryRule,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["discoveryRules"] }),
  });

  const addCondition = (type: string) => {
    if (!type) return;
    const meta = CONDITION_TYPES.find((c) => c.value === type);
    setConditions((cur) => [
      ...cur,
      meta?.needsValues ? { type, values: [] } : { type, value: 0 },
    ]);
  };

  const updateCondition = (i: number, update: Partial<Condition>) =>
    setConditions((cur) => cur.map((c, idx) => (idx === i ? { ...c, ...update } : c)));

  const removeCondition = (i: number) =>
    setConditions((cur) => cur.filter((_, idx) => idx !== i));

  return (
    <div>
      <h3>Discovery Rules</h3>
      <ul className="profiles-list">
        {rules.data?.map((r) => (
          <li key={r.id}>
            <strong>{r.name}</strong>
            <span className="muted"> · {r.enabled ? "enabled" : "disabled"}</span>
            <button type="button" onClick={() => evaluate.mutate(r.id)}>
              Run now
            </button>
            <button type="button" onClick={() => remove.mutate(r.id)}>
              Delete
            </button>
          </li>
        ))}
      </ul>

      {evaluate.data && (
        <p className="muted">
          Last evaluation: {evaluate.data.ruleName} matched {evaluate.data.matched},
          monitored {evaluate.data.videosMonitored}.
        </p>
      )}

      <h4>Create rule</h4>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          if (!name.trim() || conditions.length === 0) return;
          create.mutate();
        }}
      >
        <label>
          Name
          <input value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Synthwave 2020s" />
        </label>

        <fieldset>
          <legend>Conditions (logical AND)</legend>
          {conditions.map((c, i) => {
            const meta = CONDITION_TYPES.find((x) => x.value === c.type);
            return (
              <div key={i} className="custom-format-spec">
                <strong>{meta?.label ?? c.type}</strong>
                {meta?.needsValues ? (
                  <input
                    type="text"
                    placeholder="comma-separated"
                    value={(c.values ?? []).join(", ")}
                    onChange={(e) =>
                      updateCondition(i, {
                        values: e.target.value.split(",").map((s) => s.trim()).filter(Boolean),
                      })
                    }
                  />
                ) : (
                  <input
                    type="number"
                    value={c.value ?? 0}
                    onChange={(e) =>
                      updateCondition(i, { value: Number.parseInt(e.target.value, 10) || 0 })
                    }
                  />
                )}
                <span />
                <span />
                <button type="button" onClick={() => removeCondition(i)}>
                  Remove
                </button>
              </div>
            );
          })}
          <select
            value=""
            onChange={(e) => {
              addCondition(e.target.value);
              e.target.value = "";
            }}
          >
            <option value="">+ Add condition…</option>
            {CONDITION_TYPES.map((c) => (
              <option key={c.value} value={c.value}>
                {c.label}
              </option>
            ))}
          </select>
        </fieldset>

        <fieldset>
          <legend>Action</legend>
          <label>
            Quality profile id
            <input
              type="number"
              value={action.qualityProfileId ?? ""}
              onChange={(e) =>
                setAction((a) => ({
                  ...a,
                  qualityProfileId: e.target.value ? Number.parseInt(e.target.value, 10) : undefined,
                }))
              }
            />
          </label>
          <label>
            Root folder path
            <input
              value={action.rootFolderPath ?? ""}
              onChange={(e) =>
                setAction((a) => ({ ...a, rootFolderPath: e.target.value || undefined }))
              }
            />
          </label>
          <label>
            Monitor mode
            <select
              value={action.monitorMode ?? "All"}
              onChange={(e) => setAction((a) => ({ ...a, monitorMode: e.target.value }))}
            >
              <option>All</option>
              <option>NewOnly</option>
              <option>None</option>
            </select>
          </label>
        </fieldset>

        <button type="submit" disabled={create.isPending || !name.trim() || conditions.length === 0}>
          Create rule
        </button>
      </form>
    </div>
  );
}
