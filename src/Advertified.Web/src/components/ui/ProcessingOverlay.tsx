import { LoaderCircle } from 'lucide-react';

type ProcessingOverlayProps = {
  label?: string;
};

export function ProcessingOverlay({ label = 'Processing your request...' }: ProcessingOverlayProps) {
  return (
    <div className="processing-overlay" role="status" aria-live="polite" aria-busy="true">
      <div className="processing-overlay-card">
        <LoaderCircle className="processing-overlay-spinner" />
        <p className="processing-overlay-label">{label}</p>
      </div>
    </div>
  );
}
