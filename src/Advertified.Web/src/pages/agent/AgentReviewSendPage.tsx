import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Download, Eye, Pencil, Send, Trash2 } from 'lucide-react';
import { Link } from 'react-router-dom';
import { useToast } from '../../components/ui/toast';
import { advertifiedApi } from '../../services/advertifiedApi';
import {
  AgentPageShell,
  AgentQueryBoundary,
  fmtCurrency,
  titleize,
  useAgentCampaignsQuery,
} from './agentWorkspace';
import { ActionIconButton } from './agentSectionShared';

export function AgentReviewSendPage() {
  const campaignsQuery = useAgentCampaignsQuery();
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const sendMutation = useMutation({
    mutationFn: (campaignId: string) => advertifiedApi.sendRecommendationToClient(campaignId),
    onSuccess: async (_, campaignId) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['agent-campaigns'] }),
        queryClient.invalidateQueries({ queryKey: ['agent-campaign', campaignId] }),
        queryClient.invalidateQueries({ queryKey: ['agent-inbox'] }),
      ]);
      pushToast({ title: 'Recommendation sent.', description: 'The recommendation was sent to the client and moved into review.' });
    },
    onError: (error) => pushToast({ title: 'Could not send recommendation.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });
  const deleteDraftMutation = useMutation({
    mutationFn: ({ campaignId, recommendationId }: { campaignId: string; recommendationId: string }) => advertifiedApi.deleteRecommendation(campaignId, recommendationId),
    onSuccess: async (_, variables) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['agent-campaigns'] }),
        queryClient.invalidateQueries({ queryKey: ['agent-campaign', variables.campaignId] }),
        queryClient.invalidateQueries({ queryKey: ['agent-inbox'] }),
      ]);
      pushToast({ title: 'Draft recommendation deleted.', description: 'The draft was removed from this campaign.' }, 'info');
    },
    onError: (error) => pushToast({ title: 'Could not delete draft.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  return (
    <AgentQueryBoundary query={campaignsQuery} loadingLabel="Loading review and send...">
      <AgentPageShell title="Review and send" description="Finalize client-facing recommendations, preview the client PDF, and send only the campaigns that are ready to move out of strategist review.">
        {(() => {
          const rows = (campaignsQuery.data ?? [])
            .filter((campaign) => campaign.recommendations.some((item) => item.status === 'draft' || item.status === 'sent_to_client'))
            .sort((left, right) => new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime());

          return (
            <section className="space-y-6">
              <div className="grid gap-4 md:grid-cols-3">
                <div className="panel p-6"><p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Ready to send</p><p className="mt-4 text-3xl font-semibold text-ink">{rows.filter((item) => item.recommendations.some((rec) => rec.status === 'draft')).length}</p></div>
                <div className="panel p-6"><p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Sent to client</p><p className="mt-4 text-3xl font-semibold text-ink">{rows.filter((item) => item.recommendations.some((rec) => rec.status === 'sent_to_client')).length}</p></div>
                <div className="panel p-6"><p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">PDF available</p><p className="mt-4 text-3xl font-semibold text-ink">{rows.filter((item) => item.recommendationPdfUrl).length}</p></div>
              </div>

              <div className="overflow-hidden rounded-[28px] border border-line bg-white">
                <table className="w-full border-collapse text-sm">
                  <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft">
                    <tr>
                      <th className="px-4 py-4">Campaign</th>
                      <th className="px-4 py-4">Recommendation</th>
                      <th className="px-4 py-4">Client signals</th>
                      <th className="px-4 py-4">PDF</th>
                      <th className="px-4 py-4 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map((campaign) => {
                      const active = campaign.recommendations[0] ?? campaign.recommendation;
                      return (
                        <tr key={campaign.id} className="border-t border-line">
                          <td className="px-4 py-4">
                            <p className="font-semibold text-ink">{campaign.campaignName}</p>
                            <p className="text-xs text-ink-soft">{campaign.clientName ?? campaign.businessName ?? 'Client account'}</p>
                          </td>
                          <td className="px-4 py-4 text-ink-soft">
                            <p>{active?.proposalLabel ?? 'Recommendation draft'}</p>
                            <p className="text-xs">{titleize(active?.status ?? 'draft')} | {fmtCurrency(active?.totalCost)} | {campaign.recommendations.length} option(s)</p>
                          </td>
                          <td className="px-4 py-4 text-ink-soft">
                            {active?.clientFeedbackNotes ?? campaign.nextAction}
                          </td>
                          <td className="px-4 py-4 text-ink-soft">{campaign.recommendationPdfUrl ? 'Available' : 'Not generated yet'}</td>
                          <td className="px-4 py-4">
                            <div className="flex justify-end gap-2">
                              <Link to={`/agent/campaigns/${campaign.id}`} className="button-secondary p-2" title={`View ${campaign.campaignName}`}>
                                <Eye className="size-4" />
                              </Link>
                              <Link to={`/agent/recommendations/new?campaignId=${campaign.id}`} className="button-secondary p-2" title={`Edit ${campaign.campaignName}`}>
                                <Pencil className="size-4" />
                              </Link>
                              {campaign.recommendationPdfUrl ? (
                                <button
                                  type="button"
                                  onClick={() => {
                                    void advertifiedApi.downloadProtectedFile(
                                      campaign.recommendationPdfUrl!,
                                      `recommendation-${campaign.id}.pdf`,
                                    );
                                  }}
                                  className="button-secondary p-2"
                                  title={`Preview client PDF for ${campaign.campaignName}`}
                                >
                                  <Download className="size-4" />
                                </button>
                              ) : null}
                              {active?.status === 'draft' ? (
                                <ActionIconButton
                                  title={`Send ${campaign.campaignName} to client`}
                                  disabled={sendMutation.isPending || campaign.recommendations.length < 3}
                                  onClick={() => sendMutation.mutate(campaign.id)}
                                >
                                  <Send className="size-4" />
                                </ActionIconButton>
                              ) : null}
                              {active?.status === 'draft' ? (
                                <ActionIconButton
                                  title={`Delete draft for ${campaign.campaignName}`}
                                  tone="danger"
                                  disabled={deleteDraftMutation.isPending}
                                  onClick={() => {
                                    if (active?.id && window.confirm(`Delete the draft recommendation for ${campaign.campaignName}?`)) {
                                      deleteDraftMutation.mutate({ campaignId: campaign.id, recommendationId: active.id });
                                    }
                                  }}
                                >
                                  <Trash2 className="size-4" />
                                </ActionIconButton>
                              ) : null}
                            </div>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </section>
          );
        })()}
      </AgentPageShell>
    </AgentQueryBoundary>
  );
}
