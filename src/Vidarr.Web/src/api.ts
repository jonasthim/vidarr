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
  youTubeChannelIds: string[];
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

export type CustomFormat = {
  id: number;
  name: string;
  includeCustomFormatWhenRenaming: boolean;
  specificationsJson: string;
};

export type CustomFormatSpec = {
  implementation: string;
  negate?: boolean;
  required?: boolean;
  fields: Record<string, string | number>;
};

export const CUSTOM_FORMAT_IMPLEMENTATIONS: { value: string; label: string; field: { name: string; placeholder: string } }[] = [
  { value: "ReleaseTitleSpecification", label: "Release title (regex)", field: { name: "value", placeholder: "e.g. VEVO|OFFICIAL" } },
  { value: "ReleaseGroupSpecification", label: "Release group (regex)", field: { name: "value", placeholder: "e.g. FOOL" } },
  { value: "IndexerFlagSpecification", label: "Indexer flag", field: { name: "flagKey", placeholder: "e.g. freeleech" } },
  { value: "SourceSpecification", label: "Source", field: { name: "source", placeholder: "Webdl|Bluray|Hdtv|Dvd|Raw" } },
  { value: "ResolutionSpecification", label: "Resolution", field: { name: "resolution", placeholder: "R480p|R720p|R1080p|R2160p" } },
  { value: "LanguageSpecification", label: "Language", field: { name: "language", placeholder: "e.g. en" } },
  { value: "SizeSpecification", label: "Size (bytes)", field: { name: "minBytes", placeholder: "minBytes" } },
  { value: "YouTubeChannelSpecification", label: "YouTube channel", field: { name: "channel", placeholder: "UC... or channel-title substring" } },
];

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

  listCustomFormats: () => call<CustomFormat[]>("/customformat"),
  createCustomFormat: (body: Omit<CustomFormat, "id">) =>
    call<CustomFormat>("/customformat", { method: "POST", body: JSON.stringify(body) }),
  updateCustomFormat: (id: number, body: Omit<CustomFormat, "id">) =>
    call<CustomFormat>(`/customformat/${id}`, { method: "PUT", body: JSON.stringify(body) }),
  deleteCustomFormat: (id: number) =>
    call<unknown>(`/customformat/${id}`, { method: "DELETE" }),

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

  // indexers
  listIndexers: () => call<IndexerConfigDto[]>("/indexer"),
  createIndexer: (body: Omit<IndexerConfigDto, "id">) =>
    call<IndexerConfigDto>("/indexer", {
      method: "POST",
      body: JSON.stringify(body),
    }),
  updateIndexer: (id: number, body: Omit<IndexerConfigDto, "id">) =>
    call<IndexerConfigDto>(`/indexer/${id}`, {
      method: "PUT",
      body: JSON.stringify(body),
    }),
  deleteIndexer: (id: number) =>
    call<unknown>(`/indexer/${id}`, { method: "DELETE" }),
  listIndexerSchemas: () => call<IndexerSchema[]>("/indexer/schema"),
  testIndexer: (implementation: string, settingsJson: string) =>
    call<IndexerTestResult>("/indexer/test", {
      method: "POST",
      body: JSON.stringify({ implementation, settingsJson }),
    }),

  updateYouTubeChannels: (artistId: number, channelIds: string[]) =>
    call<ArtistDto>(`/artist/${artistId}/youtube-channels`, {
      method: "PUT",
      body: JSON.stringify({ channelIds }),
    }),

  // search
  searchReleases: (params: { artistId?: number; musicVideoId?: number; query?: string }) => {
    const qs = new URLSearchParams();
    if (params.artistId !== undefined) qs.set("artistId", String(params.artistId));
    if (params.musicVideoId !== undefined) qs.set("musicVideoId", String(params.musicVideoId));
    if (params.query) qs.set("query", params.query);
    return call<ReleaseSearchResponse>(`/release?${qs}`);
  },

  // system commands
  listCommands: () => call<SystemCommand[]>("/system/command"),
  triggerCommand: (name: string) =>
    call<unknown>(`/system/command/${name}`, { method: "POST" }),
  listJobRuns: (job?: string) =>
    call<JobRun[]>(`/system/jobs/runs${job ? `?job=${job}` : ""}`),
  listHistory: (artistId?: number) =>
    call<HistoryItem[]>(`/history${artistId ? `?artistId=${artistId}` : ""}`),

  // download clients
  listDownloadClients: () => call<DownloadClientConfigDto[]>("/downloadclient"),
  createDownloadClient: (body: Omit<DownloadClientConfigDto, "id">) =>
    call<DownloadClientConfigDto>("/downloadclient", {
      method: "POST",
      body: JSON.stringify(body),
    }),
  deleteDownloadClient: (id: number) =>
    call<unknown>(`/downloadclient/${id}`, { method: "DELETE" }),
  listDownloadClientSchemas: () => call<DownloadClientSchema[]>("/downloadclient/schema"),
  testDownloadClient: (implementation: string, settingsJson: string) =>
    call<DownloadClientTestResult>("/downloadclient/test", {
      method: "POST",
      body: JSON.stringify({ implementation, settingsJson }),
    }),
};

export type DownloadClientConfigDto = {
  id: number;
  name: string;
  implementation: string;
  settingsJson: string;
  priority: number;
  enable: boolean;
  category?: string | null;
  removesCompletedDownloads: boolean;
  tags: number[];
};

export type DownloadClientSchema = {
  implementation: string;
  displayName: string;
  protocol: string;
  fields: IndexerSchemaField[];
};

export type DownloadClientTestResult = { success: boolean; message?: string };

export type SystemCommand = {
  name: string;
  intervalSeconds: number;
  lastRun?: string;
  lastRunOk: boolean;
  recent: JobRun[];
};

export type JobRun = {
  startedAt: string;
  finishedAt?: string;
  succeeded: boolean;
  failureReason?: string;
};

export type HistoryItem = {
  id: number;
  eventType: string;
  date: string;
  artistId?: number;
  musicVideoId?: number;
  releaseTitle?: string;
  indexerName?: string;
  downloadClientName?: string;
  qualityId?: number;
  dataJson: string;
};

export type IndexerConfigDto = {
  id: number;
  name: string;
  implementation: string;
  settingsJson: string;
  priority: number;
  enableRss: boolean;
  enableAutomaticSearch: boolean;
  enableInteractiveSearch: boolean;
  preferredDownloadClientId?: number | null;
  tags: number[];
};

export type IndexerSchemaField = {
  name: string;
  label: string;
  type: string;
  required: boolean;
  helpText?: string;
};

export type IndexerSchema = {
  implementation: string;
  displayName: string;
  fields: IndexerSchemaField[];
};

export type IndexerTestResult = { success: boolean; message?: string };

export type ReleaseSearchResponse = {
  releases: ReleaseSearchItem[];
  failures: { indexerId: number; indexerName: string; reason: string }[];
  indexersQueried: number;
};

export type ReleaseSearchItem = {
  title: string;
  sourceUrl: string;
  magnet?: string;
  sizeBytes?: number;
  publishedAt?: string;
  seeders?: number;
  leechers?: number;
  protocol: string;
  indexerName: string;
  indexerCategory?: string;
};
