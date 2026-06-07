/* Adapted from Sonarr SeriesIndexPoster (GPL-3.0). */
import { Link } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { icons } from "../Components/Icon/Icon";
import type { ArtistDto } from "../api";
import { pickPoster } from "./ArtistImageHelpers";
import styles from "./ArtistCard.module.css";

type Props = { artist: ArtistDto };

export function ArtistCard({ artist }: Props): JSX.Element {
  const poster = pickPoster(artist);

  // TODO Phase G: wire hover actions to edit modal + refresh/search commands.
  // For now they're visual placeholders that don't navigate away from the link.
  const stop = (e: React.MouseEvent) => e.preventDefault();

  return (
    <div className={styles.content}>
      <div className={styles.posterContainer}>
        <span className={`${styles.status} ${artist.monitored ? styles.statusMonitored : styles.statusUnmonitored}`} />
        <Link to={`/library/${artist.id}`} className={styles.link}>
          {poster
            ? <img src={poster.url} alt={artist.name} loading="lazy" className={styles.poster} />
            : <div className={styles.posterFallback}><FontAwesomeIcon icon={icons.MUSIC} /></div>}
          <div className={styles.overlayTitle}>{artist.name}</div>
        </Link>
        <div className={styles.controls}>
          <button type="button" className={styles.action} title="Search" onClick={stop}>
            <FontAwesomeIcon icon={icons.SEARCH} />
          </button>
          <button type="button" className={styles.action} title="Edit" onClick={stop}>
            <FontAwesomeIcon icon={icons.EDIT} />
          </button>
          <button type="button" className={styles.action} title="Refresh" onClick={stop}>
            <FontAwesomeIcon icon={icons.REFRESH} />
          </button>
          <button type="button" className={styles.action} title="Delete" onClick={stop}>
            <FontAwesomeIcon icon={icons.DELETE} />
          </button>
        </div>
        {/* Progress bar reflects monitored-downloaded ratio when those stats are present;
            until the Library API surfaces per-artist counts, hide the bar entirely. */}
      </div>
      <div className={styles.title}>{artist.name}</div>
      {artist.disambiguation && <div className={styles.tagline}>{artist.disambiguation}</div>}
    </div>
  );
}
