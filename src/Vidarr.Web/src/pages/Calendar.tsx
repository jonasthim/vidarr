import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { api, type MusicVideoListItem } from "../api";
import { PageHeader, Card, StatusPill } from "../components/ui";

const WEEK_LABELS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

function startOfMonth(d: Date): Date {
  return new Date(d.getFullYear(), d.getMonth(), 1);
}

function endOfMonth(d: Date): Date {
  return new Date(d.getFullYear(), d.getMonth() + 1, 0, 23, 59, 59);
}

function addMonths(d: Date, delta: number): Date {
  return new Date(d.getFullYear(), d.getMonth() + delta, 1);
}

function formatMonth(d: Date): string {
  return d.toLocaleDateString(undefined, { month: "long", year: "numeric" });
}

function dayKey(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

function premiereDate(v: MusicVideoListItem): Date | null {
  if (v.releaseDate) return new Date(v.releaseDate);
  if (v.year) return new Date(v.year, 0, 1);
  return null;
}

export function CalendarPage(): JSX.Element {
  const [cursor, setCursor] = useState<Date>(() => startOfMonth(new Date()));

  const from = startOfMonth(cursor);
  const to = endOfMonth(cursor);
  const fromIso = from.toISOString();
  const toIso = to.toISOString();

  const eventsQuery = useQuery({
    queryKey: ["calendar", fromIso, toIso],
    queryFn: () => api.listCalendar(fromIso, toIso),
    refetchInterval: false,
  });

  // Group videos by day-of-month string.
  const byDay = useMemo<Map<string, MusicVideoListItem[]>>(() => {
    const map = new Map<string, MusicVideoListItem[]>();
    for (const v of eventsQuery.data ?? []) {
      const d = premiereDate(v);
      if (!d) continue;
      // Only include events within the displayed month
      if (d.getFullYear() !== cursor.getFullYear() || d.getMonth() !== cursor.getMonth()) continue;
      const key = dayKey(d);
      const arr = map.get(key) ?? [];
      arr.push(v);
      map.set(key, arr);
    }
    return map;
  }, [eventsQuery.data, cursor]);

  // Build the 6-week grid; week starts on Monday.
  const grid = useMemo<(Date | null)[]>(() => {
    const cells: (Date | null)[] = [];
    const first = startOfMonth(cursor);
    const last = endOfMonth(cursor);
    // JS getDay(): Sun=0, Mon=1, ..., Sat=6. We want Mon=0 ... Sun=6.
    const startOffset = (first.getDay() + 6) % 7;
    for (let i = 0; i < startOffset; i += 1) cells.push(null);
    for (let day = 1; day <= last.getDate(); day += 1) {
      cells.push(new Date(cursor.getFullYear(), cursor.getMonth(), day));
    }
    while (cells.length % 7 !== 0) cells.push(null);
    return cells;
  }, [cursor]);

  return (
    <>
      <PageHeader
        title="Calendar"
        actions={
          <>
            <button type="button" onClick={() => setCursor((c) => addMonths(c, -1))}>
              <ChevronLeft size={14} />
              Prev
            </button>
            <button type="button" onClick={() => setCursor(startOfMonth(new Date()))}>
              Today
            </button>
            <button type="button" onClick={() => setCursor((c) => addMonths(c, 1))}>
              Next
              <ChevronRight size={14} />
            </button>
          </>
        }
      />

      <Card title={formatMonth(cursor)}>
        {eventsQuery.isLoading && <div className="loading-state">Loading…</div>}
        {eventsQuery.error && (
          <div className="error-banner">
            Failed: {(eventsQuery.error as Error).message}
          </div>
        )}
        <div className="calendar">
          <div className="calendar-head">
            {WEEK_LABELS.map((w) => (
              <div key={w}>{w}</div>
            ))}
          </div>
          <div className="calendar-grid">
            {grid.map((d, i) => {
              if (!d) return <div key={i} className="calendar-cell calendar-cell-empty" />;
              const key = dayKey(d);
              const events = byDay.get(key) ?? [];
              const isToday = sameDay(d, new Date());
              return (
                <div key={key} className={`calendar-cell${isToday ? " today" : ""}`}>
                  <div className="calendar-day">{d.getDate()}</div>
                  {events.map((e) => (
                    <Link
                      key={e.id}
                      to={`/library/${e.artistId}`}
                      className={`calendar-event${e.hasFile ? " downloaded" : e.monitored ? " wanted" : " ignored"}`}
                      title={`${e.artistName} — ${e.title}`}
                    >
                      <span className="calendar-event-artist">{e.artistName}</span>
                      <span className="calendar-event-title">{e.title}</span>
                    </Link>
                  ))}
                </div>
              );
            })}
          </div>
        </div>
      </Card>

      <Card title="Legend">
        <div style={{ display: "flex", gap: "var(--space-3)", flexWrap: "wrap" }}>
          <StatusPill variant="success">Downloaded</StatusPill>
          <StatusPill variant="warning">Wanted</StatusPill>
          <StatusPill variant="unmonitored">Unmonitored</StatusPill>
        </div>
        <p className="muted" style={{ marginTop: "var(--space-3)" }}>
          Year-only entries appear on January 1st. Real <code>ReleaseDate</code> values place
          videos on the exact day.
        </p>
      </Card>
    </>
  );
}

function sameDay(a: Date, b: Date): boolean {
  return (
    a.getFullYear() === b.getFullYear()
    && a.getMonth() === b.getMonth()
    && a.getDate() === b.getDate()
  );
}
