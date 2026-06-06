import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";

export function BlocklistPanel(): JSX.Element {
  const queryClient = useQueryClient();
  const blocklist = useQuery({ queryKey: ["blocklist"], queryFn: api.listBlocklist });
  const remove = useMutation({
    mutationFn: api.deleteBlocklist,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["blocklist"] }),
  });

  return (
    <div>
      <h3>Blocklist</h3>
      <p className="muted">
        Release titles here are rejected by the Decision pipeline so they cannot be
        grabbed again. Items are added automatically when a download is removed with
        the Blocklist option, or manually here.
      </p>
      {blocklist.data?.length === 0 && <p>Nothing blocklisted.</p>}
      <table>
        <thead>
          <tr>
            <th>Release</th>
            <th>Indexer</th>
            <th>Reason</th>
            <th>Date</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {blocklist.data?.map((b) => (
            <tr key={b.id}>
              <td>{b.releaseTitle}</td>
              <td>{b.indexerName}</td>
              <td>{b.reason ?? "—"}</td>
              <td>{new Date(b.date).toLocaleString()}</td>
              <td>
                <button type="button" onClick={() => remove.mutate(b.id)}>
                  Remove
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
