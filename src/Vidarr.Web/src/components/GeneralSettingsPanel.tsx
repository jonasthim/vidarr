import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type HostConfig, type MediaManagementConfig, type NamingConfig } from "../api";

export function GeneralSettingsPanel(): JSX.Element {
  const queryClient = useQueryClient();

  const host = useQuery({ queryKey: ["hostConfig"], queryFn: api.getHostConfig });
  const naming = useQuery({
    queryKey: ["namingConfig"],
    queryFn: api.getNamingConfig,
  });
  const mm = useQuery({
    queryKey: ["mediaManagementConfig"],
    queryFn: api.getMediaManagementConfig,
  });

  const [hostDraft, setHostDraft] = useState<HostConfig | null>(null);
  const [namingDraft, setNamingDraft] = useState<NamingConfig | null>(null);
  const [mmDraft, setMmDraft] = useState<MediaManagementConfig | null>(null);

  useEffect(() => {
    if (host.data && !hostDraft) setHostDraft(host.data);
  }, [host.data, hostDraft]);
  useEffect(() => {
    if (naming.data && !namingDraft) setNamingDraft(naming.data);
  }, [naming.data, namingDraft]);
  useEffect(() => {
    if (mm.data && !mmDraft) setMmDraft(mm.data);
  }, [mm.data, mmDraft]);

  const putHost = useMutation({
    mutationFn: api.putHostConfig,
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ["hostConfig"] }),
  });
  const putNaming = useMutation({
    mutationFn: api.putNamingConfig,
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ["namingConfig"] }),
  });
  const putMm = useMutation({
    mutationFn: api.putMediaManagementConfig,
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ["mediaManagementConfig"] }),
  });

  return (
    <div>
      <h3>Media Management</h3>

      <section>
        <h4>Host</h4>
        {hostDraft && (
          <form
            onSubmit={(e) => {
              e.preventDefault();
              putHost.mutate(hostDraft);
            }}
          >
            <label>
              Instance name
              <input
                value={hostDraft.instanceName}
                onChange={(e) =>
                  setHostDraft({ ...hostDraft, instanceName: e.target.value })
                }
              />
            </label>
            <label>
              URL base
              <input
                value={hostDraft.urlBase ?? ""}
                onChange={(e) =>
                  setHostDraft({
                    ...hostDraft,
                    urlBase: e.target.value || undefined,
                  })
                }
              />
            </label>
            <label>
              Log level
              <select
                value={hostDraft.logLevel}
                onChange={(e) =>
                  setHostDraft({ ...hostDraft, logLevel: e.target.value })
                }
              >
                <option>Trace</option>
                <option>Debug</option>
                <option>Information</option>
                <option>Warning</option>
                <option>Error</option>
                <option>Fatal</option>
              </select>
            </label>
            <button type="submit" disabled={putHost.isPending}>
              Save host
            </button>
          </form>
        )}
      </section>

      <section>
        <h4>Naming</h4>
        {namingDraft && (
          <form
            onSubmit={(e) => {
              e.preventDefault();
              putNaming.mutate(namingDraft);
            }}
          >
            <label>
              Artist folder template
              <input
                value={namingDraft.artistFolderTemplate}
                onChange={(e) =>
                  setNamingDraft({
                    ...namingDraft,
                    artistFolderTemplate: e.target.value,
                  })
                }
              />
            </label>
            <label>
              File template
              <input
                value={namingDraft.fileTemplate}
                onChange={(e) =>
                  setNamingDraft({
                    ...namingDraft,
                    fileTemplate: e.target.value,
                  })
                }
              />
            </label>
            <button type="submit" disabled={putNaming.isPending}>
              Save naming
            </button>
          </form>
        )}
      </section>

      <section>
        <h4>File operation</h4>
        {mmDraft && (
          <form
            onSubmit={(e) => {
              e.preventDefault();
              putMm.mutate(mmDraft);
            }}
          >
            <label>
              File op
              <select
                value={mmDraft.fileOperation}
                onChange={(e) =>
                  setMmDraft({ ...mmDraft, fileOperation: e.target.value })
                }
              >
                <option>Move</option>
                <option>Copy</option>
                <option>HardlinkWithFallback</option>
              </select>
            </label>
            <label className="checkbox-row">
              <input
                type="checkbox"
                checked={mmDraft.replaceIllegalCharacters}
                onChange={(e) =>
                  setMmDraft({
                    ...mmDraft,
                    replaceIllegalCharacters: e.target.checked,
                  })
                }
              />
              Replace illegal characters
            </label>
            <label>
              Replacement char
              <input
                maxLength={1}
                value={mmDraft.illegalCharacterReplacement}
                onChange={(e) =>
                  setMmDraft({
                    ...mmDraft,
                    illegalCharacterReplacement: e.target.value.slice(0, 1) || "_",
                  })
                }
              />
            </label>
            <button type="submit" disabled={putMm.isPending}>
              Save media management
            </button>
          </form>
        )}
      </section>
    </div>
  );
}
