import type { ChangeEvent } from 'react';
import type { Campaign } from '../../../types/domain';

interface CreativeStudioAssetsPanelProps {
  campaign: Campaign;
  isPreview: boolean;
  assetType: string;
  assetFileName?: string;
  onAssetTypeChange: (value: string) => void;
  onAssetFileChange: (event: ChangeEvent<HTMLInputElement>) => void;
  onUploadAsset?: () => void;
  isUploadingAsset: boolean;
}

export function CreativeStudioAssetsPanel({
  campaign,
  isPreview,
  assetType,
  assetFileName,
  onAssetTypeChange,
  onAssetFileChange,
  onUploadAsset,
  isUploadingAsset,
}: CreativeStudioAssetsPanelProps) {
  return (
    <div className="grid gap-6 xl:grid-cols-[1fr_1fr]">
      <div className="user-card">
        <h3>Studio files</h3>
        <div className="mt-4 space-y-3">
          {campaign.assets.length > 0 ? campaign.assets.map((asset) => (
            <div key={asset.id} className="user-wire">
              <strong>{asset.displayName}</strong>
              <div>{asset.assetType.replace(/_/g, ' ')}</div>
              {asset.publicUrl ? (
                <a href={asset.publicUrl} target="_blank" rel="noreferrer" className="user-btn-secondary mt-3 inline-flex">
                  Open file
                </a>
              ) : null}
            </div>
          )) : (
            <div className="user-wire">No creative files uploaded yet.</div>
          )}
        </div>
      </div>

      <div className="user-card">
        <h3>{isPreview ? 'Studio uploads' : 'Upload creative files'}</h3>
        <div className="mt-4 space-y-3">
          {isPreview ? (
            <div className="user-wire">Preview mode keeps uploads disabled, but this is where the creative team's files will appear in the real studio.</div>
          ) : (
            <>
              <select value={assetType} onChange={(event) => onAssetTypeChange(event.target.value)} className="input-base">
                <option value="creative_pack">Creative pack</option>
                <option value="brand_asset">Brand asset</option>
                <option value="final_media">Final media</option>
              </select>
              <input type="file" onChange={onAssetFileChange} className="input-base" />
              <div className="user-wire">{assetFileName ?? 'Choose a file to upload into the studio asset set.'}</div>
              <button
                type="button"
                onClick={onUploadAsset}
                disabled={!assetFileName || isUploadingAsset}
                className="user-btn-primary disabled:cursor-not-allowed disabled:opacity-60"
              >
                {isUploadingAsset ? 'Uploading...' : 'Upload file'}
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
