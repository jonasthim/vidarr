import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Archive, Trash2 } from "lucide-react";
import { api } from "../api";
import { Card, EmptyState, StatusPill } from "./ui";

export function BackupPanel(): JSX.Element {
  const queryClient = useQueryClient();
  const backupsQuery = useQuery({
    queryKey: ["backups"],
    queryFn: api.listBackups,
    refetchInterval: false,
  });
  const create = useMutation({
    mutationFn: api.createBackup,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["backups"] }),
  });
  const del = useMutation({
    mutationFn: api.deleteBackup,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["backups"] }),
  });

  return (
    <Card
      title="Backups"
      actions={
        <button type="button" className="primary" onClick={() => create.mutate()} disabled={create.isPending}>
          <Archive size={14} />
          {create.isPending ? "Creating…" : "Backup now"}
        </button>
      }
    >
      {backupsQuery.isLoading && <div className="loading-state">Loading…</div>}
      {backupsQuery.error && (
        <div className="error-banner">Failed to load backups: {(backupsQuery.error as Error).message}</div>
      )}
      {backupsQuery.data && backupsQuery.data.length === 0 && (
        <EmptyState
          icon={<Archive />}
          title="No backups yet"
          description="Click Backup now to create the first one. Vidarr also creates one weekly via the scheduler."
        />
      )}
      {backupsQuery.data && backupsQuery.data.length > 0 && (
        <table className="grid">
          <thead>
            <tr>
              <th>File</th>
              <th>Size</th>
              <th>Created</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {backupsQuery.data.map((b) => (
              <tr key={b.fileName}>
                <td><code>{b.fileName}</code></td>
                <td>{formatBytes(b.sizeBytes)}</td>
                <td>{new Date(b.createdAt).toLocaleString()}</td>
                <td className="actions">
                  <button
                    type="button"
                    className="danger icon-btn"
                    title="Delete"
                    onClick={() => {
                      if (window.confirm(`Delete ${b.fileName}?`)) del.mutate(b.fileName);
                    }}
                    disabled={del.isPending}
                  >
                    <Trash2 size={14} />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <p className="muted" style={{ marginTop: "var(--space-3)" }}>
        Restore is supported via <code>POST /api/v1/system/backup/restore</code> with a zip body.
        File upload from the UI is on the roadmap. <StatusPill variant="info">P2</StatusPill>
      </p>
    </Card>
  );
}

function formatBytes(n: number): string {
  if (n < 1024) return `${n} B`;
  if (n < 1024 * 1024) return `${(n / 1024).toFixed(1)} KiB`;
  if (n < 1024 * 1024 * 1024) return `${(n / 1024 / 1024).toFixed(1)} MiB`;
  return `${(n / 1024 / 1024 / 1024).toFixed(2)} GiB`;
}
