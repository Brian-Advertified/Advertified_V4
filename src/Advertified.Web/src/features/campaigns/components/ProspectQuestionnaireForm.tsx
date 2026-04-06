import { useMutation, useQuery } from '@tanstack/react-query';
import { ClipboardList } from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useToast } from '../../../components/ui/toast';
import { catalogQueryOptions } from '../../../lib/catalogQueryOptions';
import { advertifiedApi } from '../../../services/advertifiedApi';

type QuestionnaireForm = {
  fullName: string;
  email: string;
  phone: string;
  businessName: string;
  industry: string;
  packageBandId: string;
  campaignName: string;
  objective: string;
  geographyScope: string;
  primaryArea: string;
  ageRange: string;
  gender: string;
  language: string;
  preferredMediaTypes: string[];
  targetAudienceNotes: string;
  valueProposition: string;
  growthGoal: string;
  currentCustomers: string;
  specialRequirements: string;
};

const OBJECTIVES = [
  { value: 'awareness', label: 'Brand awareness' },
  { value: 'leads', label: 'Lead generation' },
  { value: 'foot_traffic', label: 'Foot traffic' },
  { value: 'promotion', label: 'Promotion' },
  { value: 'launch', label: 'Launch' },
];

const CHANNELS = [
  { value: 'ooh', label: 'Billboards and digital screens' },
  { value: 'radio', label: 'Radio' },
  { value: 'tv', label: 'TV' },
  { value: 'digital', label: 'Digital' },
];

const AGE_RANGES = [
  { value: '', label: 'Prefer not to specify' },
  { value: '18-24', label: '18-24' },
  { value: '25-34', label: '25-34' },
  { value: '35-44', label: '35-44' },
  { value: '45-54', label: '45-54' },
  { value: '55-65', label: '55+' },
];

const GEOGRAPHIES = [
  { value: 'local', label: 'Local' },
  { value: 'provincial', label: 'Provincial' },
  { value: 'national', label: 'National' },
];

function parseAgeRange(value: string): { min?: number; max?: number } {
  if (!value) {
    return {};
  }

  const [min, max] = value.split('-').map((item) => Number.parseInt(item, 10));
  return {
    min: Number.isFinite(min) ? min : undefined,
    max: Number.isFinite(max) ? max : undefined,
  };
}

type ProspectQuestionnaireFormProps = {
  variant?: 'hero' | 'page';
};

export function ProspectQuestionnaireForm({ variant = 'page' }: ProspectQuestionnaireFormProps) {
  const { pushToast } = useToast();
  const [submitted, setSubmitted] = useState<{ campaignId: string; campaignName: string; message: string } | null>(null);
  const [step, setStep] = useState<1 | 2 | 3>(1);
  const [form, setForm] = useState<QuestionnaireForm>({
    fullName: '',
    email: '',
    phone: '',
    businessName: '',
    industry: '',
    packageBandId: '',
    campaignName: '',
    objective: 'awareness',
    geographyScope: 'provincial',
    primaryArea: '',
    ageRange: '',
    gender: '',
    language: '',
    preferredMediaTypes: ['ooh', 'radio'],
    targetAudienceNotes: '',
    valueProposition: '',
    growthGoal: '',
    currentCustomers: '',
    specialRequirements: '',
  });

  const packagesQuery = useQuery({
    queryKey: ['packages'],
    queryFn: advertifiedApi.getPackages,
    ...catalogQueryOptions,
    retry: 1,
  });

  const submitMutation = useMutation({
    mutationFn: async () => {
      const ageRange = parseAgeRange(form.ageRange);
      const areaValue = form.primaryArea.trim();
      const specialRequirements = [
        form.specialRequirements.trim(),
        form.valueProposition.trim() ? `Value proposition: ${form.valueProposition.trim()}` : '',
        form.growthGoal.trim() ? `Growth objective: ${form.growthGoal.trim()}` : '',
        form.currentCustomers.trim() ? `Current customers: ${form.currentCustomers.trim()}` : '',
      ].filter(Boolean).join('\n');

      return advertifiedApi.submitProspectQuestionnaire({
        fullName: form.fullName.trim(),
        email: form.email.trim(),
        phone: form.phone.trim(),
        businessName: form.businessName.trim() || undefined,
        industry: form.industry.trim() || undefined,
        packageBandId: form.packageBandId,
        campaignName: form.campaignName.trim() || undefined,
        brief: {
          objective: form.objective,
          geographyScope: form.geographyScope,
          provinces: form.geographyScope === 'provincial' && areaValue ? [areaValue] : undefined,
          cities: form.geographyScope === 'local' && areaValue ? [areaValue] : undefined,
          targetAgeMin: ageRange.min,
          targetAgeMax: ageRange.max,
          targetGender: form.gender || undefined,
          targetLanguages: form.language.trim() ? [form.language.trim()] : undefined,
          targetAudienceNotes: form.targetAudienceNotes.trim() || undefined,
          targetInterests: [form.valueProposition.trim(), form.growthGoal.trim()].filter(Boolean),
          preferredMediaTypes: form.preferredMediaTypes,
          openToUpsell: false,
          specialRequirements: specialRequirements || undefined,
        },
      });
    },
    onSuccess: (result) => {
      setSubmitted(result);
      pushToast({
        title: 'Questionnaire submitted.',
        description: 'An agent can now review this brief and prepare recommendations.',
      });
    },
  });

  const toggleChannel = (value: string) => {
    setForm((current) => ({
      ...current,
      preferredMediaTypes: current.preferredMediaTypes.includes(value)
        ? current.preferredMediaTypes.filter((item) => item !== value)
        : [...current.preferredMediaTypes, value],
    }));
  };

  const canSubmit = Boolean(
    form.fullName.trim()
    && form.email.trim()
    && form.phone.trim()
    && form.packageBandId
    && form.targetAudienceNotes.trim(),
  );
  const canContinueStep1 = Boolean(
    form.fullName.trim()
    && form.email.trim()
    && form.phone.trim()
    && form.packageBandId,
  );
  const canContinueStep2 = form.preferredMediaTypes.length > 0;

  const containerClassName = variant === 'hero'
    ? 'hero-glass-card rounded-[28px] p-5 text-ink sm:p-6'
    : 'panel px-6 py-8 sm:px-8';
  const titleClassName = variant === 'hero'
    ? 'mt-3 text-2xl font-semibold tracking-tight text-ink'
    : 'mt-4 text-3xl font-semibold tracking-tight text-ink sm:text-4xl';
  const copyClassName = variant === 'hero'
    ? 'mt-3 text-sm leading-6 text-ink-soft'
    : 'mt-4 max-w-3xl text-sm leading-7 text-ink-soft sm:text-[15px]';

  return (
    <div className={containerClassName}>
      <div className="pill bg-white text-brand">Questionnaire</div>
      <h2 className={titleClassName}>
        Start here.
      </h2>
      <p className={copyClassName}>
        Share your campaign requirements and an agent will turn them into a recommendation.
      </p>

      {packagesQuery.isError ? (
        <div className="mt-6 rounded-[20px] border border-amber-200 bg-amber-50 px-4 py-4 text-sm text-amber-900">
          <p className="font-semibold">We could not load package options right now.</p>
          <p className="mt-2 leading-6">
            {packagesQuery.error instanceof Error ? packagesQuery.error.message : 'The package service is taking too long to respond.'}
          </p>
          <div className="mt-4">
            <button
              type="button"
              className="button-secondary px-4 py-2"
              onClick={() => {
                void packagesQuery.refetch();
              }}
            >
              Retry package options
            </button>
          </div>
        </div>
      ) : null}

      {submitted ? (
        <div className="mt-6 rounded-[24px] border border-emerald-200 bg-emerald-50 px-5 py-5">
          <p className="text-sm font-semibold text-emerald-800">Submission received</p>
          <p className="mt-2 text-sm leading-6 text-emerald-900">{submitted.message}</p>
          <p className="mt-2 text-xs uppercase tracking-[0.16em] text-emerald-700">Campaign ref: {submitted.campaignId.slice(0, 8).toUpperCase()}</p>
          <div className="mt-4 flex flex-wrap gap-3">
            <Link to="/" className="button-primary px-5 py-3">Back to homepage</Link>
            <Link to="/packages" className="button-secondary px-5 py-3">View packages</Link>
          </div>
        </div>
      ) : (
        <form
          className="mt-6 space-y-6"
          onSubmit={(event) => {
            event.preventDefault();
            if (step < 3) {
              setStep((current) => (current === 1 ? 2 : 3));
              return;
            }

            if (canSubmit) {
              submitMutation.mutate();
            }
          }}
        >
          <div className="flex flex-wrap gap-2">
            {[
              { id: 1, label: 'About you' },
              { id: 2, label: 'Campaign setup' },
              { id: 3, label: 'Audience details' },
            ].map((item) => (
              <button
                key={item.id}
                type="button"
                onClick={() => setStep(item.id as 1 | 2 | 3)}
                className={`rounded-full border px-4 py-2 text-sm font-semibold transition ${
                  step === item.id ? 'border-brand bg-brand text-white' : 'border-line bg-white text-ink-soft'
                }`}
              >
                {item.id}. {item.label}
              </button>
            ))}
          </div>

          {step === 1 ? (
            <>
              <div>
                <p className="text-sm font-semibold text-ink">Step 1 of 3</p>
                <p className="mt-1 text-sm leading-6 text-ink-soft">Tell us who you are and which package band you want us to plan around.</p>
              </div>

              <div className="grid gap-4 md:grid-cols-2">
                <label className="block">
                  <span className="label-base">Full name</span>
                  <input value={form.fullName} onChange={(event) => setForm((current) => ({ ...current, fullName: event.target.value }))} className="input-base" />
                </label>
                <label className="block">
                  <span className="label-base">Email</span>
                  <input value={form.email} onChange={(event) => setForm((current) => ({ ...current, email: event.target.value }))} className="input-base" type="email" />
                </label>
                <label className="block">
                  <span className="label-base">Phone</span>
                  <input value={form.phone} onChange={(event) => setForm((current) => ({ ...current, phone: event.target.value }))} className="input-base" />
                </label>
                <label className="block">
                  <span className="label-base">Business name</span>
                  <input value={form.businessName} onChange={(event) => setForm((current) => ({ ...current, businessName: event.target.value }))} className="input-base" />
                </label>
                <label className="block">
                  <span className="label-base">Industry</span>
                  <input value={form.industry} onChange={(event) => setForm((current) => ({ ...current, industry: event.target.value }))} className="input-base" placeholder="Retail, property, legal..." />
                </label>
                <label className="block">
                  <span className="label-base">Package band</span>
                  <select
                    value={form.packageBandId}
                    onChange={(event) => setForm((current) => ({ ...current, packageBandId: event.target.value }))}
                    className="input-base"
                    disabled={packagesQuery.isPending || packagesQuery.isError}
                  >
                    <option value="">
                      {packagesQuery.isPending
                        ? 'Loading package options...'
                        : packagesQuery.isError
                          ? 'Package options unavailable'
                          : 'Select package band'}
                    </option>
                    {(packagesQuery.data ?? []).map((item) => (
                      <option key={item.id} value={item.id}>{item.name} | R {item.minBudget.toLocaleString()} - R {item.maxBudget.toLocaleString()}</option>
                    ))}
                  </select>
                </label>
                <label className="block md:col-span-2">
                  <span className="label-base">Campaign name</span>
                  <input value={form.campaignName} onChange={(event) => setForm((current) => ({ ...current, campaignName: event.target.value }))} className="input-base" placeholder="Optional campaign name" />
                </label>
              </div>
            </>
          ) : null}

          {step === 2 ? (
            <>
              <div>
                <p className="text-sm font-semibold text-ink">Step 2 of 3</p>
                <p className="mt-1 text-sm leading-6 text-ink-soft">Set the campaign basics so the brief starts in the right direction.</p>
              </div>

              <div className="grid gap-4 md:grid-cols-2">
                <label className="block">
                  <span className="label-base">Primary goal</span>
                  <select value={form.objective} onChange={(event) => setForm((current) => ({ ...current, objective: event.target.value }))} className="input-base">
                    {OBJECTIVES.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                  </select>
                </label>
                <label className="block">
                  <span className="label-base">Geography scope</span>
                  <select value={form.geographyScope} onChange={(event) => setForm((current) => ({ ...current, geographyScope: event.target.value }))} className="input-base">
                    {GEOGRAPHIES.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                  </select>
                </label>
                <label className="block">
                  <span className="label-base">Primary area</span>
                  <input value={form.primaryArea} onChange={(event) => setForm((current) => ({ ...current, primaryArea: event.target.value }))} className="input-base" placeholder={form.geographyScope === 'local' ? 'City or suburb' : form.geographyScope === 'provincial' ? 'Province' : 'Optional'} />
                </label>
                <label className="block">
                  <span className="label-base">Target language</span>
                  <input value={form.language} onChange={(event) => setForm((current) => ({ ...current, language: event.target.value }))} className="input-base" placeholder="English, isiZulu..." />
                </label>
                <label className="block">
                  <span className="label-base">Target age range</span>
                  <select value={form.ageRange} onChange={(event) => setForm((current) => ({ ...current, ageRange: event.target.value }))} className="input-base">
                    {AGE_RANGES.map((option) => <option key={option.value || 'unset'} value={option.value}>{option.label}</option>)}
                  </select>
                </label>
                <label className="block">
                  <span className="label-base">Target gender</span>
                  <select value={form.gender} onChange={(event) => setForm((current) => ({ ...current, gender: event.target.value }))} className="input-base">
                    <option value="">Prefer not to specify</option>
                    <option value="female">Female</option>
                    <option value="male">Male</option>
                  </select>
                </label>
              </div>

              <div>
                <span className="label-base">Preferred channels</span>
                <div className="mt-3 flex flex-wrap gap-3">
                  {CHANNELS.map((channel) => {
                    const checked = form.preferredMediaTypes.includes(channel.value);
                    return (
                      <label key={channel.value} className={`flex items-center gap-3 rounded-2xl border px-4 py-3 text-sm shadow-sm transition ${checked ? 'border-brand/25 bg-brand-soft/50 text-ink' : 'border-slate-200 bg-white text-ink-soft'}`}>
                        <input
                          type="checkbox"
                          checked={checked}
                          onChange={() => toggleChannel(channel.value)}
                          className="size-4 rounded border-slate-300 accent-brand"
                        />
                        <span>{channel.label}</span>
                      </label>
                    );
                  })}
                </div>
              </div>
            </>
          ) : null}

          {step === 3 ? (
            <>
              <div>
                <p className="text-sm font-semibold text-ink">Step 3 of 3</p>
                <p className="mt-1 text-sm leading-6 text-ink-soft">Add the context that helps us tailor recommendations to your business.</p>
              </div>

              <div className="grid gap-4 md:grid-cols-2">
                <label className="block">
                  <span className="label-base">Current customers</span>
                  <textarea value={form.currentCustomers} onChange={(event) => setForm((current) => ({ ...current, currentCustomers: event.target.value }))} rows={4} className="input-base min-h-[120px] resize-y" placeholder="Who currently buys from you?" />
                </label>
                <label className="block">
                  <span className="label-base">Target audience</span>
                  <textarea value={form.targetAudienceNotes} onChange={(event) => setForm((current) => ({ ...current, targetAudienceNotes: event.target.value }))} rows={4} className="input-base min-h-[120px] resize-y" placeholder="Who do you want to reach?" />
                </label>
                <label className="block">
                  <span className="label-base">Value proposition</span>
                  <textarea value={form.valueProposition} onChange={(event) => setForm((current) => ({ ...current, valueProposition: event.target.value }))} rows={4} className="input-base min-h-[120px] resize-y" placeholder="What makes your offer compelling?" />
                </label>
                <label className="block">
                  <span className="label-base">Growth target</span>
                  <textarea value={form.growthGoal} onChange={(event) => setForm((current) => ({ ...current, growthGoal: event.target.value }))} rows={4} className="input-base min-h-[120px] resize-y" placeholder="What outcome do you want next?" />
                </label>
              </div>

              <label className="block">
                <span className="label-base">Anything the agent should know?</span>
                <textarea value={form.specialRequirements} onChange={(event) => setForm((current) => ({ ...current, specialRequirements: event.target.value }))} rows={5} className="input-base min-h-[150px] resize-y" placeholder="Add urgency, constraints, buying behaviour, or any context that should shape the recommendation." />
              </label>
            </>
          ) : null}

          <div className="flex flex-wrap items-center gap-3">
            {step > 1 ? (
              <button type="button" onClick={() => setStep((current) => (current === 3 ? 2 : 1))} className="button-secondary px-6 py-3">
                Back
              </button>
            ) : null}
            <button
              type="submit"
              disabled={
                submitMutation.isPending
                || (step === 1 && !canContinueStep1)
                || (step === 2 && !canContinueStep2)
                || (step === 3 && !canSubmit)
              }
              className="button-primary inline-flex items-center gap-2 px-6 py-3 disabled:cursor-not-allowed disabled:opacity-60"
            >
              <ClipboardList className="size-4" />
              {submitMutation.isPending ? 'Submitting...' : step === 3 ? 'Submit questionnaire' : 'Continue'}
            </button>
            {variant === 'hero' ? (
              <Link to="/start-campaign" className="button-secondary px-6 py-3">Open full page</Link>
            ) : (
              <Link to="/" className="button-secondary px-6 py-3">Back home</Link>
            )}
          </div>
        </form>
      )}
    </div>
  );
}
