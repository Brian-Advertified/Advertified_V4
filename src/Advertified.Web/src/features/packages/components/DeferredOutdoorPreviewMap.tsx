import 'mapbox-gl/dist/mapbox-gl.css';
import { MapPinned } from 'lucide-react';
import { useEffect, useMemo, useRef, useState } from 'react';
import type { Map as MapboxMap, Marker as MapboxMarker, Popup as MapboxPopup } from 'mapbox-gl';
import type { PackagePreviewMapPoint } from '../../../types/domain';

const DEFAULT_ZOOM = 10;
const SINGLE_POINT_ZOOM = 13;

export function DeferredOutdoorPreviewMap({ points }: { points: PackagePreviewMapPoint[] }) {
  const mapboxToken = (import.meta.env.VITE_MAPBOX_ACCESS_TOKEN as string | undefined)?.trim() ?? '';
  const mapContainerRef = useRef<HTMLDivElement | null>(null);
  const mapRef = useRef<MapboxMap | null>(null);
  const markersRef = useRef<MapboxMarker[]>([]);
  const popupRef = useRef<MapboxPopup | null>(null);
  const [mapbox, setMapbox] = useState<typeof import('mapbox-gl')['default'] | null>(null);
  const selectedAreaCount = useMemo(
    () => points.filter((point) => point.isInSelectedArea).length,
    [points],
  );
  const focusPoints = useMemo(() => {
    const selectedPoints = points.filter((point) => point.isInSelectedArea);
    return selectedPoints.length > 0 ? selectedPoints : points;
  }, [points]);
  const sampleLabels = useMemo(
    () => focusPoints.slice(0, 4).map((point) => point.siteName),
    [focusPoints],
  );

  useEffect(() => {
    if (!mapboxToken || points.length === 0) {
      return;
    }

    let isMounted = true;
    void import('mapbox-gl').then((module) => {
      if (isMounted) {
        setMapbox(module.default);
      }
    });

    return () => {
      isMounted = false;
    };
  }, [mapboxToken, points.length]);

  useEffect(() => {
    if (!mapbox || !mapboxToken || !mapContainerRef.current || points.length === 0) {
      return;
    }

    mapbox.accessToken = mapboxToken;
    if (!mapRef.current) {
      mapRef.current = new mapbox.Map({
        container: mapContainerRef.current,
        style: 'mapbox://styles/mapbox/light-v11',
        center: [focusPoints[0]?.longitude ?? 28.0473, focusPoints[0]?.latitude ?? -26.2041],
        zoom: focusPoints.length === 1 ? SINGLE_POINT_ZOOM : DEFAULT_ZOOM,
        attributionControl: false,
        dragRotate: false,
        touchZoomRotate: false,
        scrollZoom: false,
        cooperativeGestures: true,
      });
      mapRef.current.addControl(new mapbox.NavigationControl({ showCompass: false }), 'top-right');
    }

    markersRef.current.forEach((marker) => marker.remove());
    markersRef.current = [];
    popupRef.current?.remove();
    popupRef.current = null;

    points.forEach((point) => {
      const element = document.createElement('button');
      element.type = 'button';
      element.className = [
        'flex size-4 items-center justify-center rounded-full border-2 shadow-[0_0_0_4px_rgba(20,184,166,0.18)] transition-transform duration-200 hover:scale-110',
        point.isInSelectedArea
          ? 'border-[#0f766e] bg-[#14b8a6]'
          : 'border-slate-400 bg-white',
      ].join(' ');
      element.setAttribute('aria-label', `View ${point.siteName}`);
      element.title = `${point.siteName} - ${point.label}`;
      element.addEventListener('click', () => {
        popupRef.current?.remove();
        popupRef.current = new mapbox.Popup({
          anchor: 'top',
          closeButton: true,
          closeOnClick: false,
          offset: 18,
        })
          .setLngLat([point.longitude, point.latitude])
          .setDOMContent(createPopupContent(point))
          .addTo(mapRef.current!);
      });

      const marker = new mapbox.Marker({ element, anchor: 'bottom' })
        .setLngLat([point.longitude, point.latitude])
        .addTo(mapRef.current!);
      markersRef.current.push(marker);
    });

    if (focusPoints.length === 1) {
      mapRef.current.jumpTo({
        center: [focusPoints[0].longitude, focusPoints[0].latitude],
        zoom: SINGLE_POINT_ZOOM,
      });
      return;
    }

    const bounds = new mapbox.LngLatBounds();
    focusPoints.forEach((point) => bounds.extend([point.longitude, point.latitude]));
    mapRef.current.fitBounds(bounds, {
      padding: 36,
      maxZoom: SINGLE_POINT_ZOOM,
      duration: 350,
    });
  }, [focusPoints, mapbox, mapboxToken, points]);

  useEffect(() => () => {
    markersRef.current.forEach((marker) => marker.remove());
    popupRef.current?.remove();
    mapRef.current?.remove();
    mapRef.current = null;
  }, []);

  if (points.length === 0) {
    return (
      <section className="overflow-hidden rounded-[22px] border border-line bg-white">
        <div className="border-b border-line px-4 py-3">
          <p className="text-sm font-semibold text-ink">Billboards and Digital Screens near this campaign area</p>
          <p className="mt-1 text-xs leading-6 text-ink-soft">No mapped inventory points are available for the selected area yet.</p>
        </div>
      </section>
    );
  }

  if (!mapboxToken) {
    return (
      <section className="overflow-hidden rounded-[22px] border border-line bg-white">
        <div className="border-b border-line px-4 py-3">
          <p className="text-sm font-semibold text-ink">Billboards and Digital Screens near this campaign area</p>
          <p className="mt-1 text-xs leading-6 text-ink-soft">Add a Mapbox token to enable the interactive package map.</p>
        </div>
      </section>
    );
  }

  return (
    <section className="overflow-hidden rounded-[22px] border border-line bg-white">
      <div className="border-b border-line px-4 py-3">
        <p className="text-sm font-semibold text-ink">Billboards and Digital Screens near this campaign area</p>
        <p className="mt-1 text-xs leading-6 text-ink-soft">
          Outdoor pins reflect current preview inventory with supplier GPS coordinates.
        </p>
      </div>
      <div className="grid gap-5 bg-white px-4 py-4 lg:grid-cols-[minmax(0,1.4fr)_minmax(240px,0.72fr)]">
        <div className="overflow-hidden rounded-[20px] border border-line bg-white">
          <div className="relative h-[300px]">
            <div ref={mapContainerRef} className="size-full" />
            {!mapbox ? (
              <div className="absolute inset-0 flex flex-col items-center justify-center bg-white px-6 text-center">
                <span className="flex size-10 items-center justify-center rounded-full bg-brand-soft text-brand">
                  <MapPinned className="size-5" />
                </span>
                <p className="mt-3 text-sm font-semibold text-ink">Loading interactive map...</p>
                <p className="mt-2 max-w-sm text-sm leading-6 text-ink-soft">Preparing outdoor inventory for this package area.</p>
              </div>
            ) : null}
          </div>
        </div>

        <div className="space-y-4">
          <div className="rounded-[22px] border border-line bg-white/80 px-4 py-4">
            <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-brand">How to read it</p>
            <div className="mt-3 space-y-3 text-sm leading-6 text-ink-soft">
              <div className="flex items-center gap-3">
                <span className="flex size-4 shrink-0 rounded-full border-2 border-[#0f766e] bg-[#14b8a6]" />
                <span>Recommended for the selected campaign area</span>
              </div>
              <div className="flex items-center gap-3">
                <span className="flex size-4 shrink-0 rounded-full border-2 border-slate-400 bg-white" />
                <span>Additional mapped inventory nearby</span>
              </div>
            </div>
          </div>
          <div className="rounded-[22px] border border-line bg-white/80 px-4 py-4">
            <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-brand">Mapped sites</p>
            <div className="mt-3 flex flex-wrap gap-2">
              <span className="rounded-full border border-line bg-white px-3 py-2 text-xs font-semibold text-ink-soft">
                {points.length} total
              </span>
              <span className="rounded-full border border-line bg-white px-3 py-2 text-xs font-semibold text-ink-soft">
                {selectedAreaCount} in selected area
              </span>
            </div>
          </div>
          {sampleLabels.length > 0 ? (
            <div className="rounded-[22px] border border-line bg-white/80 px-4 py-4">
              <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-brand">Sample locations</p>
              <div className="mt-3 space-y-2">
                {sampleLabels.map((label) => (
                  <p key={label} className="text-sm text-ink-soft">{label}</p>
                ))}
              </div>
            </div>
          ) : null}
        </div>
      </div>
    </section>
  );
}

function createPopupContent(point: PackagePreviewMapPoint) {
  const wrapper = document.createElement('div');
  wrapper.className = 'space-y-1 pr-4';

  const title = document.createElement('p');
  title.className = 'text-sm font-semibold text-ink';
  title.textContent = point.siteName;
  wrapper.appendChild(title);

  const label = document.createElement('p');
  label.className = 'text-xs text-ink-soft';
  label.textContent = point.label;
  wrapper.appendChild(label);

  const location = document.createElement('p');
  location.className = 'text-xs text-ink-soft';
  location.textContent = `${point.city}, ${point.province}`;
  wrapper.appendChild(location);

  return wrapper;
}
