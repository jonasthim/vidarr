import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, CUSTOM_FORMAT_IMPLEMENTATIONS, type CustomFormatSpec } from "../api";

const blankSpec = (impl: string): CustomFormatSpec => ({
  implementation: impl,
  negate: false,
  required: false,
  fields: {},
});

export function CustomFormatsPanel(): JSX.Element {
  const queryClient = useQueryClient();
  const formats = useQuery({
    queryKey: ["customFormats"],
    queryFn: api.listCustomFormats,
  });

  const [name, setName] = useState("");
  const [specs, setSpecs] = useState<CustomFormatSpec[]>([]);

  const create = useMutation({
    mutationFn: () =>
      api.createCustomFormat({
        name,
        includeCustomFormatWhenRenaming: false,
        specificationsJson: JSON.stringify(specs),
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["customFormats"] });
      setName("");
      setSpecs([]);
    },
  });

  const remove = useMutation({
    mutationFn: api.deleteCustomFormat,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["customFormats"] }),
  });

  const addSpec = (impl: string) => {
    if (!impl) return;
    setSpecs((current) => [...current, blankSpec(impl)]);
  };

  const updateSpec = (i: number, update: Partial<CustomFormatSpec>) =>
    setSpecs((current) =>
      current.map((s, idx) => (idx === i ? { ...s, ...update, fields: { ...s.fields, ...update.fields } } : s)),
    );

  const removeSpec = (i: number) =>
    setSpecs((current) => current.filter((_, idx) => idx !== i));

  const fieldFor = (impl: string) =>
    CUSTOM_FORMAT_IMPLEMENTATIONS.find((c) => c.value === impl)?.field ?? { name: "value", placeholder: "" };

  const previewJson = useMemo(() => JSON.stringify(specs, null, 2), [specs]);

  return (
    <div>
      <h3>Custom Formats</h3>
      <ul className="profiles-list">
        {formats.data?.map((f) => (
          <li key={f.id}>
            <strong>{f.name}</strong>
            <span className="muted"> · {JSON.parse(f.specificationsJson || "[]").length} specs</span>
            <button type="button" onClick={() => remove.mutate(f.id)}>
              Delete
            </button>
          </li>
        ))}
      </ul>

      <h4>Create custom format</h4>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          if (!name.trim() || specs.length === 0) return;
          create.mutate();
        }}
      >
        <label>
          Name
          <input value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Prefer VEVO" />
        </label>

        <fieldset>
          <legend>Specifications</legend>
          {specs.map((s, i) => {
            const impl = CUSTOM_FORMAT_IMPLEMENTATIONS.find((c) => c.value === s.implementation);
            const field = fieldFor(s.implementation);
            return (
              <div key={i} className="custom-format-spec">
                <strong>{impl?.label ?? s.implementation}</strong>
                <input
                  type="text"
                  placeholder={field.placeholder}
                  value={(s.fields[field.name] as string) ?? ""}
                  onChange={(e) => updateSpec(i, { fields: { [field.name]: e.target.value } })}
                />
                <label className="checkbox-row">
                  <input
                    type="checkbox"
                    checked={s.required ?? false}
                    onChange={(e) => updateSpec(i, { required: e.target.checked })}
                  />
                  Required
                </label>
                <label className="checkbox-row">
                  <input
                    type="checkbox"
                    checked={s.negate ?? false}
                    onChange={(e) => updateSpec(i, { negate: e.target.checked })}
                  />
                  Negate
                </label>
                <button type="button" onClick={() => removeSpec(i)}>
                  Remove
                </button>
              </div>
            );
          })}
          <select
            value=""
            onChange={(e) => {
              addSpec(e.target.value);
              e.target.value = "";
            }}
          >
            <option value="">+ Add specification…</option>
            {CUSTOM_FORMAT_IMPLEMENTATIONS.map((c) => (
              <option key={c.value} value={c.value}>
                {c.label}
              </option>
            ))}
          </select>
        </fieldset>

        <details>
          <summary>Preview JSON</summary>
          <pre>{previewJson}</pre>
        </details>

        <button type="submit" disabled={create.isPending || !name.trim() || specs.length === 0}>
          Create
        </button>
      </form>
    </div>
  );
}
