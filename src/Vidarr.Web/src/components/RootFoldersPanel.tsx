import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";

function formatBytes(n: number): string {
  if (n > 1_000_000_000_000) return `${(n / 1_000_000_000_000).toFixed(1)} TB`;
  if (n > 1_000_000_000) return `${(n / 1_000_000_000).toFixed(1)} GB`;
  if (n > 1_000_000) return `${(n / 1_000_000).toFixed(1)} MB`;
  return `${n} B`;
}

export function RootFoldersPanel(): JSX.Element {
  const [path, setPath] = useState("");
  const queryClient = useQueryClient();

  const folders = useQuery({
    queryKey: ["rootFolders"],
    queryFn: api.listRootFolders,
  });
  const create = useMutation({
    mutationFn: api.createRootFolder,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["rootFolders"] });
      setPath("");
    },
  });
  const remove = useMutation({
    mutationFn: api.deleteRootFolder,
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ["rootFolders"] }),
  });

  return (
    <div>
      <h3>Root Folders</h3>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          if (path.trim()) create.mutate(path.trim());
        }}
      >
        <label>
          Path
          <input
            value={path}
            onChange={(e) => setPath(e.target.value)}
            placeholder="/library/music-videos"
          />
        </label>
        <button type="submit" disabled={create.isPending}>
          Add
        </button>
      </form>

      <table>
        <thead>
          <tr>
            <th>Path</th>
            <th>Accessible</th>
            <th>Free</th>
            <th>Total</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {folders.data?.map((f) => (
            <tr key={f.id}>
              <td>{f.path}</td>
              <td>{f.accessible ? "yes" : "no"}</td>
              <td>{formatBytes(f.freeBytes)}</td>
              <td>{formatBytes(f.totalBytes)}</td>
              <td>
                <button type="button" onClick={() => remove.mutate(f.id)}>
                  Remove
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
