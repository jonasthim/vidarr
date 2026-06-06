import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";

export function LibraryPage(): JSX.Element {
  const [selectedArtistId, setSelectedArtistId] = useState<number | null>(null);
  const [channelsDraft, setChannelsDraft] = useState<string>("");
  const queryClient = useQueryClient();

  const artists = useQuery({
    queryKey: ["artists"],
    queryFn: api.listArtists,
  });

  const selectedArtist = artists.data?.find((a) => a.id === selectedArtistId);

  useEffect(() => {
    if (selectedArtist) {
      setChannelsDraft(selectedArtist.youTubeChannelIds.join(", "));
    }
  }, [selectedArtist]);

  const videos = useQuery({
    queryKey: ["videos", selectedArtistId],
    queryFn: () => api.listMusicVideos(selectedArtistId!),
    enabled: selectedArtistId !== null,
  });

  const search = useMutation({
    mutationFn: (artistId: number) => api.triggerArtistSearch(artistId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["queue"] }),
  });

  const saveChannels = useMutation({
    mutationFn: ({ id, channels }: { id: number; channels: string[] }) =>
      api.updateYouTubeChannels(id, channels),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["artists"] }),
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

            <section className="youtube-channels">
              <label>
                YouTube channels (comma-separated UC… IDs)
                <input
                  value={channelsDraft}
                  onChange={(e) => setChannelsDraft(e.target.value)}
                  placeholder="UCabc, UCdef"
                />
              </label>
              <button
                type="button"
                disabled={saveChannels.isPending}
                onClick={() =>
                  saveChannels.mutate({
                    id: selectedArtistId,
                    channels: channelsDraft
                      .split(",")
                      .map((c) => c.trim())
                      .filter(Boolean),
                  })
                }
              >
                Save channels
              </button>
            </section>
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
