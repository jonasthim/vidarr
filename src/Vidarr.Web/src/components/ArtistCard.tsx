import { Link } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { icons } from "../Components/Icon/Icon";
import type { ArtistDto } from "../api";
import { StatusPill } from "./ui";
import { pickPoster } from "./ArtistImageHelpers";

type Props = { artist: ArtistDto };

export function ArtistCard({ artist }: Props): JSX.Element {
  const poster = pickPoster(artist);
  return (
    <Link to={`/library/${artist.id}`} className="artist-card">
      <div className="artist-card-poster">
        {poster ? (
          <img src={poster.url} alt={artist.name} loading="lazy" />
        ) : (
          <div className="artist-card-poster-fallback">
            <FontAwesomeIcon icon={icons.MUSIC} />
          </div>
        )}
      </div>
      <div className="artist-card-body">
        <div className="artist-card-name">{artist.name}</div>
        {artist.disambiguation && (
          <div className="artist-card-sub muted">{artist.disambiguation}</div>
        )}
        <StatusPill variant={artist.monitored ? "monitored" : "unmonitored"}>
          {artist.monitored ? "Monitored" : "Unmonitored"}
        </StatusPill>
      </div>
    </Link>
  );
}
