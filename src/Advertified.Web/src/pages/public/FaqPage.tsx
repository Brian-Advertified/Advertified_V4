import { Link } from 'react-router-dom';
import { PageHero } from '../../components/marketing/PageHero';

const faqSections = [
  {
    title: 'Getting started',
    items: [
      {
        question: 'How does Advertified work?',
        answer: 'You choose a package, set your working budget, complete checkout, and then move into a guided campaign workspace where briefing, planning, approvals, and launch progress are tracked for you.',
      },
      {
        question: 'Do I have to know which channels to buy first?',
        answer: 'No. Advertified is designed so you can start from your budget and campaign objective first. The planning and recommendation process then shapes the best media mix for the campaign.',
      },
      {
        question: 'Can I still work with a real person?',
        answer: 'Yes. The platform supports campaign-manager involvement, in-app messaging, recommendation review, and a creative-production workflow when human help is needed.',
      },
    ],
  },
  {
    title: 'Packages, payment, and pricing',
    items: [
      {
        question: 'What happens after I purchase a package?',
        answer: 'Your package purchase creates the campaign shell, unlocks the workflow, and gives you access to the next required step in the campaign workspace.',
      },
      {
        question: 'Can I adjust the amount within a package band?',
        answer: 'Yes. The platform supports working within the package range you selected, so your campaign can be shaped around the amount you actually want to deploy.',
      },
      {
        question: 'Will I see detailed media line-item prices as a client?',
        answer: 'No. Client-facing recommendation screens and recommendation PDFs are intentionally simplified to show overall campaign totals rather than internal line-item pricing.',
      },
      {
        question: 'Do you support refunds?',
        answer: 'Yes, but refund treatment depends on campaign stage. Before work starts, refunds may be full less any non-recoverable gateway fees. During planning or strategy, partial refunds may apply. After recommendation, creative work, or activation, only unused or uncommitted value may be refundable.',
      },
    ],
  },
  {
    title: 'Campaign workflow',
    items: [
      {
        question: 'What are the main campaign steps?',
        answer: 'The core flow is package purchase, recommendation approval, creative production, final creative approval, and launch. The platform tracks those steps so clients and internal teams can work from the same state.',
      },
      {
        question: 'Can I request changes before launch?',
        answer: 'Yes. Recommendation approvals and final creative review both support change-request workflows, and those requests are tracked formally in the backend.',
      },
      {
        question: 'Can campaigns be paused?',
        answer: 'Yes. Admin operations can pause and unpause campaigns, and the platform keeps track of the remaining time so the effective days left stay meaningful.',
      },
      {
        question: 'Can I message the team inside the platform?',
        answer: 'Yes. Campaign messaging is now a real persisted conversation flow between the client side and the assigned internal team context.',
      },
    ],
  },
  {
    title: 'Creative and launch',
    items: [
      {
        question: 'What is the Advertified AI Studio?',
        answer: 'The AI Studio is the creative-production environment used to build campaign concepts, channel adaptations, assets, and final media packs after a recommendation has been approved.',
      },
      {
        question: 'Do I approve the final creative before the campaign goes live?',
        answer: 'Yes. Finished media is sent back for client approval, and launch can be tracked separately from creative sign-off so activation is not confused with approval.',
      },
      {
        question: 'Where are invoices and PDFs stored?',
        answer: 'Generated recommendation and invoice PDFs are stored through the platform storage layer so they can be surfaced back into the workflow when needed.',
      },
    ],
  },
] as const;

export function FaqPage() {
  return (
    <div className="page-shell space-y-8 pb-16">
      <PageHero
        kicker="FAQ"
        title="Questions businesses usually ask before they launch"
        description="A straightforward guide to how packages, approvals, creative production, payments, and campaign support work inside Advertified."
        actions={(
          <>
            <Link to="/packages" className="hero-primary-button rounded-full font-semibold">
              Explore packages
            </Link>
            <Link to="/partner-enquiry" className="hero-secondary-button rounded-full font-semibold">
              Contact our team
            </Link>
          </>
        )}
      />

      <section className="grid gap-6 lg:grid-cols-[0.9fr_1.1fr]">
        <div className="panel px-6 py-7 sm:px-8">
          <p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">What this covers</p>
          <h2 className="mt-4 text-2xl font-semibold tracking-tight text-ink">Everything a client needs to understand the journey</h2>
          <p className="mt-4 text-sm leading-7 text-ink-soft">
            This page focuses on the customer-facing flow: package selection, payment, approvals, creative handoff, launch, messaging, and operational support.
          </p>
        </div>

        <div className="panel px-6 py-7 sm:px-8">
          <p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">Need something else?</p>
          <h2 className="mt-4 text-2xl font-semibold tracking-tight text-ink">Still need clarity on your campaign?</h2>
          <p className="mt-4 text-sm leading-7 text-ink-soft">
            If your question is specific to payment timing, media suitability, brand requirements, or launch planning, our team can guide you before you commit.
          </p>
          <div className="mt-6 flex flex-wrap gap-3">
            <Link to="/partner-enquiry" className="button-primary px-5 py-3">
              Speak to us
            </Link>
            <Link to="/how-it-works" className="button-secondary px-5 py-3">
              Read how it works
            </Link>
          </div>
        </div>
      </section>

      <section className="space-y-6">
        {faqSections.map((section) => (
          <div key={section.title} className="panel px-6 py-7 sm:px-8">
            <div className="max-w-3xl">
              <p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">{section.title}</p>
            </div>
            <div className="mt-6 space-y-4">
              {section.items.map((item) => (
                <details key={item.question} className="group rounded-[24px] border border-line bg-white px-5 py-4 shadow-[0_18px_40px_rgba(17,24,39,0.04)]">
                  <summary className="cursor-pointer list-none text-base font-semibold text-ink marker:content-none">
                    <span className="flex items-center justify-between gap-4">
                      <span>{item.question}</span>
                      <span className="rounded-full border border-line px-3 py-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft transition group-open:border-brand group-open:text-brand">
                        View
                      </span>
                    </span>
                  </summary>
                  <p className="mt-4 max-w-4xl text-sm leading-7 text-ink-soft sm:text-[15px]">
                    {item.answer}
                  </p>
                </details>
              ))}
            </div>
          </div>
        ))}
      </section>
    </div>
  );
}
