import advertifiedPoster from '../../assets/Channels/advertified-poster.svg';

let advertifiedVideoPromise: Promise<string> | null = null;

export function loadAdvertifiedVideo() {
  if (!advertifiedVideoPromise) {
    advertifiedVideoPromise = import('../../assets/Channels/advertified.mp4').then((module) => module.default);
  }

  return advertifiedVideoPromise;
}

export const advertifiedVideoPoster = advertifiedPoster;
