import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";

export function TagsPanel(): JSX.Element {
  const [label, setLabel] = useState("");
  const queryClient = useQueryClient();

  const tags = useQuery({ queryKey: ["tags"], queryFn: api.listTags });
  const create = useMutation({
    mutationFn: api.createTag,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["tags"] });
      setLabel("");
    },
  });
  const remove = useMutation({
    mutationFn: api.deleteTag,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["tags"] }),
  });

  return (
    <div>
      <h3>Tags</h3>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          if (label.trim()) create.mutate(label.trim());
        }}
      >
        <label>
          New tag label
          <input
            value={label}
            onChange={(e) => setLabel(e.target.value)}
            placeholder="e.g. favorites"
          />
        </label>
        <button type="submit" disabled={create.isPending}>
          Add
        </button>
      </form>

      <ul>
        {tags.data?.map((t) => (
          <li key={t.id}>
            <span>{t.label}</span>
            <button type="button" onClick={() => remove.mutate(t.id)}>
              Remove
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}
