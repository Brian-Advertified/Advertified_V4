import { ArrowRight } from 'lucide-react';
import { Link } from 'react-router-dom';
import { ProspectQuestionnaireForm } from '../../features/campaigns/components/ProspectQuestionnaireForm';

export function ProspectQuestionnairePage() {
  return (
    <section className="page-shell py-10">
      <div className="mx-auto max-w-5xl space-y-8">
        <div className="grid gap-6 lg:grid-cols-[minmax(0,1.2fr)_340px]">
          <ProspectQuestionnaireForm />

          <aside className="space-y-6">
            <div className="panel px-6 py-6">
              <p className="text-xs uppercase tracking-[0.18em] text-ink-soft">Best route for you</p>
              <div className="mt-4 space-y-4 text-sm leading-6 text-ink-soft">
                <div className="rounded-[20px] border border-brand/15 bg-brand-soft/20 px-4 py-4">
                  <p className="font-semibold text-ink">Start here if you want guidance first.</p>
                  <p className="mt-2">Use this route if you are still working out the right package, channel mix, or campaign setup.</p>
                </div>
                <div className="rounded-[20px] border border-line bg-white px-4 py-4">
                  <p className="font-semibold text-ink">Choose packages first if you already know your spend band.</p>
                  <p className="mt-2">That route is faster if the commercial decision is already clear and you are ready to move toward payment.</p>
                </div>
              </div>
            </div>
            <div className="panel px-6 py-6">
              <p className="text-xs uppercase tracking-[0.18em] text-ink-soft">What happens next</p>
              <ul className="mt-4 space-y-3 text-sm leading-6 text-ink-soft">
                <li>1. Your answers are turned into a working campaign brief for our planning team.</li>
                <li>2. An Advertified agent reviews the brief and checks the strongest route forward.</li>
                <li>3. We use that brief to shape tailored media recommendations for your business.</li>
              </ul>
            </div>
            <div className="panel hero-mint px-6 py-6 text-ink">
              <p className="text-xs uppercase tracking-[0.18em] text-ink-soft">Already know your budget?</p>
              <h2 className="mt-2 text-2xl font-semibold">You can still start from package selection.</h2>
              <p className="mt-3 text-sm leading-7 text-ink-soft">
                This questionnaire is the guidance-first route. If the spend band is already clear and you want to move toward checkout first, package purchase is still available.
              </p>
              <Link to="/packages" className="mt-5 inline-flex items-center gap-2 font-semibold text-brand">
                Browse packages
                <ArrowRight className="size-4" />
              </Link>
            </div>
          </aside>
        </div>
      </div>
    </section>
  );
}

export default ProspectQuestionnairePage;
