import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, NOTIFICATION_EVENT_TYPES, type NotificationSchema } from "../api";

const blankSettings = (schema: NotificationSchema): Record<string, string> =>
  Object.fromEntries(schema.fields.map((f) => [f.name, ""]));

export function NotificationsPanel(): JSX.Element {
  const queryClient = useQueryClient();
  const list = useQuery({
    queryKey: ["notifications"],
    queryFn: api.listNotifications,
  });
  const schemas = useQuery({
    queryKey: ["notificationSchemas"],
    queryFn: api.listNotificationSchemas,
  });

  const [selectedImpl, setSelectedImpl] = useState<string>("");
  const [name, setName] = useState("");
  const [settings, setSettings] = useState<Record<string, string>>({});
  const [events, setEvents] = useState<number[]>([2, 3, 4]); // sensible default
  const [testResult, setTestResult] = useState<{ success: boolean; message?: string } | null>(null);

  const create = useMutation({
    mutationFn: () =>
      api.createNotification({
        name,
        implementation: selectedImpl,
        settingsJson: JSON.stringify(settings),
        enable: true,
        subscribedEvents: events,
        tags: [],
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["notifications"] });
      setName("");
      setSettings({});
      setSelectedImpl("");
      setEvents([2, 3, 4]);
      setTestResult(null);
    },
  });

  const remove = useMutation({
    mutationFn: api.deleteNotification,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["notifications"] }),
  });

  const test = useMutation({
    mutationFn: () => api.testNotification(selectedImpl, JSON.stringify(settings)),
    onSuccess: setTestResult,
  });

  const onPickImpl = (impl: string) => {
    setSelectedImpl(impl);
    const schema = schemas.data?.find((s) => s.implementation === impl);
    setSettings(schema ? blankSettings(schema) : {});
    setTestResult(null);
  };

  const toggleEvent = (value: number, on: boolean) =>
    setEvents((cur) => (on ? [...cur, value] : cur.filter((e) => e !== value)));

  const activeSchema = schemas.data?.find((s) => s.implementation === selectedImpl);

  return (
    <div>
      <h3>Notifications</h3>
      <ul className="profiles-list">
        {list.data?.map((n) => (
          <li key={n.id}>
            <strong>{n.name}</strong>
            <span className="muted">
              · {n.implementation} · {n.enable ? "enabled" : "disabled"} ·{" "}
              {n.subscribedEvents.length} events
            </span>
            <button type="button" onClick={() => remove.mutate(n.id)}>
              Delete
            </button>
          </li>
        ))}
      </ul>

      <h4>Add notification</h4>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          if (!selectedImpl || !name.trim()) return;
          create.mutate();
        }}
      >
        <label>
          Implementation
          <select value={selectedImpl} onChange={(e) => onPickImpl(e.target.value)}>
            <option value="">—</option>
            {schemas.data?.map((s) => (
              <option key={s.implementation} value={s.implementation}>
                {s.displayName}
              </option>
            ))}
          </select>
        </label>

        {activeSchema && (
          <>
            <label>
              Name
              <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Display name" />
            </label>

            {activeSchema.fields.map((f) => (
              <label key={f.name}>
                {f.label} {f.required && <span className="muted">*</span>}
                <input
                  type={f.type === "number" ? "number" : f.type === "password" ? "password" : "text"}
                  value={settings[f.name] ?? ""}
                  onChange={(e) => setSettings({ ...settings, [f.name]: e.target.value })}
                  placeholder={f.helpText ?? ""}
                />
              </label>
            ))}

            <fieldset>
              <legend>Subscribed events</legend>
              {NOTIFICATION_EVENT_TYPES.map((ev) => {
                const supported = activeSchema.supportedEvents.includes(ev.label);
                if (!supported) return null;
                return (
                  <label key={ev.value} className="checkbox-row">
                    <input
                      type="checkbox"
                      checked={events.includes(ev.value)}
                      onChange={(e) => toggleEvent(ev.value, e.target.checked)}
                    />
                    {ev.label}
                  </label>
                );
              })}
            </fieldset>

            <div style={{ display: "flex", gap: "0.5rem" }}>
              <button type="button" disabled={test.isPending} onClick={() => test.mutate()}>
                Test
              </button>
              <button type="submit" disabled={create.isPending || !name.trim()}>
                Add
              </button>
            </div>

            {testResult && (
              <div className={testResult.success ? "muted" : "error"}>
                {testResult.success ? "✓" : "✗"} {testResult.message ?? "(no message)"}
              </div>
            )}
          </>
        )}
      </form>
    </div>
  );
}
