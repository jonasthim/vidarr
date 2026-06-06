const API_KEY = (window as { VIDARR_API_KEY?: string }).VIDARR_API_KEY ?? "";

export type ArtistLookupResult = {
  providerId: string;
  name: string;
  disambiguation?: string;
  country?: string;
  thumbnailUrl?: string;
};

export type ArtistDto = {
  id: number;
  name: string;
  sortName: string;
  country?: string;
  monitored: boolean;
  monitorMode: string;
  rootFolderPath: string;
  added: string;
};

export type MusicVideoDto = {
  id: number;
  artistId: number;
  title: string;
  year?: number;
  type: string;
  monitored: boolean;
  hasFile: boolean;
};

export type QueueItem = {
  id: string;
  title: string;
  status: string;
  totalBytes?: number;
  remainingBytes?: number;
  etaSeconds?: number;
  outputPath?: string;
  message?: string;
};

async function call<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers);
  if (API_KEY) headers.set("X-Api-Key", API_KEY);
  headers.set("Accept", "application/json");
  if (init?.body) headers.set("Content-Type", "application/json");
  const resp = await fetch(`/api/v1${path}`, { ...init, headers });
  if (!resp.ok) {
    throw new Error(`HTTP ${resp.status}: ${await resp.text()}`);
  }
  return (await resp.json()) as T;
}

export const api = {
  lookupArtist: (query: string) =>
    call<ArtistLookupResult[]>("/artist/lookup", {
      method: "POST",
      body: JSON.stringify({ query }),
    }),
  addArtist: (provider: string, providerId: string, rootFolderPath: string) =>
    call<ArtistDto>("/artist", {
      method: "POST",
      body: JSON.stringify({
        provider,
        providerId,
        rootFolderPath,
        qualityProfileId: 1,
        monitorMode: "All",
      }),
    }),
  listArtists: () => call<ArtistDto[]>("/artist"),
  listMusicVideos: (artistId: number) =>
    call<MusicVideoDto[]>(`/musicvideo?artistId=${artistId}`),
  triggerArtistSearch: (artistId: number) =>
    call<unknown>("/command", {
      method: "POST",
      body: JSON.stringify({ name: "ArtistSearch", artistId }),
    }),
  listQueue: () => call<QueueItem[]>("/queue"),
};
