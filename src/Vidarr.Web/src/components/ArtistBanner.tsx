/* Adapted from Sonarr SeriesIndexBanner (GPL-3.0). */
import { Link } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { icons } from "../Components/Icon/Icon";
import type { ArtistDto } from "../api";
import { pickBanner } from "./ArtistImageHelpers";
import styles from "./ArtistBanner.module.css";

type Props = { artist: ArtistDto };

export function ArtistBanner({ artist }: Props): JSX.Element {
  const banner = pickBanner(artist);
  const stop = (e: React.MouseEvent) => e.preventDefault();
  return (
    <div className={styles.content}>
      <div className={styles.bannerContainer}>
        <span className={`${styles.status} ${artist.monitored ? styles.statusMonitored : styles.statusUnmonitored}`} />
        <Link to={`/library/${artist.id}`} className={styles.link}>
          {banner
            ? <img src={banner.url} alt={artist.name} loading="lazy" className={styles.banner} />
            : <div className={styles.bannerFallback}><FontAwesomeIcon icon={icons.MUSIC} /></div>}
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
      </div>
      <div className={styles.body}>
        <div className={styles.title}>{artist.name}</div>
        <div className={styles.meta}>
          {artist.country && <span>{artist.country}</span>}
          {artist.genres.length > 0 && <span>{artist.genres.slice(0, 3).join(", ")}</span>}
        </div>
      </div>
    </div>
  );
}
