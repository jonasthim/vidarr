import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type QualityProfile } from "../api";

const blankDraft = (): Omit<QualityProfile, "id"> => ({
  name: "",
  allowedQualityIds: [],
  cutoffQualityId: 0,
  upgradeAllowed: true,
  minFormatScore: 0,
  tags: [],
});

export function ProfilesPanel(): JSX.Element {
  const queryClient = useQueryClient();
  const profiles = useQuery({
    queryKey: ["qualityProfiles"],
    queryFn: api.listQualityProfiles,
  });
  const qualities = useQuery({
    queryKey: ["qualityDefinitions"],
    queryFn: api.listQualityDefinitions,
  });

  const [draft, setDraft] = useState<Omit<QualityProfile, "id">>(blankDraft);

  const create = useMutation({
    mutationFn: api.createQualityProfile,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["qualityProfiles"] });
      setDraft(blankDraft());
    },
  });
  const remove = useMutation({
    mutationFn: api.deleteQualityProfile,
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ["qualityProfiles"] }),
  });

  const toggleAllowed = (id: number, on: boolean) =>
    setDraft((d) => ({
      ...d,
      allowedQualityIds: on
        ? [...d.allowedQualityIds, id]
        : d.allowedQualityIds.filter((x) => x !== id),
    }));

  return (
    <div>
      <h3>Quality Profiles</h3>
      <ul className="profiles-list">
        {profiles.data?.map((p) => (
          <li key={p.id}>
            <strong>{p.name}</strong>
            <span className="muted">
              · {p.allowedQualityIds.length} qualities · cutoff{" "}
              {p.cutoffQualityId} · upgrade{" "}
              {p.upgradeAllowed ? "enabled" : "disabled"}
            </span>
            <button type="button" onClick={() => remove.mutate(p.id)}>
              Delete
            </button>
          </li>
        ))}
      </ul>

      <h4>Create profile</h4>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          if (!draft.name.trim() || draft.allowedQualityIds.length === 0) return;
          create.mutate(draft);
        }}
      >
        <label>
          Name
          <input
            value={draft.name}
            onChange={(e) =>
              setDraft((d) => ({ ...d, name: e.target.value }))
            }
          />
        </label>
        <fieldset>
          <legend>Allowed qualities</legend>
          {qualities.data?.map((q) => (
            <label key={q.id} className="checkbox-row">
              <input
                type="checkbox"
                checked={draft.allowedQualityIds.includes(q.id)}
                onChange={(e) => toggleAllowed(q.id, e.target.checked)}
              />
              {q.name} <span className="muted">({q.resolution})</span>
            </label>
          ))}
        </fieldset>
        <label>
          Cutoff quality
          <select
            value={draft.cutoffQualityId}
            onChange={(e) =>
              setDraft((d) => ({
                ...d,
                cutoffQualityId: Number.parseInt(e.target.value, 10),
              }))
            }
          >
            <option value={0}>—</option>
            {draft.allowedQualityIds.map((id) => {
              const q = qualities.data?.find((x) => x.id === id);
              return (
                <option value={id} key={id}>
                  {q?.name ?? id}
                </option>
              );
            })}
          </select>
        </label>
        <label className="checkbox-row">
          <input
            type="checkbox"
            checked={draft.upgradeAllowed}
            onChange={(e) =>
              setDraft((d) => ({ ...d, upgradeAllowed: e.target.checked }))
            }
          />
          Allow upgrades
        </label>
        <button
          type="submit"
          disabled={
            create.isPending ||
            !draft.name.trim() ||
            draft.allowedQualityIds.length === 0
          }
        >
          Create profile
        </button>
      </form>
    </div>
  );
}
