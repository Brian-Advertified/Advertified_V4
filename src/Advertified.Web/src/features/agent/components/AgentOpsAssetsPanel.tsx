import { Download } from 'lucide-react';
import { useState } from 'react';
import type { CampaignAsset } from '../../../types/domain';

export function AgentOpsAssetsPanel({
  executionAssets,
  isUploading,
  onUpload,
}: {
  executionAssets: CampaignAsset[];
  isUploading: boolean;
  onUpload: (input: { file: File; type: string }) => void;
}) {
  const [opsAssetFile, setOpsAssetFile] = useState<File | null>(null);
  const [opsAssetType, setOpsAssetType] = useState('proof_of_booking');

  return (
    <div className="panel border-line/80 bg-white px-6 py-6 shadow-[0_10px_26px_rgba(17,24,39,0.05)]">
      <h2 className="text-xl font-semibold text-ink">Files from suppliers and ops</h2>
      <p className="mt-2 text-sm leading-7 text-ink-soft">
        Upload proofs, delivery evidence, and any supporting files so the team has one place to find them later.
      </p>
      <div className="mt-4 space-y-3">
        {executionAssets.length > 0 ? executionAssets.map((asset) => (
          <div key={asset.id} className="rounded-[16px] border border-line bg-slate-50 px-4 py-3">
            <p className="text-sm font-semibold text-ink">{asset.displayName}</p>
            <p className="mt-1 text-xs text-ink-soft">{asset.assetType.replace(/_/g, ' ')}</p>
            {asset.publicUrl ? (
              <a href={asset.publicUrl} target="_blank" rel="noreferrer" className="button-secondary mt-3 inline-flex px-3 py-2 text-xs">
                <Download className="size-3.5" />
                Open file
              </a>
            ) : null}
          </div>
        )) : (
          <div className="rounded-[16px] border border-line bg-slate-50 px-4 py-3 text-sm text-ink-soft">
            No supplier proofs or support files uploaded yet.
          </div>
        )}
      </div>
      <div className="mt-5 space-y-3">
        <select value={opsAssetType} onChange={(event) => setOpsAssetType(event.target.value)} className="input-base">
          <option value="proof_of_booking">Booking confirmation</option>
          <option value="delivery_proof">Delivery proof</option>
          <option value="supporting_asset">Other supporting file</option>
        </select>
        <input type="file" onChange={(event) => setOpsAssetFile(event.target.files?.[0] ?? null)} className="input-base" />
        <button
          type="button"
          disabled={!opsAssetFile || isUploading}
          onClick={() => {
            if (!opsAssetFile) return;
            onUpload({ file: opsAssetFile, type: opsAssetType });
            setOpsAssetFile(null);
          }}
          className="button-secondary inline-flex w-full items-center justify-center gap-2 px-4 py-3 disabled:opacity-60"
        >
          Save file
        </button>
      </div>
    </div>
  );
}
