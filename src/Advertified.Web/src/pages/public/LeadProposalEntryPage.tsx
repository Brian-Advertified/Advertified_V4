import { useQuery } from '@tanstack/react-query';
import { ArrowRight, Download, Mail } from 'lucide-react';
import { Navigate, useParams, useSearchParams } from 'react-router-dom';
import { LoadingState } from '../../components/ui/LoadingState';
import { PageHero } from '../../components/marketing/PageHero';
import { parseCampaignOpportunityContext } from '../../features/campaigns/briefModel';
import { getCampaignRecommendations } from '../../features/campaigns/recommendationSelection';
import { formatCurrency } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';

const supportEmail = 'support@advertified.com';

export function LeadProposalEntryPage() {
  const { id = '' } = useParams();
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token')?.trim() ?? '';

  const proposalQuery = useQuery({
    queryKey: ['public-lead-proposal', id, token],
    queryFn: () => advertifiedApi.getPublicProposal(id, token),
    enabled: Boolean(id && token),
    retry: false,
  });

  if (!id) {
    return <Navigate to="/" replace />;
  }

  if (!token) {
    return (
      <section className="page-shell space-y-8 pb-20">
        <PageHero
          kicker="Growth opportunity"
          title="This lead proposal link is incomplete."
          description="Use the full link from your email so we can open your tailored growth options."
        />
      </section>
    );
  }

  if (proposalQuery.isLoading) {
    return <LoadingState label="Loading growth snapshot..." />;
  }

  if (proposalQuery.isError || !proposalQuery.data) {
    return (
      <section className="page-shell space-y-8 pb-20">
        <PageHero
          kicker="Growth opportunity"
          title="This lead proposal link is unavailable."
          description="The link may have expired. Reply to your Advertified contact and we will resend it."
        />
      </section>
    );
  }

  const campaign = proposalQuery.data;
  const recommendations = getCampaignRecommendations(campaign);
  const opportunityContext = parseCampaignOpportunityContext(campaign.brief);
  const detectedGaps = opportunityContext?.detectedGaps?.filter(Boolean) ?? [];
  const areaLabel = campaign.brief?.cities?.[0]
    ?? campaign.brief?.areas?.[0]
    ?? campaign.brief?.provinces?.[0]
    ?? campaign.industry
    ?? 'your market';

  const mailtoLink = `mailto:${supportEmail}?subject=${encodeURIComponent(`Growth options for ${campaign.campaignName}`)}`;

  async function handleDownloadPdf() {
    await advertifiedApi.downloadPublicFile(
      `/public/proposals/${encodeURIComponent(id)}/recommendation-pdf?token=${encodeURIComponent(token)}`,
      `growth-opportunity-${id}.pdf`,
    );
  }

  return (
    <section className="page-shell space-y-8 pb-20">
      <PageHero
        kicker="Growth opportunity snapshot"
        title={campaign.campaignName}
        description={`We reviewed public market signals for ${campaign.businessName ?? campaign.clientName ?? campaign.campaignName} and prepared three rollout options.`}
      />

      <div className="grid gap-5 xl:grid-cols-[minmax(0,0.95fr)_minmax(340px,420px)]">
        <div className="space-y-5">
          <div className="rounded-[22px] border border-line bg-white p-6 shadow-[0_12px_36px_rgba(15,23,42,0.04)]">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-brand">Who we are</p>
            <p className="mt-3 text-sm leading-7 text-ink-soft">
              Advertified is a South African advertising company focused on helping businesses capture more demand using real market signals and practical campaign execution.
            </p>
          </div>

          <div className="rounded-[22px] border border-line bg-white p-6 shadow-[0_12px_36px_rgba(15,23,42,0.04)]">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-brand">What we found</p>
            <p className="mt-3 text-sm leading-7 text-ink-soft">
              We identified opportunity gaps in how demand is currently being captured in {areaLabel}.
            </p>
            {detectedGaps.length > 0 ? (
              <ul className="mt-3 list-disc space-y-2 pl-5 text-sm leading-7 text-ink">
                {detectedGaps.slice(0, 4).map((gap) => (
                  <li key={gap}>{gap}</li>
                ))}
              </ul>
            ) : (
              <p className="mt-3 text-sm leading-7 text-ink-soft">
                We found clear room to improve demand capture and local visibility with a practical multi-channel rollout.
              </p>
            )}
          </div>

          <div className="rounded-[22px] border border-line bg-white p-6 shadow-[0_12px_36px_rgba(15,23,42,0.04)]">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-brand">Recommended growth paths</p>
            {recommendations.length > 0 ? (
              <div className="mt-4 grid gap-3 md:grid-cols-3">
                {recommendations.map((proposal, index) => (
                  <article key={proposal.id} className="rounded-[14px] border border-line bg-slate-50/70 p-4">
                    <p className="text-sm font-semibold text-ink">{proposal.proposalLabel ?? `Proposal ${index + 1}`}</p>
                    <p className="mt-1 text-sm font-semibold text-ink">{formatCurrency(proposal.totalCost)}</p>
                    <p className="mt-2 text-xs leading-6 text-ink-soft">
                      {proposal.proposalStrategy ?? proposal.summary ?? 'Growth option tailored to your market'}
                    </p>
                  </article>
                ))}
              </div>
            ) : (
              <p className="mt-3 text-sm leading-7 text-ink-soft">
                We are finalizing your proposal options now. Reply to this email and we will share the full rollout.
              </p>
            )}
          </div>
        </div>

        <aside className="rounded-[22px] border border-brand/20 bg-[linear-gradient(180deg,#f7fcfa_0%,#ffffff_100%)] p-6 shadow-[0_18px_44px_rgba(15,118,110,0.08)]">
          <div className="text-lg font-semibold text-ink">Next step</div>
          <p className="mt-2 text-sm leading-7 text-ink-soft">
            Reply and we will walk you through the options in 15 minutes. Campaigns can be launched with buy now, pay later.
          </p>
          <div className="mt-5 space-y-3">
            <a
              href={mailtoLink}
              className="button-primary flex w-full items-center justify-center gap-2 px-5 py-3"
            >
              Reply to Advertified
              <Mail className="size-4" />
            </a>
            <button
              type="button"
              onClick={() => void handleDownloadPdf()}
              className="button-secondary flex w-full items-center justify-center gap-2 px-5 py-3"
            >
              Download PDF snapshot
              <Download className="size-4" />
            </button>
            <a
              href={mailtoLink}
              className="user-btn-secondary flex w-full items-center justify-center gap-2 px-5 py-3 text-sm"
            >
              Ask for a callback
              <ArrowRight className="size-4" />
            </a>
          </div>
        </aside>
      </div>
    </section>
  );
}
