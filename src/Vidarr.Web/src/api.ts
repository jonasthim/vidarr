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

export type Quality = {
  id: number;
  name: string;
  resolution: string;
  source: string;
};

export type QualityProfile = {
  id: number;
  name: string;
  allowedQualityIds: number[];
  cutoffQualityId: number;
  upgradeAllowed: boolean;
  minFormatScore: number;
  minSizeBytes?: number;
  maxSizeBytes?: number;
  tags: number[];
};

export type Tag = { id: number; label: string };

export type RootFolder = {
  id: number;
  path: string;
  accessible: boolean;
  freeBytes: number;
  totalBytes: number;
};

export type HostConfig = {
  instanceName: string;
  urlBase?: string;
  logLevel: string;
};

export type NamingConfig = {
  artistFolderTemplate: string;
  fileTemplate: string;
};

export type MediaManagementConfig = {
  fileOperation: string;
  replaceIllegalCharacters: boolean;
  illegalCharacterReplacement: string;
};

export const api = {
  lookupArtist: (query: string) =>
    call<ArtistLookupResult[]>("/artist/lookup", {
      method: "POST",
      body: JSON.stringify({ query }),
    }),
  addArtist: (
    provider: string,
    providerId: string,
    rootFolderPath: string,
    qualityProfileId: number,
  ) =>
    call<ArtistDto>("/artist", {
      method: "POST",
      body: JSON.stringify({
        provider,
        providerId,
        rootFolderPath,
        qualityProfileId,
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

  // settings
  listQualityDefinitions: () => call<Quality[]>("/qualitydefinition"),
  listQualityProfiles: () => call<QualityProfile[]>("/qualityprofile"),
  createQualityProfile: (body: Omit<QualityProfile, "id">) =>
    call<QualityProfile>("/qualityprofile", {
      method: "POST",
      body: JSON.stringify(body),
    }),
  updateQualityProfile: (id: number, body: Omit<QualityProfile, "id">) =>
    call<QualityProfile>(`/qualityprofile/${id}`, {
      method: "PUT",
      body: JSON.stringify(body),
    }),
  deleteQualityProfile: (id: number) =>
    call<unknown>(`/qualityprofile/${id}`, { method: "DELETE" }),

  listTags: () => call<Tag[]>("/tag"),
  createTag: (label: string) =>
    call<Tag>("/tag", { method: "POST", body: JSON.stringify({ label }) }),
  deleteTag: (id: number) =>
    call<unknown>(`/tag/${id}`, { method: "DELETE" }),

  listRootFolders: () => call<RootFolder[]>("/rootfolder"),
  createRootFolder: (path: string) =>
    call<RootFolder>("/rootfolder", {
      method: "POST",
      body: JSON.stringify({ path }),
    }),
  deleteRootFolder: (id: number) =>
    call<unknown>(`/rootfolder/${id}`, { method: "DELETE" }),

  getHostConfig: () => call<HostConfig>("/config/host"),
  putHostConfig: (body: HostConfig) =>
    call<HostConfig>("/config/host", {
      method: "PUT",
      body: JSON.stringify(body),
    }),
  getNamingConfig: () => call<NamingConfig>("/config/naming"),
  putNamingConfig: (body: NamingConfig) =>
    call<NamingConfig>("/config/naming", {
      method: "PUT",
      body: JSON.stringify(body),
    }),
  getMediaManagementConfig: () =>
    call<MediaManagementConfig>("/config/mediamanagement"),
  putMediaManagementConfig: (body: MediaManagementConfig) =>
    call<MediaManagementConfig>("/config/mediamanagement", {
      method: "PUT",
      body: JSON.stringify(body),
    }),
};
