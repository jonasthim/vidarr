import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { icons } from "../Components/Icon/Icon";
import { api, type MusicVideoListItem } from "../api";
import { PageContent } from "../Components/Page/PageContent";
import { PageContentBody } from "../Components/Page/PageContentBody";
import { PageToolbar } from "../Components/Page/Toolbar/PageToolbar";
import { PageToolbarSection } from "../Components/Page/Toolbar/PageToolbarSection";
import { PageToolbarButton } from "../Components/Page/Toolbar/PageToolbarButton";
import { PageToolbarSeparator } from "../Components/Page/Toolbar/PageToolbarSeparator";

const WEEK_LABELS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

function startOfMonth(d: Date): Date { return new Date(d.getFullYear(), d.getMonth(), 1); }
function endOfMonth(d: Date): Date { return new Date(d.getFullYear(), d.getMonth() + 1, 0, 23, 59, 59); }
function addMonths(d: Date, delta: number): Date { return new Date(d.getFullYear(), d.getMonth() + delta, 1); }
function formatMonth(d: Date): string { return d.toLocaleDateString(undefined, { month: "long", year: "numeric" }); }
function dayKey(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}
function premiereDate(v: MusicVideoListItem): Date | null {
  if (v.releaseDate) return new Date(v.releaseDate);
  if (v.year) return new Date(v.year, 0, 1);
  return null;
}
function sameDay(a: Date, b: Date): boolean {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
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

  const byDay = useMemo<Map<string, MusicVideoListItem[]>>(() => {
    const map = new Map<string, MusicVideoListItem[]>();
    for (const v of eventsQuery.data ?? []) {
      const d = premiereDate(v);
      if (!d) continue;
      if (d.getFullYear() !== cursor.getFullYear() || d.getMonth() !== cursor.getMonth()) continue;
      const key = dayKey(d);
      const arr = map.get(key) ?? [];
      arr.push(v);
      map.set(key, arr);
    }
    return map;
  }, [eventsQuery.data, cursor]);

  const grid = useMemo<(Date | null)[]>(() => {
    const cells: (Date | null)[] = [];
    const first = startOfMonth(cursor);
    const last = endOfMonth(cursor);
    const startOffset = (first.getDay() + 6) % 7;
    for (let i = 0; i < startOffset; i += 1) cells.push(null);
    for (let day = 1; day <= last.getDate(); day += 1) {
      cells.push(new Date(cursor.getFullYear(), cursor.getMonth(), day));
    }
    while (cells.length % 7 !== 0) cells.push(null);
    return cells;
  }, [cursor]);

  return (
    <PageContent title="Calendar">
      <PageToolbar>
        <PageToolbarSection>
          <PageToolbarButton
            label="Previous"
            iconName={icons.CHEVRON_LEFT}
            onPress={() => setCursor((c) => addMonths(c, -1))}
          />
          <PageToolbarButton
            label="Today"
            iconName={icons.CALENDAR}
            onPress={() => setCursor(startOfMonth(new Date()))}
          />
          <PageToolbarButton
            label="Next"
            iconName={icons.CHEVRON_RIGHT}
            onPress={() => setCursor((c) => addMonths(c, 1))}
          />
          <PageToolbarSeparator />
          <PageToolbarButton
            label="Refresh"
            iconName={icons.REFRESH}
            isSpinning={eventsQuery.isFetching}
            onPress={() => eventsQuery.refetch()}
          />
        </PageToolbarSection>
      </PageToolbar>
      <PageContentBody>
        <h2 style={{ marginBottom: 16 }}>{formatMonth(cursor)}</h2>
        {eventsQuery.isLoading && <div className="loading-state">Loading…</div>}
        {eventsQuery.error && (
          <div className="error-banner">Failed: {(eventsQuery.error as Error).message}</div>
        )}
        <div className="calendar">
          <div className="calendar-head">
            {WEEK_LABELS.map((w) => <div key={w}>{w}</div>)}
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
      </PageContentBody>
    </PageContent>
  );
}
