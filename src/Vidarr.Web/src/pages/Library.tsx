import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";

export function LibraryPage(): JSX.Element {
  const [selectedArtistId, setSelectedArtistId] = useState<number | null>(null);
  const queryClient = useQueryClient();

  const artists = useQuery({
    queryKey: ["artists"],
    queryFn: api.listArtists,
  });

  const videos = useQuery({
    queryKey: ["videos", selectedArtistId],
    queryFn: () => api.listMusicVideos(selectedArtistId!),
    enabled: selectedArtistId !== null,
  });

  const search = useMutation({
    mutationFn: (artistId: number) => api.triggerArtistSearch(artistId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["queue"] }),
  });

  return (
    <section className="library">
      <div className="artists">
        <h2>Artists ({artists.data?.length ?? 0})</h2>
        {artists.isLoading && <p>Loading...</p>}
        <ul>
          {artists.data?.map((a) => (
            <li
              key={a.id}
              onClick={() => setSelectedArtistId(a.id)}
              className={selectedArtistId === a.id ? "selected" : ""}
              role="button"
              tabIndex={0}
              onKeyDown={(e) => {
                if (e.key === "Enter") setSelectedArtistId(a.id);
              }}
            >
              <strong>{a.name}</strong>
              {a.country && <span> · {a.country}</span>}
            </li>
          ))}
        </ul>
      </div>
      <div className="videos">
        {selectedArtistId !== null && (
          <>
            <header>
              <h2>Videos</h2>
              <button
                type="button"
                disabled={search.isPending}
                onClick={() => search.mutate(selectedArtistId)}
              >
                Search now
              </button>
            </header>
            <ul>
              {videos.data?.map((v) => (
                <li key={v.id}>
                  <strong>{v.title}</strong>
                  {v.year && <span> ({v.year})</span>} ·{" "}
                  {v.hasFile ? "downloaded" : v.monitored ? "wanted" : "ignored"}
                </li>
              ))}
            </ul>
          </>
        )}
      </div>
    </section>
  );
}
