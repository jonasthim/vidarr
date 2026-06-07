const API_KEY = (window as { VIDARR_API_KEY?: string }).VIDARR_API_KEY ?? "";

export type ArtistLookupResult = {
  providerId: string;
  name: string;
  disambiguation?: string;
  country?: string;
  thumbnailUrl?: string;
};

export type ArtistImage = { kind: string; url: string };

export type ArtistDto = {
  id: number;
  name: string;
  sortName: string;
  disambiguation?: string | null;
  country?: string;
  monitored: boolean;
  monitorMode: string;
  rootFolderPath: string;
  added: string;
  lastInfoSync?: string | null;
  youTubeChannelIds: string[];
  genres: string[];
  images: ArtistImage[];
};

export type ArtistDetailsDto = {
  artist: ArtistDto;
  aliases: string[];
  videoCount: number;
  downloadedCount: number;
};

export type MusicVideoDto = {
  id: number;
  artistId: number;
  title: string;
  year?: number;
  releaseDate?: string | null;
  type: string;
  director?: string | null;
  runtimeSeconds?: number | null;
  thumbnailUrl?: string | null;
  monitored: boolean;
  hasFile: boolean;
  genres: string[];
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
  const resp = await fetch(`/api/v1${path}`, {
    credentials: "same-origin",
    ...init,
    headers,
  });
  if (!resp.ok) {
    throw new Error(`HTTP ${resp.status}: ${await resp.text()}`);
  }
  if (resp.status === 204) return undefined as T;
  return (await resp.json()) as T;
}

export type AuthStatus = {
  method: string;
  enabled: boolean;
  authenticated: boolean;
  username?: string | null;
};

export type HealthIssue = {
  checkName: string;
  source: string;
  severity: "Info" | "Warning" | "Error";
  message: string;
};

export type HealthStatus = {
  lastRun?: string | null;
  issues: HealthIssue[];
};

export type BackupArtifact = {
  fileName: string;
  sizeBytes: number;
  createdAt: string;
};

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

export type BlocklistEntry = {
  id: number;
  artistId?: number;
  musicVideoId?: number;
  releaseTitle: string;
  indexerName: string;
  reason?: string;
  date: string;
};

export type DiscoveryRule = {
  id: number;
  name: string;
  enabled: boolean;
  conditionsJson: string;
  actionJson: string;
  lastRun?: string;
};

export type DiscoveryEvaluationDto = {
  ruleId: number;
  ruleName: string;
  matched: number;
  videosMonitored: number;
};

export type ReleaseGrab = {
  title: string;
  sourceUrl: string;
  magnet?: string;
  sizeBytes?: number;
  publishedAt?: string;
  seeders?: number;
  leechers?: number;
  protocol?: string;
  indexerName?: string;
  indexerCategory?: string;
  musicVideoIds?: number[];
  extraMetadata?: Record<string, string>;
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
  getArtist: (id: number) => call<ArtistDto>(`/artist/${id}`),
  getArtistDetails: (id: number) => call<ArtistDetailsDto>(`/artist/${id}/details`),
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

  listBlocklist: () => call<BlocklistEntry[]>("/blocklist"),
  addBlocklist: (body: { releaseTitle: string; indexerName?: string; reason?: string }) =>
    call<BlocklistEntry>("/blocklist", { method: "POST", body: JSON.stringify(body) }),
  deleteBlocklist: (id: number) =>
    call<unknown>(`/blocklist/${id}`, { method: "DELETE" }),
  removeQueueItem: (id: string, blocklist: boolean) =>
    call<unknown>(`/queue/${encodeURIComponent(id)}?blocklist=${blocklist}`, { method: "DELETE" }),

  listDiscoveryRules: () => call<DiscoveryRule[]>("/discoveryrule"),
  createDiscoveryRule: (body: Omit<DiscoveryRule, "id" | "lastRun">) =>
    call<DiscoveryRule>("/discoveryrule", { method: "POST", body: JSON.stringify(body) }),
  deleteDiscoveryRule: (id: number) =>
    call<unknown>(`/discoveryrule/${id}`, { method: "DELETE" }),
  evaluateDiscoveryRule: (id: number) =>
    call<DiscoveryEvaluationDto>(`/discoveryrule/evaluate/${id}`, { method: "POST" }),
  evaluateAllDiscoveryRules: () =>
    call<DiscoveryEvaluationDto[]>("/discoveryrule/evaluate-all", { method: "POST" }),

  grabRelease: (body: ReleaseGrab) =>
    call<{ downloadId: string; title: string }>("/release/grab", {
      method: "POST",
      body: JSON.stringify(body),
    }),

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
  listNotifications: () => call<NotificationConfigDto[]>("/notification"),
  createNotification: (body: Omit<NotificationConfigDto, "id">) =>
    call<NotificationConfigDto>("/notification", { method: "POST", body: JSON.stringify(body) }),
  deleteNotification: (id: number) =>
    call<unknown>(`/notification/${id}`, { method: "DELETE" }),
  listNotificationSchemas: () => call<NotificationSchema[]>("/notification/schema"),
  testNotification: (implementation: string, settingsJson: string) =>
    call<{ success: boolean; message?: string }>("/notification/test", {
      method: "POST",
      body: JSON.stringify({ implementation, settingsJson }),
    }),

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

  // system
  getSystemStatus: () =>
    call<{ version: string; buildtime: string; authenticated: boolean }>("/system/status"),

  // backups
  listBackups: () => call<BackupArtifact[]>("/system/backup"),
  createBackup: () => call<BackupArtifact>("/system/backup", { method: "POST" }),
  deleteBackup: (fileName: string) =>
    call<void>(`/system/backup/${encodeURIComponent(fileName)}`, { method: "DELETE" }),

  // api key (sonarr-style: persisted, rotatable from Settings)
  getApiKey: () => call<{ apiKey: string }>("/system/apikey"),
  rotateApiKey: () =>
    call<{ apiKey: string }>("/system/apikey/rotate", { method: "POST" }),

  // auth
  getAuthStatus: () => call<AuthStatus>("/auth/status"),
  login: (username: string, password: string) =>
    call<AuthStatus>("/auth/login", {
      method: "POST",
      body: JSON.stringify({ username, password }),
    }),
  logout: () => call<void>("/auth/logout", { method: "POST" }),

  // health
  getHealth: () => call<HealthStatus>("/health"),
  runHealth: () => call<HealthStatus>("/health/run", { method: "POST" }),

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

export type NotificationConfigDto = {
  id: number;
  name: string;
  implementation: string;
  settingsJson: string;
  enable: boolean;
  subscribedEvents: number[];
  tags: number[];
};

export type NotificationSchema = {
  implementation: string;
  displayName: string;
  fields: IndexerSchemaField[];
  supportedEvents: string[];
};

export const NOTIFICATION_EVENT_TYPES: { value: number; label: string }[] = [
  { value: 1, label: "OnGrab" },
  { value: 2, label: "OnImport" },
  { value: 3, label: "OnUpgrade" },
  { value: 4, label: "OnDelete" },
  { value: 5, label: "OnHealthIssue" },
  { value: 6, label: "OnApplicationUpdate" },
  { value: 7, label: "OnTest" },
];

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
