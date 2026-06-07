import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { icons } from "../Components/Icon/Icon";
import { api, type HostConfig, type MediaManagementConfig, type NamingConfig } from "../api";
import { FieldSet } from "../Components/Form/FieldSet";
import { FormGroup } from "../Components/Form/FormGroup";
import { FormLabel } from "../Components/Form/FormLabel";
import { FormInputGroup } from "../Components/Form/FormInputGroup";

export function GeneralSettingsPanel(): JSX.Element {
  const queryClient = useQueryClient();

  const host = useQuery({ queryKey: ["hostConfig"], queryFn: api.getHostConfig });
  const naming = useQuery({ queryKey: ["namingConfig"], queryFn: api.getNamingConfig });
  const mm = useQuery({ queryKey: ["mediaManagementConfig"], queryFn: api.getMediaManagementConfig });

  const [hostDraft, setHostDraft] = useState<HostConfig | null>(null);
  const [namingDraft, setNamingDraft] = useState<NamingConfig | null>(null);
  const [mmDraft, setMmDraft] = useState<MediaManagementConfig | null>(null);

  useEffect(() => { if (host.data && !hostDraft) setHostDraft(host.data); }, [host.data, hostDraft]);
  useEffect(() => { if (naming.data && !namingDraft) setNamingDraft(naming.data); }, [naming.data, namingDraft]);
  useEffect(() => { if (mm.data && !mmDraft) setMmDraft(mm.data); }, [mm.data, mmDraft]);

  const putHost = useMutation({
    mutationFn: api.putHostConfig,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["hostConfig"] }),
  });
  const putNaming = useMutation({
    mutationFn: api.putNamingConfig,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["namingConfig"] }),
  });
  const putMm = useMutation({
    mutationFn: api.putMediaManagementConfig,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["mediaManagementConfig"] }),
  });

  return (
    <div>
      {hostDraft && (
        <form onSubmit={(e) => { e.preventDefault(); putHost.mutate(hostDraft); }}>
          <FieldSet legend="Host">
            <FormGroup>
              <FormLabel htmlFor="instanceName">Instance Name</FormLabel>
              <FormInputGroup helpText="The name shown in the browser tab and notifications.">
                <input
                  id="instanceName"
                  type="text"
                  value={hostDraft.instanceName}
                  onChange={(e) => setHostDraft({ ...hostDraft, instanceName: e.target.value })}
                />
              </FormInputGroup>
            </FormGroup>
            <FormGroup>
              <FormLabel htmlFor="urlBase">URL Base</FormLabel>
              <FormInputGroup helpText="For reverse proxy support, default is empty.">
                <input
                  id="urlBase"
                  type="text"
                  value={hostDraft.urlBase ?? ""}
                  onChange={(e) =>
                    setHostDraft({ ...hostDraft, urlBase: e.target.value || undefined })
                  }
                />
              </FormInputGroup>
            </FormGroup>
            <FormGroup>
              <FormLabel htmlFor="logLevel">Log Level</FormLabel>
              <FormInputGroup helpText="Production deployments should stick with Information.">
                <select
                  id="logLevel"
                  value={hostDraft.logLevel}
                  onChange={(e) => setHostDraft({ ...hostDraft, logLevel: e.target.value })}
                >
                  <option>Trace</option>
                  <option>Debug</option>
                  <option>Information</option>
                  <option>Warning</option>
                  <option>Error</option>
                  <option>Fatal</option>
                </select>
              </FormInputGroup>
            </FormGroup>
            <button type="submit" className="primary" disabled={putHost.isPending}>
              <FontAwesomeIcon icon={icons.SAVE} />
              Save Host
            </button>
          </FieldSet>
        </form>
      )}

      {namingDraft && (
        <form onSubmit={(e) => { e.preventDefault(); putNaming.mutate(namingDraft); }}>
          <FieldSet legend="Naming">
            <FormGroup>
              <FormLabel htmlFor="artistFolder">Artist Folder Format</FormLabel>
              <FormInputGroup helpText="Token-driven template, e.g. {Artist Name}.">
                <input
                  id="artistFolder"
                  type="text"
                  value={namingDraft.artistFolderTemplate}
                  onChange={(e) =>
                    setNamingDraft({ ...namingDraft, artistFolderTemplate: e.target.value })
                  }
                />
              </FormInputGroup>
            </FormGroup>
            <FormGroup>
              <FormLabel htmlFor="fileFormat">File Format</FormLabel>
              <FormInputGroup helpText="Tokens: {Artist Name}, {Title}, {Year}, {Quality Full}.">
                <input
                  id="fileFormat"
                  type="text"
                  value={namingDraft.fileTemplate}
                  onChange={(e) =>
                    setNamingDraft({ ...namingDraft, fileTemplate: e.target.value })
                  }
                />
              </FormInputGroup>
            </FormGroup>
            <button type="submit" className="primary" disabled={putNaming.isPending}>
              <FontAwesomeIcon icon={icons.SAVE} />
              Save Naming
            </button>
          </FieldSet>
        </form>
      )}

      {mmDraft && (
        <form onSubmit={(e) => { e.preventDefault(); putMm.mutate(mmDraft); }}>
          <FieldSet legend="File Management">
            <FormGroup>
              <FormLabel htmlFor="fileOp">File Operation</FormLabel>
              <FormInputGroup helpText="Move/Copy semantics on import. HardlinkWithFallback prefers hardlinks where possible.">
                <select
                  id="fileOp"
                  value={mmDraft.fileOperation}
                  onChange={(e) => setMmDraft({ ...mmDraft, fileOperation: e.target.value })}
                >
                  <option>Move</option>
                  <option>Copy</option>
                  <option>HardlinkWithFallback</option>
                </select>
              </FormInputGroup>
            </FormGroup>
            <FormGroup>
              <FormLabel htmlFor="replaceIllegal">Replace Illegal Characters</FormLabel>
              <FormInputGroup helpText="When on, characters disallowed by the filesystem are swapped for the replacement char below.">
                <label style={{ display: "inline-flex", alignItems: "center", gap: 8, paddingTop: 8 }}>
                  <input
                    id="replaceIllegal"
                    type="checkbox"
                    checked={mmDraft.replaceIllegalCharacters}
                    onChange={(e) =>
                      setMmDraft({ ...mmDraft, replaceIllegalCharacters: e.target.checked })
                    }
                  />
                  Enable
                </label>
              </FormInputGroup>
            </FormGroup>
            <FormGroup>
              <FormLabel htmlFor="replaceChar">Replacement Character</FormLabel>
              <FormInputGroup helpText="Single character used when Replace Illegal Characters is on.">
                <input
                  id="replaceChar"
                  type="text"
                  maxLength={1}
                  style={{ maxWidth: 60 }}
                  value={mmDraft.illegalCharacterReplacement}
                  onChange={(e) =>
                    setMmDraft({
                      ...mmDraft,
                      illegalCharacterReplacement: e.target.value.slice(0, 1) || "_",
                    })
                  }
                />
              </FormInputGroup>
            </FormGroup>
            <button type="submit" className="primary" disabled={putMm.isPending}>
              <FontAwesomeIcon icon={icons.SAVE} />
              Save Media Management
            </button>
          </FieldSet>
        </form>
      )}
    </div>
  );
}
