import 'mapbox-gl/dist/mapbox-gl.css';
import { LngLatBounds } from 'mapbox-gl';
import { useMemo, useRef, useState } from 'react';
import Map, { Marker, NavigationControl, Popup, type MapRef } from 'react-map-gl/mapbox';
import type { PackagePreviewMapPoint } from '../../../types/domain';

const DEFAULT_ZOOM = 10;
const SINGLE_POINT_ZOOM = 13;

export function OutdoorPreviewMap({ points }: { points: PackagePreviewMapPoint[] }) {
  const mapboxToken = (import.meta.env.VITE_MAPBOX_ACCESS_TOKEN as string | undefined)?.trim();
  const mapRef = useRef<MapRef | null>(null);
  const [selectedPointKey, setSelectedPointKey] = useState<string | null | undefined>(undefined);
  const focusPoints = useMemo(() => {
    const focused = points.filter((point) => point.isInSelectedArea);
    return focused.length > 0 ? focused : points;
  }, [points]);
  const focusPointsKey = useMemo(
    () => focusPoints.map(getPointKey).join('|'),
    [focusPoints],
  );
  const selectedPoint = useMemo(() => {
    if (points.length === 0) {
      return null;
    }

    if (selectedPointKey === null) {
      return null;
    }

    if (selectedPointKey === undefined) {
      return points[0];
    }

    return points.find((point) => getPointKey(point) === selectedPointKey) ?? points[0];
  }, [points, selectedPointKey]);

  const initialViewState = useMemo(
    () => ({
      longitude: focusPoints[0]?.longitude ?? 28.0473,
      latitude: focusPoints[0]?.latitude ?? -26.2041,
      zoom: focusPoints.length === 1 ? SINGLE_POINT_ZOOM : DEFAULT_ZOOM,
    }),
    [focusPoints],
  );

  if (points.length === 0) {
    return null;
  }

  if (!mapboxToken) {
    return (
      <div className="overflow-hidden rounded-[22px] border border-line bg-white">
        <div className="border-b border-line px-4 py-3">
          <p className="text-sm font-semibold text-ink">Billboards and digital screens near this campaign area</p>
          <p className="mt-1 text-xs leading-6 text-ink-soft">Add a Mapbox token to enable the interactive package map.</p>
        </div>
      </div>
    );
  }

  return (
    <div className="overflow-hidden rounded-[22px] border border-line bg-white">
      <div className="border-b border-line px-4 py-3">
        <p className="text-sm font-semibold text-ink">Billboards and digital screens near this campaign area</p>
        <p className="mt-1 text-xs leading-6 text-ink-soft">Outdoor pins reflect current preview inventory with supplier GPS coordinates.</p>
      </div>
      <div className="h-[260px] w-full">
        <Map
          key={focusPointsKey}
          ref={(instance) => {
            mapRef.current = instance;
            if (!instance) {
              return;
            }

            const map = instance.getMap();
            if (focusPoints.length === 1) {
              map.jumpTo({
                center: [focusPoints[0].longitude, focusPoints[0].latitude],
                zoom: SINGLE_POINT_ZOOM,
              });
              return;
            }

            const bounds = new LngLatBounds();
            focusPoints.forEach((point) => bounds.extend([point.longitude, point.latitude]));
            map.fitBounds(bounds, {
              padding: 28,
              maxZoom: SINGLE_POINT_ZOOM,
              duration: 0,
            });
          }}
          initialViewState={initialViewState}
          mapboxAccessToken={mapboxToken}
          mapStyle="mapbox://styles/mapbox/light-v11"
          reuseMaps
          attributionControl={false}
          dragRotate={false}
          touchZoomRotate={false}
          scrollZoom={false}
          cooperativeGestures
          style={{ width: '100%', height: '100%' }}
        >
          <NavigationControl position="top-right" showCompass={false} />
          {points.map((point) => (
            <Marker
              key={getPointKey(point)}
              longitude={point.longitude}
              latitude={point.latitude}
              anchor="bottom"
            >
              <button
                type="button"
                className={[
                  'flex size-4 items-center justify-center rounded-full border-2 shadow-[0_0_0_4px_rgba(20,184,166,0.18)]',
                  point.isInSelectedArea
                    ? 'border-[#0f766e] bg-[#14b8a6]'
                    : 'border-slate-400 bg-white',
                ].join(' ')}
                onClick={() => setSelectedPointKey(getPointKey(point))}
                aria-label={`View ${point.siteName}`}
              />
            </Marker>
          ))}

          {selectedPoint ? (
            <Popup
              longitude={selectedPoint.longitude}
              latitude={selectedPoint.latitude}
              anchor="top"
              closeButton
              closeOnClick={false}
              offset={18}
              onClose={() => setSelectedPointKey(null)}
            >
              <div className="space-y-1 pr-4">
                <p className="text-sm font-semibold text-ink">{selectedPoint.siteName}</p>
                <p className="text-xs text-ink-soft">{selectedPoint.label}</p>
                <p className="text-xs text-ink-soft">
                  {selectedPoint.city}, {selectedPoint.province}
                </p>
              </div>
            </Popup>
          ) : null}
        </Map>
      </div>
    </div>
  );
}

function getPointKey(point: PackagePreviewMapPoint) {
  return `${point.siteName}-${point.latitude}-${point.longitude}`;
}
