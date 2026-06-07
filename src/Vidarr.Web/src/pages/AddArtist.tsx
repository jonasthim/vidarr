import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { icons } from "../Components/Icon/Icon";
import { api, type ArtistLookupResult } from "../api";
import { PageContent } from "../Components/Page/PageContent";
import { PageContentBody } from "../Components/Page/PageContentBody";
import { PageToolbar } from "../Components/Page/Toolbar/PageToolbar";
import { PageToolbarSection } from "../Components/Page/Toolbar/PageToolbarSection";
import { PageToolbarButton } from "../Components/Page/Toolbar/PageToolbarButton";
import { Card } from "../components/ui";

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
    <PageContent title="Add Artist">
      <PageToolbar>
        <PageToolbarSection>
          <PageToolbarButton
            label="Search"
            iconName={icons.SEARCH}
            isSpinning={lookupMutation.isPending}
            isDisabled={!query.trim()}
            onPress={() => query.trim() && lookupMutation.mutate(query.trim())}
          />
        </PageToolbarSection>
      </PageToolbar>
      <PageContentBody>
        <Card title="Search the metadata provider">
          <form
            onSubmit={(e) => {
              e.preventDefault();
              if (query.trim()) lookupMutation.mutate(query.trim());
            }}
          >
            <div className="form-row inline">
              <label htmlFor="rf">Root folder</label>
              <input id="rf" value={rootFolder} onChange={(e) => setRootFolder(e.target.value)} />
            </div>
            <div className="form-row inline">
              <label htmlFor="qp">Quality profile</label>
              <select
                id="qp"
                value={profileId ?? ""}
                onChange={(e) =>
                  setProfileId(e.target.value ? Number.parseInt(e.target.value, 10) : null)
                }
              >
                <option value="">Default</option>
                {profiles.data?.map((p) => (
                  <option key={p.id} value={p.id}>{p.name}</option>
                ))}
              </select>
            </div>
            <div className="form-row inline">
              <label htmlFor="q">Artist name</label>
              <input
                id="q"
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder="e.g. Daft Punk"
              />
            </div>
          </form>
          {lookupMutation.error && (
            <div className="error-banner" style={{ marginTop: 12 }}>
              Lookup failed: {(lookupMutation.error as Error).message}
            </div>
          )}
        </Card>

        {results.length > 0 && (
          <Card title="Results">
            <ul className="lookup-results">
              {results.map((r) => (
                <li key={r.providerId}>
                  <div>
                    <strong>{r.name}</strong>
                    <div className="lookup-meta">
                      {r.disambiguation && <span>{r.disambiguation} · </span>}
                      {r.country && <span>{r.country}</span>}
                    </div>
                  </div>
                  <button
                    type="button"
                    className="primary"
                    disabled={addMutation.isPending}
                    onClick={() => addMutation.mutate(r.providerId)}
                  >
                    <FontAwesomeIcon icon={icons.ADD} />
                    Add
                  </button>
                </li>
              ))}
            </ul>
          </Card>
        )}
      </PageContentBody>
    </PageContent>
  );
}
