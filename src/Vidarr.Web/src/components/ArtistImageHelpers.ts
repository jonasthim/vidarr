import type { ArtistDto, ArtistImage } from "../api";

const POSTER_KINDS = ["poster", "cover", "fanart", "front"];
const BANNER_KINDS = ["banner", "headshot", "background"];

export function pickPoster(a: ArtistDto): ArtistImage | undefined {
  return pickByKinds(a.images, POSTER_KINDS) ?? a.images[0];
}

export function pickBanner(a: ArtistDto): ArtistImage | undefined {
  return pickByKinds(a.images, BANNER_KINDS) ?? a.images[0];
}

function pickByKinds(images: ArtistImage[], kinds: string[]): ArtistImage | undefined {
  for (const k of kinds) {
    const m = images.find((i) => i.kind.toLowerCase() === k);
    if (m) return m;
  }
  return undefined;
}
