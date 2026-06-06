import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api, type ArtistLookupResult } from "../api";

export function AddArtistPage(): JSX.Element {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<ArtistLookupResult[]>([]);
  const [rootFolder, setRootFolder] = useState("/library");
  const queryClient = useQueryClient();

  const lookupMutation = useMutation({
    mutationFn: (q: string) => api.lookupArtist(q),
    onSuccess: setResults,
  });

  const addMutation = useMutation({
    mutationFn: (providerId: string) =>
      api.addArtist("imvdb", providerId, rootFolder),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["artists"] });
      setResults([]);
      setQuery("");
    },
  });

  return (
    <section className="add-artist">
      <h2>Add Artist</h2>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          if (query.trim()) lookupMutation.mutate(query.trim());
        }}
      >
        <label>
          Root folder
          <input
            value={rootFolder}
            onChange={(e) => setRootFolder(e.target.value)}
          />
        </label>
        <label>
          Search artist
          <input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="e.g. Daft Punk"
          />
        </label>
        <button type="submit" disabled={lookupMutation.isPending}>
          {lookupMutation.isPending ? "Searching..." : "Search"}
        </button>
      </form>

      {lookupMutation.error && (
        <div className="error">Lookup failed: {String(lookupMutation.error)}</div>
      )}

      <ul className="results">
        {results.map((r) => (
          <li key={r.providerId}>
            <div>
              <strong>{r.name}</strong>
              {r.country && <span> · {r.country}</span>}
            </div>
            <button
              type="button"
              disabled={addMutation.isPending}
              onClick={() => addMutation.mutate(r.providerId)}
            >
              Add
            </button>
          </li>
        ))}
      </ul>
    </section>
  );
}
