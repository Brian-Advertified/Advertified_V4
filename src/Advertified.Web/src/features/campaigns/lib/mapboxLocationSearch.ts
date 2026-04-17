import type { LocationSuggestion } from '../../../types/domain';

type MapboxFeature = {
  properties?: {
    name?: string;
    name_preferred?: string;
    full_address?: string;
    feature_type?: string;
    place_formatted?: string;
    context?: {
      place?: { name?: string };
      locality?: { name?: string };
      district?: { name?: string };
      region?: { name?: string };
    };
  };
  geometry?: {
    coordinates?: [number, number];
  };
};

type MapboxResponse = {
  features?: MapboxFeature[];
};

export async function searchMapboxLocations(
  query: string,
  accessToken: string,
  signal?: AbortSignal,
): Promise<LocationSuggestion[]> {
  const trimmedQuery = query.trim();
  if (trimmedQuery.length < 2 || !accessToken.trim()) {
    return [];
  }

  const searchParams = new URLSearchParams({
    q: trimmedQuery,
    access_token: accessToken.trim(),
    country: 'ZA',
    language: 'en',
    limit: '6',
    types: 'place,locality,neighborhood,address',
    autocomplete: 'true',
  });

  const response = await fetch(`https://api.mapbox.com/search/geocode/v6/forward?${searchParams.toString()}`, {
    method: 'GET',
    signal,
  });

  if (!response.ok) {
    return [];
  }

  const payload = await response.json() as MapboxResponse;
  const features = Array.isArray(payload.features) ? payload.features : [];

  return features
    .map(mapFeatureToSuggestion)
    .filter((item): item is LocationSuggestion => item !== null);
}

function mapFeatureToSuggestion(feature: MapboxFeature): LocationSuggestion | null {
  const properties = feature.properties;
  const context = properties?.context;
  const coordinates = Array.isArray(feature.geometry?.coordinates) ? feature.geometry?.coordinates : undefined;
  const city = firstNonEmpty(
    context?.place?.name,
    context?.locality?.name,
    context?.district?.name,
  );
  const province = firstNonEmpty(context?.region?.name);
  const label = firstNonEmpty(
    properties?.name_preferred,
    properties?.name,
    properties?.full_address,
  );

  if (!label) {
    return null;
  }

  return {
    label,
    locationType: mapLocationType(properties?.feature_type, city, label),
    city,
    province,
    latitude: typeof coordinates?.[1] === 'number' ? coordinates[1] : undefined,
    longitude: typeof coordinates?.[0] === 'number' ? coordinates[0] : undefined,
    source: 'mapbox',
  };
}

function mapLocationType(featureType: string | undefined, city: string | undefined, label: string): string {
  const normalized = featureType?.trim().toLowerCase();
  if (normalized === 'place' || (city && city.localeCompare(label, undefined, { sensitivity: 'accent' }) === 0)) {
    return 'city';
  }

  if (normalized === 'address') {
    return 'address';
  }

  return 'suburb';
}

function firstNonEmpty(...values: Array<string | undefined>): string | undefined {
  for (const value of values) {
    if (!value) {
      continue;
    }

    const trimmed = value.trim();
    if (trimmed.length > 0) {
      return trimmed;
    }
  }

  return undefined;
}
