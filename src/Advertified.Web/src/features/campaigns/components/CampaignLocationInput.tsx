import { useId, useMemo, useState, type ChangeEvent } from 'react';
import { useQuery } from '@tanstack/react-query';
import type { AddressAutofillRetrieveResponse } from '@mapbox/search-js-core';
import { advertifiedApi } from '../../../services/advertifiedApi';
import type { LocationSuggestion } from '../../../types/domain';
import { AddressAutofillInput } from '../../auth/components/AddressAutofillInput';

export type ResolvedCampaignLocation = {
  label: string;
  city?: string;
  province?: string;
  latitude?: number;
  longitude?: number;
  source: 'mapbox' | 'catalog';
};

export function CampaignLocationInput({
  value,
  geographyScope,
  cityFilter,
  placeholder,
  disabled,
  className,
  onChange,
  onResolved,
}: {
  value: string;
  geographyScope: string;
  cityFilter?: string;
  placeholder: string;
  disabled?: boolean;
  className?: string;
  onChange: (value: string) => void;
  onResolved: (location: ResolvedCampaignLocation | null) => void;
}) {
  const listboxId = useId();
  const [focused, setFocused] = useState(false);
  const mapboxAccessToken = (import.meta.env.VITE_MAPBOX_ACCESS_TOKEN as string | undefined)?.trim();
  const hasMapbox = Boolean(mapboxAccessToken);
  const normalizedQuery = value.trim();

  const suggestionsQuery = useQuery({
    queryKey: ['public-location-search', geographyScope, cityFilter ?? '', normalizedQuery],
    queryFn: () => advertifiedApi.searchLocations({
      query: normalizedQuery,
      geographyScope,
      city: cityFilter,
      limit: 6,
    }),
    enabled: normalizedQuery.length >= 2,
    staleTime: 5 * 60 * 1000,
    retry: false,
  });

  const suggestions = suggestionsQuery.data ?? [];
  const showSuggestions = focused && suggestions.length > 0 && normalizedQuery.length >= 2;

  const inputProps = useMemo(() => ({
    value,
    placeholder,
    disabled,
    className,
    onFocus: () => setFocused(true),
    onBlur: () => {
      window.setTimeout(() => {
        setFocused(false);
      }, 120);
    },
    onChange: (event: ChangeEvent<HTMLInputElement>) => {
      onChange(event.target.value);
      onResolved(null);
    },
  }), [className, disabled, onChange, onResolved, placeholder, value]);

  function handleSuggestionClick(suggestion: LocationSuggestion) {
    const label = suggestion.locationType === 'suburb' && suggestion.city
      ? `${suggestion.label}, ${suggestion.city}`
      : suggestion.label;

    onChange(label);
    onResolved({
      label,
      city: suggestion.city,
      province: suggestion.province,
      latitude: suggestion.latitude,
      longitude: suggestion.longitude,
      source: 'catalog',
    });
    setFocused(false);
  }

  function handleRetrieve(response: AddressAutofillRetrieveResponse) {
    const feature = response.features[0];
    const properties = feature?.properties;
    const coordinates = Array.isArray(feature?.geometry?.coordinates) ? feature.geometry.coordinates : undefined;
    const label = properties?.full_address
      ?? properties?.place_name
      ?? value;
    const city = properties?.address_level2 ?? properties?.address_level3 ?? undefined;
    const province = properties?.address_level1 ?? undefined;
    const longitude = typeof coordinates?.[0] === 'number' ? coordinates[0] : undefined;
    const latitude = typeof coordinates?.[1] === 'number' ? coordinates[1] : undefined;

    onChange(label);
    onResolved({
      label,
      city,
      province,
      latitude,
      longitude,
      source: 'mapbox',
    });
    setFocused(false);
  }

  return (
    <div className="relative">
      {hasMapbox && mapboxAccessToken ? (
        <AddressAutofillInput
          accessToken={mapboxAccessToken}
          onRetrieve={handleRetrieve}
          inputProps={inputProps}
        />
      ) : (
        <input {...inputProps} />
      )}

      {showSuggestions ? (
        <div
          id={listboxId}
          className="absolute z-20 mt-2 w-full overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-xl"
        >
          {suggestions.map((suggestion) => {
            const secondary = [suggestion.city, suggestion.province].filter(Boolean).join(', ');
            return (
              <button
                key={`${suggestion.locationType}:${suggestion.label}:${suggestion.city ?? ''}`}
                type="button"
                className="flex w-full items-start justify-between gap-3 border-b border-slate-100 px-4 py-3 text-left last:border-b-0 hover:bg-slate-50"
                onMouseDown={(event) => {
                  event.preventDefault();
                  handleSuggestionClick(suggestion);
                }}
              >
                <span>
                  <span className="block text-sm font-medium text-ink">{suggestion.label}</span>
                  {secondary ? <span className="mt-1 block text-xs text-ink-soft">{secondary}</span> : null}
                </span>
                <span className="text-[11px] font-semibold uppercase tracking-[0.16em] text-slate-400">
                  {suggestion.locationType}
                </span>
              </button>
            );
          })}
        </div>
      ) : null}
    </div>
  );
}
