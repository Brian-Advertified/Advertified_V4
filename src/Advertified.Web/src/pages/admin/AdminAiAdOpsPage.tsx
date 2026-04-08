import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { BarChart3, Megaphone, RefreshCcw, Rocket, TrendingUp } from 'lucide-react';
import { AdminPageShell, AdminQueryBoundary, fmtDate, useAdminDashboardQuery } from './adminWorkspace';
import { advertifiedApi } from '../../services/advertifiedApi';

type AdVariant = {
  id: string;
  campaignId: string;
  campaignCreativeId?: string | null;
  platform: string;
  channel: string;
  language: string;
  templateId?: number | null;
  voicePackId?: string | null;
  voicePackName?: string | null;
  script: string;
  audioAssetUrl?: string | null;
  platformAdId?: string | null;
  status: string;
  createdAt: string;
  updatedAt: string;
  publishedAt?: string | null;
};

type MetricsSummary = {
  campaignId: string;
  variantCount: number;
  publishedVariantCount: number;
  impressions: number;
  clicks: number;
  conversions: number;
  costZar: number;
  ctr: number;
  conversionRate: number;
  topVariantId?: string | null;
  topVariantConversionRate?: number | null;
  lastRecordedAt?: string | null;
};

type PlatformConnection = {
  linkId: string;
  connectionId: string;
  campaignId: string;
  provider: string;
  externalAccountId: string;
  accountName: string;
  externalCampaignId?: string | null;
  isPrimary: boolean;
  status: string;
  updatedAt: string;
};

export function AdminAiAdOpsPage() {
  const dashboardQuery = useAdminDashboardQuery();
  const queryClient = useQueryClient();
  const [campaignId, setCampaignId] = useState('');
  const [platform, setPlatform] = useState('Meta');
  const [channel, setChannel] = useState('Digital');
  const [language, setLanguage] = useState('English');
  const [templateId, setTemplateId] = useState<number | ''>('');
  const [voicePackName, setVoicePackName] = useState('');
  const [script, setScript] = useState('');
  const [audioAssetUrl, setAudioAssetUrl] = useState('');
  const [connectionAccountId, setConnectionAccountId] = useState('');
  const [connectionAccountName, setConnectionAccountName] = useState('');
  const [connectionCampaignId, setConnectionCampaignId] = useState('');
  const [connectionPrimary, setConnectionPrimary] = useState(true);
  const [message, setMessage] = useState<string | null>(null);

  const variantsQuery = useQuery({
    queryKey: ['admin-ai-ad-ops-variants', campaignId],
    enabled: campaignId.trim().length > 0,
    queryFn: () => advertifiedApi.getAiAdVariants(campaignId.trim()),
  });

  const summaryQuery = useQuery({
    queryKey: ['admin-ai-ad-ops-summary', campaignId],
    enabled: campaignId.trim().length > 0,
    queryFn: () => advertifiedApi.getAiCampaignAdMetricsSummary(campaignId.trim()),
  });

  const connectionsQuery = useQuery({
    queryKey: ['admin-ai-ad-ops-connections', campaignId],
    enabled: campaignId.trim().length > 0,
    queryFn: () => advertifiedApi.getAiCampaignPlatformConnections(campaignId.trim()),
  });

  const createVariantMutation = useMutation({
    mutationFn: () => advertifiedApi.createAiAdVariant({
      campaignId: campaignId.trim(),
      platform,
      channel,
      language,
      templateId: typeof templateId === 'number' ? templateId : undefined,
      voicePackName: voicePackName.trim() || undefined,
      script: script.trim(),
      audioAssetUrl: audioAssetUrl.trim() || undefined,
    }),
    onSuccess: () => {
      setMessage('Ad variant created.');
      queryClient.invalidateQueries({ queryKey: ['admin-ai-ad-ops-variants', campaignId] });
      queryClient.invalidateQueries({ queryKey: ['admin-ai-ad-ops-summary', campaignId] });
    },
  });

  const syncMetricsMutation = useMutation({
    mutationFn: () => advertifiedApi.syncAiCampaignAdMetrics(campaignId.trim()),
    onSuccess: () => {
      setMessage('Metrics sync completed.');
      queryClient.invalidateQueries({ queryKey: ['admin-ai-ad-ops-summary', campaignId] });
      queryClient.invalidateQueries({ queryKey: ['admin-ai-ad-ops-variants', campaignId] });
    },
  });

  const publishVariantMutation = useMutation({
    mutationFn: (variantId: string) => advertifiedApi.publishAiAdVariant(variantId),
    onSuccess: () => {
      setMessage('Variant published.');
      queryClient.invalidateQueries({ queryKey: ['admin-ai-ad-ops-variants', campaignId] });
      queryClient.invalidateQueries({ queryKey: ['admin-ai-ad-ops-summary', campaignId] });
    },
  });

  const trackConversionMutation = useMutation({
    mutationFn: ({ variantId, conversions }: { variantId: string; conversions: number }) =>
      advertifiedApi.trackAiAdConversion(variantId, conversions),
    onSuccess: () => {
      setMessage('Conversion captured.');
      queryClient.invalidateQueries({ queryKey: ['admin-ai-ad-ops-summary', campaignId] });
    },
  });

  const optimizeMutation = useMutation({
    mutationFn: () => advertifiedApi.optimizeAiCampaign(campaignId.trim()),
    onSuccess: (result) => {
      setMessage(result.message);
      queryClient.invalidateQueries({ queryKey: ['admin-ai-ad-ops-summary', campaignId] });
      queryClient.invalidateQueries({ queryKey: ['admin-ai-ad-ops-variants', campaignId] });
    },
  });

  const upsertConnectionMutation = useMutation({
    mutationFn: () => advertifiedApi.upsertAiCampaignPlatformConnection(campaignId.trim(), {
      provider: platform,
      externalAccountId: connectionAccountId.trim(),
      accountName: connectionAccountName.trim(),
      externalCampaignId: connectionCampaignId.trim() || undefined,
      isPrimary: connectionPrimary,
      status: 'active',
    }),
    onSuccess: () => {
      setMessage('Campaign platform connection saved.');
      queryClient.invalidateQueries({ queryKey: ['admin-ai-ad-ops-connections', campaignId] });
    },
  });

  const canCreate = useMemo(
    () => campaignId.trim().length > 0 && script.trim().length > 0 && !createVariantMutation.isPending,
    [campaignId, script, createVariantMutation.isPending],
  );
  const canSaveConnection = useMemo(
    () =>
      campaignId.trim().length > 0
      && connectionAccountId.trim().length > 0
      && connectionAccountName.trim().length > 0
      && !upsertConnectionMutation.isPending,
    [campaignId, connectionAccountId, connectionAccountName, upsertConnectionMutation.isPending],
  );

  return (
    <AdminQueryBoundary query={dashboardQuery}>
      {() => (
        <AdminPageShell title="AI Ad Operations" description="Create ad variants, publish to platform connectors, sync metrics, and monitor conversion performance.">
          <div className="panel p-6">
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.2em] text-ink-soft">Campaign scope</p>
                <h2 className="mt-2 text-2xl font-semibold text-ink">Ad variants and performance loop</h2>
                <p className="mt-2 text-sm text-ink-soft">Use this page to run the full campaign ad lifecycle in one place.</p>
              </div>
              <div className="rounded-2xl border border-brand/30 bg-brand-soft px-4 py-3 text-xs text-brand">
                <div className="flex items-center gap-2 font-semibold">
                  <Megaphone className="size-4" />
                  Meta + Google-ready flow
                </div>
              </div>
            </div>

            <div className="mt-6 grid gap-3 md:grid-cols-2 xl:grid-cols-3">
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft md:col-span-2 xl:col-span-3">
                Campaign ID
                <input className="input-base" value={campaignId} onChange={(event) => setCampaignId(event.target.value)} placeholder="Campaign GUID" />
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                Platform
                <select className="input-base" value={platform} onChange={(event) => setPlatform(event.target.value)}>
                  <option value="Meta">Meta</option>
                  <option value="GoogleAds">Google Ads</option>
                </select>
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                Channel
                <input className="input-base" value={channel} onChange={(event) => setChannel(event.target.value)} />
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                Language
                <input className="input-base" value={language} onChange={(event) => setLanguage(event.target.value)} />
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                Template ID
                <input className="input-base" type="number" value={templateId} onChange={(event) => setTemplateId(event.target.value ? Number(event.target.value) : '')} />
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft md:col-span-2">
                Voice pack name
                <input className="input-base" value={voicePackName} onChange={(event) => setVoicePackName(event.target.value)} />
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft xl:col-span-3">
                Script
                <textarea className="input-base min-h-[120px]" value={script} onChange={(event) => setScript(event.target.value)} placeholder="Enter the ad script for this variant." />
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft xl:col-span-3">
                Audio asset URL (optional)
                <input className="input-base" value={audioAssetUrl} onChange={(event) => setAudioAssetUrl(event.target.value)} placeholder="https://..." />
              </label>
            </div>

            <div className="mt-6 rounded-2xl border border-line bg-slate-50/70 p-4">
              <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Platform connection</p>
              <div className="mt-3 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
                <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                  Account ID
                  <input
                    className="input-base"
                    value={connectionAccountId}
                    onChange={(event) => setConnectionAccountId(event.target.value)}
                    placeholder="act_123..."
                  />
                </label>
                <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                  Account name
                  <input
                    className="input-base"
                    value={connectionAccountName}
                    onChange={(event) => setConnectionAccountName(event.target.value)}
                    placeholder="Meta Business Account"
                  />
                </label>
                <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                  External campaign ID
                  <input
                    className="input-base"
                    value={connectionCampaignId}
                    onChange={(event) => setConnectionCampaignId(event.target.value)}
                    placeholder="optional"
                  />
                </label>
                <label className="flex items-center gap-2 pt-6 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                  <input
                    type="checkbox"
                    checked={connectionPrimary}
                    onChange={(event) => setConnectionPrimary(event.target.checked)}
                  />
                  Primary link
                </label>
              </div>
              <div className="mt-3">
                <button
                  type="button"
                  onClick={() => upsertConnectionMutation.mutate()}
                  disabled={!canSaveConnection}
                  className="button-secondary px-4 py-2 text-sm font-semibold disabled:opacity-50"
                >
                  Save platform connection
                </button>
              </div>
              {upsertConnectionMutation.error instanceof Error ? (
                <p className="mt-2 text-sm text-rose-600">{upsertConnectionMutation.error.message}</p>
              ) : null}
            </div>

            <div className="mt-4 flex flex-wrap gap-2">
              <button type="button" onClick={() => createVariantMutation.mutate()} disabled={!canCreate} className="button-primary inline-flex items-center gap-2 px-4 py-2 text-sm font-semibold disabled:cursor-not-allowed disabled:opacity-50">
                <Rocket className="size-4" />
                Create variant
              </button>
              <button type="button" onClick={() => syncMetricsMutation.mutate()} disabled={!campaignId.trim() || syncMetricsMutation.isPending} className="button-secondary inline-flex items-center gap-2 px-4 py-2 text-sm font-semibold disabled:opacity-50">
                <RefreshCcw className="size-4" />
                Sync metrics
              </button>
              <button type="button" onClick={() => optimizeMutation.mutate()} disabled={!campaignId.trim() || optimizeMutation.isPending} className="button-secondary inline-flex items-center gap-2 px-4 py-2 text-sm font-semibold disabled:opacity-50">
                <TrendingUp className="size-4" />
                Auto-optimize
              </button>
            </div>

            {message ? <p className="mt-3 text-sm text-brand">{message}</p> : null}
            {createVariantMutation.error instanceof Error ? <p className="mt-2 text-sm text-rose-600">{createVariantMutation.error.message}</p> : null}
            {syncMetricsMutation.error instanceof Error ? <p className="mt-2 text-sm text-rose-600">{syncMetricsMutation.error.message}</p> : null}
            {optimizeMutation.error instanceof Error ? <p className="mt-2 text-sm text-rose-600">{optimizeMutation.error.message}</p> : null}
          </div>

          <div className="grid gap-4 md:grid-cols-3">
            <StatCard icon={BarChart3} label="Impressions" value={String((summaryQuery.data as MetricsSummary | undefined)?.impressions ?? 0)} />
            <StatCard icon={TrendingUp} label="Clicks" value={String((summaryQuery.data as MetricsSummary | undefined)?.clicks ?? 0)} />
            <StatCard icon={Rocket} label="Conversions" value={String((summaryQuery.data as MetricsSummary | undefined)?.conversions ?? 0)} />
          </div>

          <div className="panel overflow-hidden p-0">
            <div className="border-b border-line px-6 py-4">
              <h3 className="text-lg font-semibold text-ink">Ad variants</h3>
            </div>
            <div className="overflow-x-auto">
              <table className="min-w-full text-left text-sm">
                <thead className="bg-slate-50 text-xs uppercase tracking-[0.14em] text-ink-soft">
                  <tr>
                    <th className="px-4 py-3">Platform</th>
                    <th className="px-4 py-3">Status</th>
                    <th className="px-4 py-3">Platform Ad ID</th>
                    <th className="px-4 py-3">Updated</th>
                    <th className="px-4 py-3 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {((variantsQuery.data ?? []) as AdVariant[]).map((item) => (
                    <tr key={item.id} className="border-t border-line">
                      <td className="px-4 py-3 text-ink">{item.platform}</td>
                      <td className="px-4 py-3 text-ink-soft">{item.status}</td>
                      <td className="px-4 py-3 font-mono text-xs text-ink-soft">{item.platformAdId ?? 'Not published'}</td>
                      <td className="px-4 py-3 text-ink-soft">{fmtDate(item.updatedAt)}</td>
                      <td className="px-4 py-3">
                        <div className="flex justify-end gap-2">
                          <button
                            type="button"
                            onClick={() => publishVariantMutation.mutate(item.id)}
                            disabled={publishVariantMutation.isPending || item.status === 'published'}
                            className="button-secondary px-3 py-1.5 text-xs font-semibold disabled:opacity-50"
                          >
                            Publish
                          </button>
                          <button
                            type="button"
                            onClick={() => trackConversionMutation.mutate({ variantId: item.id, conversions: 1 })}
                            disabled={trackConversionMutation.isPending}
                            className="button-secondary px-3 py-1.5 text-xs font-semibold disabled:opacity-50"
                          >
                            +1 Conversion
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                  {!variantsQuery.isLoading && (variantsQuery.data ?? []).length === 0 ? (
                    <tr>
                      <td colSpan={5} className="px-4 py-6 text-center text-sm text-ink-soft">No ad variants for this campaign yet.</td>
                    </tr>
                  ) : null}
                </tbody>
              </table>
            </div>
            {publishVariantMutation.error instanceof Error ? <p className="px-6 py-3 text-sm text-rose-600">{publishVariantMutation.error.message}</p> : null}
            {trackConversionMutation.error instanceof Error ? <p className="px-6 pb-4 text-sm text-rose-600">{trackConversionMutation.error.message}</p> : null}
          </div>

          <div className="panel overflow-hidden p-0">
            <div className="border-b border-line px-6 py-4">
              <h3 className="text-lg font-semibold text-ink">Campaign platform links</h3>
            </div>
            <div className="overflow-x-auto">
              <table className="min-w-full text-left text-sm">
                <thead className="bg-slate-50 text-xs uppercase tracking-[0.14em] text-ink-soft">
                  <tr>
                    <th className="px-4 py-3">Provider</th>
                    <th className="px-4 py-3">Account</th>
                    <th className="px-4 py-3">External Campaign</th>
                    <th className="px-4 py-3">Primary</th>
                    <th className="px-4 py-3">Updated</th>
                  </tr>
                </thead>
                <tbody>
                  {((connectionsQuery.data ?? []) as PlatformConnection[]).map((item) => (
                    <tr key={item.linkId} className="border-t border-line">
                      <td className="px-4 py-3 text-ink">{item.provider}</td>
                      <td className="px-4 py-3">
                        <div className="text-ink">{item.accountName}</div>
                        <div className="font-mono text-xs text-ink-soft">{item.externalAccountId}</div>
                      </td>
                      <td className="px-4 py-3 font-mono text-xs text-ink-soft">{item.externalCampaignId ?? 'Not linked'}</td>
                      <td className="px-4 py-3 text-ink-soft">{item.isPrimary ? 'Yes' : 'No'}</td>
                      <td className="px-4 py-3 text-ink-soft">{fmtDate(item.updatedAt)}</td>
                    </tr>
                  ))}
                  {!connectionsQuery.isLoading && (connectionsQuery.data ?? []).length === 0 ? (
                    <tr>
                      <td colSpan={5} className="px-4 py-6 text-center text-sm text-ink-soft">
                        No platform links for this campaign yet.
                      </td>
                    </tr>
                  ) : null}
                </tbody>
              </table>
            </div>
          </div>
        </AdminPageShell>
      )}
    </AdminQueryBoundary>
  );
}

function StatCard({
  icon: Icon,
  label,
  value,
}: {
  icon: typeof BarChart3;
  label: string;
  value: string;
}) {
  return (
    <div className="panel p-5">
      <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
        <Icon className="size-4 text-brand" />
        {label}
      </div>
      <p className="mt-4 text-3xl font-semibold text-ink">{value}</p>
    </div>
  );
}
