import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type ArtistLookupResult } from "../api";

export function AddArtistPage(): JSX.Element {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<ArtistLookupResult[]>([]);
  const [rootFolder, setRootFolder] = useState("/library");
  const [profileId, setProfileId] = useState<number | null>(null);
  const queryClient = useQueryClient();

  const profiles = useQuery({
    queryKey: ["qualityProfiles"],
    queryFn: api.listQualityProfiles,
  });

  const lookupMutation = useMutation({
    mutationFn: (q: string) => api.lookupArtist(q),
    onSuccess: setResults,
  });

  const addMutation = useMutation({
    mutationFn: (providerId: string) =>
      api.addArtist("imvdb", providerId, rootFolder, profileId ?? profiles.data?.[0]?.id ?? 1),
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
          Quality profile
          <select
            value={profileId ?? ""}
            onChange={(e) =>
              setProfileId(
                e.target.value ? Number.parseInt(e.target.value, 10) : null,
              )
            }
          >
            <option value="">Default</option>
            {profiles.data?.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </select>
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
