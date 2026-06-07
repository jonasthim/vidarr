import { Link } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { icons } from "../Components/Icon/Icon";
import type { ArtistDto } from "../api";
import { StatusPill } from "./ui";
import { pickBanner } from "./ArtistImageHelpers";

type Props = { artist: ArtistDto };

export function ArtistBanner({ artist }: Props): JSX.Element {
  const banner = pickBanner(artist);
  return (
    <Link to={`/library/${artist.id}`} className="artist-banner">
      <div className="artist-banner-image">
        {banner ? (
          <img src={banner.url} alt={artist.name} loading="lazy" />
        ) : (
          <div className="artist-banner-fallback">
            <FontAwesomeIcon icon={icons.MUSIC} />
          </div>
        )}
      </div>
      <div className="artist-banner-body">
        <div className="artist-banner-name">{artist.name}</div>
        <div className="artist-banner-meta">
          {artist.country && <span>{artist.country}</span>}
          {artist.genres.length > 0 && <span>{artist.genres.slice(0, 3).join(", ")}</span>}
        </div>
        <StatusPill variant={artist.monitored ? "monitored" : "unmonitored"}>
          {artist.monitored ? "Monitored" : "Unmonitored"}
        </StatusPill>
      </div>
    </Link>
  );
}
