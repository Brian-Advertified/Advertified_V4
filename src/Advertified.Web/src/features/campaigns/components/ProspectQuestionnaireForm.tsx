import { useMutation, useQuery } from '@tanstack/react-query';
import { ClipboardList } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { z } from 'zod';
import { useToast } from '../../../components/ui/toast';
import {
  createDefaultQuestionnaireBriefFields,
  parseAgeRange,
  type QuestionnaireBriefFields,
} from '../briefModel';
import { catalogQueryOptions } from '../../../lib/catalogQueryOptions';
import { useSharedFormOptions } from '../../../lib/useSharedFormOptions';
import { advertifiedApi } from '../../../services/advertifiedApi';

type QuestionnaireForm = QuestionnaireBriefFields & {
  fullName: string;
  email: string;
  phone: string;
  businessName: string;
  industry: string;
  packageBandId: string;
  campaignName: string;
  primaryArea: string;
  ageRange: string;
  language: string;
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

const CITIES = [
  'Johannesburg',
  'Cape Town',
  'Durban',
  'Pretoria',
  'Sandton',
  'Midrand',
  'Centurion',
  'Bloemfontein',
  'Port Elizabeth',
  'East London',
  'Polokwane',
  'Nelspruit',
  'Rustenburg',
  'Kimberley',
  'Pietermaritzburg',
  'Other',
] as const;

const LANGUAGES = [
  'English',
  'isiZulu',
  'isiXhosa',
  'Afrikaans',
  'Sesotho',
  'Setswana',
  'Sepedi',
  'Xitsonga',
  'Tshivenda',
  'Siswati',
  'isiNdebele',
  'Multilingual',
] as const;

type ProspectQuestionnaireFormProps = {
  variant?: 'hero' | 'page';
};

const QUESTIONNAIRE_DRAFT_STORAGE_KEY = 'advertified.prospect-questionnaire.draft';

const questionnaireSchema = z.object({
  fullName: z.string().trim().min(1, 'Full name is required.'),
  email: z.string().trim().min(1, 'Email is required.').email('Enter a valid email address.'),
  phone: z.string().trim().min(1, 'Phone is required.'),
  businessName: z.string(),
  industry: z.string(),
  businessStage: z.string(),
  monthlyRevenueBand: z.string(),
  salesModel: z.string(),
  packageBandId: z.string().trim().min(1, 'Package band is required.'),
  campaignName: z.string(),
  objective: z.string().trim().min(1, 'Primary goal is required.'),
  geographyScope: z.enum(['local', 'provincial', 'national'], { message: 'Geography scope is required.' }),
  primaryArea: z.string(),
  ageRange: z.string(),
  targetGender: z.string(),
  language: z.string(),
  preferredMediaTypes: z.array(z.string()).min(1, 'Select at least one preferred channel.'),
  customerType: z.string(),
  buyingBehaviour: z.string(),
  decisionCycle: z.string(),
  pricePositioning: z.string(),
  averageCustomerSpendBand: z.string(),
  growthTarget: z.string(),
  urgencyLevel: z.string(),
  audienceClarity: z.string(),
  valuePropositionFocus: z.string(),
  specialRequirements: z.string(),
});

type QuestionnaireErrors = Partial<Record<keyof QuestionnaireForm | 'preferredMediaTypes', string>>;

function validateQuestionnaireStep(step: 1 | 2 | 3, values: QuestionnaireForm): QuestionnaireErrors {
  const result = questionnaireSchema.safeParse(values);
  const allErrors: QuestionnaireErrors = {};

  if (!result.success) {
    for (const issue of result.error.issues) {
      const path = issue.path[0];
      if (typeof path === 'string' && !allErrors[path as keyof QuestionnaireErrors]) {
        allErrors[path as keyof QuestionnaireErrors] = issue.message;
      }
    }
  }

  if (values.geographyScope !== 'national' && !values.primaryArea.trim()) {
    allErrors.primaryArea = values.geographyScope === 'local'
      ? 'Select a city.'
      : 'Select a province.';
  }

  const stepFields: Record<1 | 2 | 3, (keyof QuestionnaireErrors)[]> = {
    1: ['fullName', 'email', 'phone', 'packageBandId'],
    2: ['objective', 'geographyScope', 'primaryArea', 'preferredMediaTypes'],
    3: [],
  };

  return stepFields[step].reduce<QuestionnaireErrors>((acc, field) => {
    if (allErrors[field]) {
      acc[field] = allErrors[field];
    }

    return acc;
  }, {});
}

function FieldError({ message }: { message?: string }) {
  if (!message) {
    return null;
  }

  return <p className="mt-2 text-sm text-rose-700">{message}</p>;
}

function readStoredQuestionnaireDraft(): QuestionnaireForm | null {
  if (typeof window === 'undefined') {
    return null;
  }

  try {
    const raw = window.localStorage.getItem(QUESTIONNAIRE_DRAFT_STORAGE_KEY);
    if (!raw) {
      return null;
    }

    const parsed = JSON.parse(raw) as Partial<QuestionnaireForm>;
    const defaultBrief = createDefaultQuestionnaireBriefFields();
    return {
      fullName: parsed.fullName ?? '',
      email: parsed.email ?? '',
      phone: parsed.phone ?? '',
      businessName: parsed.businessName ?? '',
      industry: parsed.industry ?? '',
      businessStage: parsed.businessStage ?? defaultBrief.businessStage ?? '',
      monthlyRevenueBand: parsed.monthlyRevenueBand ?? defaultBrief.monthlyRevenueBand ?? '',
      salesModel: parsed.salesModel ?? defaultBrief.salesModel ?? '',
      packageBandId: parsed.packageBandId ?? '',
      campaignName: parsed.campaignName ?? '',
      objective: parsed.objective ?? defaultBrief.objective,
      geographyScope: parsed.geographyScope ?? defaultBrief.geographyScope,
      primaryArea: parsed.primaryArea ?? '',
      ageRange: parsed.ageRange ?? '',
      targetGender: parsed.targetGender ?? defaultBrief.targetGender ?? '',
      language: parsed.language ?? '',
      preferredMediaTypes: Array.isArray(parsed.preferredMediaTypes) && parsed.preferredMediaTypes.length > 0
        ? parsed.preferredMediaTypes
        : defaultBrief.preferredMediaTypes,
      customerType: parsed.customerType ?? defaultBrief.customerType ?? '',
      buyingBehaviour: parsed.buyingBehaviour ?? defaultBrief.buyingBehaviour ?? '',
      decisionCycle: parsed.decisionCycle ?? defaultBrief.decisionCycle ?? '',
      pricePositioning: parsed.pricePositioning ?? defaultBrief.pricePositioning ?? '',
      averageCustomerSpendBand: parsed.averageCustomerSpendBand ?? defaultBrief.averageCustomerSpendBand ?? '',
      growthTarget: parsed.growthTarget ?? defaultBrief.growthTarget ?? '',
      urgencyLevel: parsed.urgencyLevel ?? defaultBrief.urgencyLevel ?? '',
      audienceClarity: parsed.audienceClarity ?? defaultBrief.audienceClarity ?? '',
      valuePropositionFocus: parsed.valuePropositionFocus ?? defaultBrief.valuePropositionFocus ?? '',
      specialRequirements: parsed.specialRequirements ?? defaultBrief.specialRequirements ?? '',
    };
  } catch {
    return null;
  }
}

function clearStoredQuestionnaireDraft() {
  if (typeof window !== 'undefined') {
    window.localStorage.removeItem(QUESTIONNAIRE_DRAFT_STORAGE_KEY);
  }
}

export function ProspectQuestionnaireForm({ variant = 'page' }: ProspectQuestionnaireFormProps) {
  const { pushToast } = useToast();
  const formOptionsQuery = useSharedFormOptions();
  const [submitted, setSubmitted] = useState<{ campaignId: string; campaignName: string; message: string } | null>(null);
  const [step, setStep] = useState<1 | 2 | 3>(1);
  const [errors, setErrors] = useState<QuestionnaireErrors>({});
  const defaultBrief = createDefaultQuestionnaireBriefFields();
  const [form, setForm] = useState<QuestionnaireForm>({
    fullName: '',
    email: '',
    phone: '',
    businessName: '',
    industry: '',
    businessStage: defaultBrief.businessStage ?? '',
    monthlyRevenueBand: defaultBrief.monthlyRevenueBand ?? '',
    salesModel: defaultBrief.salesModel ?? '',
    packageBandId: '',
    campaignName: '',
    objective: defaultBrief.objective,
    geographyScope: defaultBrief.geographyScope,
    primaryArea: '',
    ageRange: '',
    targetGender: defaultBrief.targetGender ?? '',
    language: '',
    preferredMediaTypes: defaultBrief.preferredMediaTypes,
    customerType: defaultBrief.customerType ?? '',
    buyingBehaviour: defaultBrief.buyingBehaviour ?? '',
    decisionCycle: defaultBrief.decisionCycle ?? '',
    pricePositioning: defaultBrief.pricePositioning ?? '',
    averageCustomerSpendBand: defaultBrief.averageCustomerSpendBand ?? '',
    growthTarget: defaultBrief.growthTarget ?? '',
    urgencyLevel: defaultBrief.urgencyLevel ?? '',
    audienceClarity: defaultBrief.audienceClarity ?? '',
    valuePropositionFocus: defaultBrief.valuePropositionFocus ?? '',
    specialRequirements: defaultBrief.specialRequirements ?? '',
  });
  const [draftRestored, setDraftRestored] = useState(false);

  useEffect(() => {
    const storedDraft = readStoredQuestionnaireDraft();
    if (storedDraft) {
      setForm(storedDraft);
      setDraftRestored(true);
    }
  }, []);

  useEffect(() => {
    if (submitted || typeof window === 'undefined') {
      return;
    }

    window.localStorage.setItem(QUESTIONNAIRE_DRAFT_STORAGE_KEY, JSON.stringify(form));
  }, [form, submitted]);

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
          businessStage: form.businessStage || undefined,
          monthlyRevenueBand: form.monthlyRevenueBand || undefined,
          salesModel: form.salesModel || undefined,
          geographyScope: form.geographyScope,
          provinces: form.geographyScope === 'provincial' && areaValue ? [areaValue] : undefined,
          cities: form.geographyScope === 'local' && areaValue ? [areaValue] : undefined,
          targetAgeMin: ageRange.min,
          targetAgeMax: ageRange.max,
          targetGender: form.targetGender || undefined,
          targetLanguages: form.language.trim() ? [form.language.trim()] : undefined,
          customerType: form.customerType || undefined,
          buyingBehaviour: form.buyingBehaviour || undefined,
          decisionCycle: form.decisionCycle || undefined,
          pricePositioning: form.pricePositioning || undefined,
          averageCustomerSpendBand: form.averageCustomerSpendBand || undefined,
          growthTarget: form.growthTarget || undefined,
          urgencyLevel: form.urgencyLevel || undefined,
          audienceClarity: form.audienceClarity || undefined,
          valuePropositionFocus: form.valuePropositionFocus || undefined,
          preferredMediaTypes: form.preferredMediaTypes,
          openToUpsell: false,
          specialRequirements: form.specialRequirements.trim() || undefined,
        },
      });
    },
    onSuccess: (result) => {
      clearStoredQuestionnaireDraft();
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
  const {
    audienceClarity,
    averageCustomerSpendBands,
    businessStages,
    buyingBehaviours,
    customerTypes,
    decisionCycles,
    growthTargets,
    industries,
    monthlyRevenueBands,
    pricePositioning,
    provinces,
    salesModels,
    urgencyLevels,
    valuePropositionFocus,
  } = formOptionsQuery.data ?? {
    audienceClarity: [],
    averageCustomerSpendBands: [],
    businessStages: [],
    buyingBehaviours: [],
    customerTypes: [],
    decisionCycles: [],
    growthTargets: [],
    industries: [],
    monthlyRevenueBands: [],
    pricePositioning: [],
    provinces: [],
    salesModels: [],
    urgencyLevels: [],
    valuePropositionFocus: [],
  };

  const containerClassName = variant === 'hero'
    ? 'hero-glass-card rounded-[28px] p-5 text-ink sm:p-6'
    : 'panel px-6 py-8 sm:px-8';
  const titleClassName = variant === 'hero'
    ? 'mt-3 text-2xl font-semibold tracking-tight text-ink'
    : 'mt-4 text-3xl font-semibold tracking-tight text-ink sm:text-4xl';
  const copyClassName = variant === 'hero'
    ? 'mt-3 text-sm leading-6 text-ink-soft'
    : 'mt-4 max-w-3xl text-sm leading-7 text-ink-soft sm:text-[15px]';

  if (formOptionsQuery.isPending) {
    return <div className={containerClassName}>Loading questionnaire options...</div>;
  }

  if (formOptionsQuery.isError || !formOptionsQuery.data) {
    return <div className={containerClassName}>We could not load questionnaire options right now. Please refresh and try again.</div>;
  }

  const stepErrors = validateQuestionnaireStep(step, form);
  const canAdvanceFromStep = Object.keys(stepErrors).length === 0;

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
          <p className="text-sm font-semibold text-emerald-800">Your brief is in</p>
          <p className="mt-2 text-sm leading-6 text-emerald-900">
            We’ve saved your answers and queued them for review. An Advertified agent can now turn this into a recommendation.
          </p>
          <div className="mt-4 rounded-[20px] border border-emerald-200 bg-white/70 px-4 py-4">
            <p className="text-xs font-semibold uppercase tracking-[0.16em] text-emerald-700">What happens next</p>
            <ul className="mt-3 space-y-2 text-sm leading-6 text-emerald-900">
              <li>1. Your answers are reviewed and shaped into a working campaign brief.</li>
              <li>2. We use that brief to build tailored media recommendations for your business.</li>
              <li>3. If you already know your spend band, you can continue with package selection right away.</li>
            </ul>
          </div>
          <p className="mt-4 text-sm leading-6 text-emerald-900">{submitted.message}</p>
          <p className="mt-2 text-xs uppercase tracking-[0.16em] text-emerald-700">Campaign ref: {submitted.campaignId.slice(0, 8).toUpperCase()}</p>
          <div className="mt-4 flex flex-wrap gap-3">
            <Link to="/packages" className="button-primary px-5 py-3">Continue to packages</Link>
            <Link to="/" className="button-secondary px-5 py-3">Back to homepage</Link>
          </div>
        </div>
      ) : (
        <form
          className="mt-6 space-y-6"
          onSubmit={(event) => {
            event.preventDefault();
            const nextErrors = validateQuestionnaireStep(step, form);
            setErrors(nextErrors);

            if (Object.keys(nextErrors).length > 0) {
              pushToast({
                title: 'Some details are still missing.',
                description: 'Check the highlighted fields and try again.',
              }, 'error');
              return;
            }

            if (step < 3) {
              setStep((current) => (current === 1 ? 2 : 3));
              return;
            }

            submitMutation.mutate();
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

          {Object.keys(errors).length > 0 ? (
            <div className="rounded-[20px] border border-rose-200 bg-rose-50 px-4 py-4 text-sm text-rose-800">
              Please complete the highlighted fields before continuing.
            </div>
          ) : null}

          {draftRestored ? (
            <div className="rounded-[20px] border border-brand/15 bg-brand/[0.06] px-4 py-4 text-sm text-ink-soft">
              We restored your saved questionnaire so you can continue where you left off.
            </div>
          ) : null}

          {step === 1 ? (
            <>
              <div>
                <p className="text-sm font-semibold text-ink">Step 1 of 3</p>
                <p className="mt-1 text-sm leading-6 text-ink-soft">Tell us a little about your business. If you are unsure about any question, choose the closest option and we will guide the rest.</p>
              </div>

              <div className="grid gap-4 md:grid-cols-2">
                <label className="block">
                  <span className="label-base">Full name</span>
                  <input value={form.fullName} onChange={(event) => setForm((current) => ({ ...current, fullName: event.target.value }))} className="input-base" />
                  <FieldError message={errors.fullName} />
                </label>
                <label className="block">
                  <span className="label-base">Email</span>
                  <input value={form.email} onChange={(event) => setForm((current) => ({ ...current, email: event.target.value }))} className="input-base" type="email" />
                  <FieldError message={errors.email} />
                </label>
                <label className="block">
                  <span className="label-base">Phone</span>
                  <input value={form.phone} onChange={(event) => setForm((current) => ({ ...current, phone: event.target.value }))} className="input-base" />
                  <FieldError message={errors.phone} />
                </label>
                <label className="block">
                  <span className="label-base">Business name</span>
                  <input value={form.businessName} onChange={(event) => setForm((current) => ({ ...current, businessName: event.target.value }))} className="input-base" placeholder="Your company or trading name" />
                </label>
                <label className="block">
                  <span className="label-base">Industry</span>
                  <select value={form.industry} onChange={(event) => setForm((current) => ({ ...current, industry: event.target.value }))} className="input-base">
                    <option value="">Select industry</option>
                    {industries.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
                  </select>
                  <p className="mt-2 text-xs text-ink-soft">Choose the industry that best matches what your business sells or does.</p>
                </label>
                <label className="block">
                  <span className="label-base">Where is your business right now?</span>
                  <select value={form.businessStage} onChange={(event) => setForm((current) => ({ ...current, businessStage: event.target.value }))} className="input-base">
                    <option value="">Select business stage</option>
                    {businessStages.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                  </select>
                  <p className="mt-2 text-xs text-ink-soft">For example: newly launched, growing steadily, or already well established.</p>
                </label>
                <label className="block">
                  <span className="label-base">About how much does the business make each month?</span>
                  <select value={form.monthlyRevenueBand} onChange={(event) => setForm((current) => ({ ...current, monthlyRevenueBand: event.target.value }))} className="input-base">
                    <option value="">Select monthly revenue</option>
                    {monthlyRevenueBands.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                  </select>
                  <p className="mt-2 text-xs text-ink-soft">An estimate is fine. This helps us recommend a realistic campaign approach.</p>
                </label>
                <label className="block">
                  <span className="label-base">How do you mainly sell?</span>
                  <select value={form.salesModel} onChange={(event) => setForm((current) => ({ ...current, salesModel: event.target.value }))} className="input-base">
                    <option value="">Select sales model</option>
                    {salesModels.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                  </select>
                  <p className="mt-2 text-xs text-ink-soft">For example: online, in-store, by appointment, or through a sales team.</p>
                </label>
                <label className="block">
                  <span className="label-base">Budget range</span>
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
                  <p className="mt-2 text-xs text-ink-soft">Choose the budget range you are most comfortable with. You can refine the spend later.</p>
                  <FieldError message={errors.packageBandId} />
                </label>
                <label className="block md:col-span-2">
                  <span className="label-base">Campaign name</span>
                  <input value={form.campaignName} onChange={(event) => setForm((current) => ({ ...current, campaignName: event.target.value }))} className="input-base" placeholder="Optional, for example Winter Promo or New Store Launch" />
                </label>
              </div>
            </>
          ) : null}

          {step === 2 ? (
            <>
              <div>
                <p className="text-sm font-semibold text-ink">Step 2 of 3</p>
                <p className="mt-1 text-sm leading-6 text-ink-soft">Tell us what you want the advertising to achieve and where you want it to work hardest.</p>
              </div>

              <div className="grid gap-4 md:grid-cols-2">
                <label className="block">
                  <span className="label-base">What do you want this advertising to do?</span>
                  <select value={form.objective} onChange={(event) => setForm((current) => ({ ...current, objective: event.target.value }))} className="input-base">
                    {OBJECTIVES.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                  </select>
                  <p className="mt-2 text-xs text-ink-soft">Choose the main result you want first, like awareness, leads, more visits, or a launch push.</p>
                  <FieldError message={errors.objective} />
                </label>
                <label className="block">
                  <span className="label-base">How wide should the campaign reach?</span>
                  <select value={form.geographyScope} onChange={(event) => setForm((current) => ({ ...current, geographyScope: event.target.value }))} className="input-base">
                    {GEOGRAPHIES.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                  </select>
                  <p className="mt-2 text-xs text-ink-soft">Choose local for one city, provincial for one province, or national for a wider rollout.</p>
                  <FieldError message={errors.geographyScope} />
                </label>
                <label className="block">
                  <span className="label-base">{form.geographyScope === 'local' ? 'Which city matters most?' : 'Which area matters most?'}</span>
                  <select
                    value={form.primaryArea}
                    onChange={(event) => setForm((current) => ({ ...current, primaryArea: event.target.value }))}
                    className="input-base"
                    disabled={form.geographyScope === 'national'}
                  >
                    <option value="">
                      {form.geographyScope === 'national'
                        ? 'Not needed for national'
                        : form.geographyScope === 'local'
                          ? 'Select city'
                          : 'Select province'}
                    </option>
                    {(form.geographyScope === 'local' ? CITIES : provinces.map((item) => item.value)).map((item) => (
                      <option key={item} value={item}>{item}</option>
                    ))}
                  </select>
                  <p className="mt-2 text-xs text-ink-soft">
                    {form.geographyScope === 'national'
                      ? 'You can skip this because the campaign is meant to reach customers across the country.'
                      : form.geographyScope === 'local'
                        ? 'Pick the city where you most want to attract customers.'
                        : 'Pick the province where you most want the campaign to focus.'}
                  </p>
                  <FieldError message={errors.primaryArea} />
                </label>
                <label className="block">
                  <span className="label-base">What language should the message mainly use?</span>
                  <select value={form.language} onChange={(event) => setForm((current) => ({ ...current, language: event.target.value }))} className="input-base">
                    <option value="">Select language</option>
                    {LANGUAGES.map((item) => <option key={item} value={item}>{item}</option>)}
                  </select>
                  <p className="mt-2 text-xs text-ink-soft">Choose the main language your customers are most likely to respond to.</p>
                </label>
                <label className="block">
                  <span className="label-base">What age group are you mainly trying to reach?</span>
                  <select value={form.ageRange} onChange={(event) => setForm((current) => ({ ...current, ageRange: event.target.value }))} className="input-base">
                    {AGE_RANGES.map((option) => <option key={option.value || 'unset'} value={option.value}>{option.label}</option>)}
                  </select>
                  <p className="mt-2 text-xs text-ink-soft">If you are not sure, leave this open and we will keep the plan broader.</p>
                </label>
                <label className="block">
                  <span className="label-base">Is the campaign mainly aimed at men, women, or everyone?</span>
                  <select value={form.targetGender} onChange={(event) => setForm((current) => ({ ...current, targetGender: event.target.value }))} className="input-base">
                    <option value="">Prefer not to specify</option>
                    <option value="all">All</option>
                    <option value="female">Female</option>
                    <option value="male">Male</option>
                    <option value="mixed">Mixed</option>
                  </select>
                  <p className="mt-2 text-xs text-ink-soft">Only choose this if one group clearly matters more for this campaign.</p>
                </label>
              </div>

              <div>
                <span className="label-base">Which advertising channels interest you most?</span>
                <p className="mt-2 text-xs text-ink-soft">Pick the options that feel right for your business. We can still recommend a better mix if needed.</p>
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
                <FieldError message={errors.preferredMediaTypes} />
              </div>
            </>
          ) : null}

          {step === 3 ? (
            <>
              <div>
                <p className="text-sm font-semibold text-ink">Step 3 of 3</p>
                <p className="mt-1 text-sm leading-6 text-ink-soft">A few final questions help us match the campaign to the way your customers usually buy.</p>
              </div>

              <div className="grid gap-4 md:grid-cols-2">
                <label className="block">
                  <span className="label-base">Who usually buys from you?</span>
                  <select value={form.customerType} onChange={(event) => setForm((current) => ({ ...current, customerType: event.target.value }))} className="input-base">
                    <option value="">Select customer type</option>
                    {customerTypes.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                  </select>
                  <p className="mt-2 text-xs text-ink-soft">Choose the type of customer you most often sell to today.</p>
                </label>
                <label className="block">
                  <span className="label-base">What usually makes people choose you?</span>
                  <select value={form.valuePropositionFocus} onChange={(event) => setForm((current) => ({ ...current, valuePropositionFocus: event.target.value }))} className="input-base">
                    <option value="">Select value proposition</option>
                    {valuePropositionFocus.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                  </select>
                  <p className="mt-2 text-xs text-ink-soft">For example: price, quality, convenience, speed, trust, or exclusivity.</p>
                </label>
                <label className="block">
                  <span className="label-base">How do customers usually decide to buy?</span>
                  <select value={form.buyingBehaviour} onChange={(event) => setForm((current) => ({ ...current, buyingBehaviour: event.target.value }))} className="input-base">
                    <option value="">Select buying behaviour</option>
                    {buyingBehaviours.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                  </select>
                  <p className="mt-2 text-xs text-ink-soft">Think about whether people buy quickly, compare options first, or need more trust before deciding.</p>
                </label>
                <label className="block">
                  <span className="label-base">How long does it usually take someone to buy?</span>
                  <select value={form.decisionCycle} onChange={(event) => setForm((current) => ({ ...current, decisionCycle: event.target.value }))} className="input-base">
                    <option value="">Select decision cycle</option>
                    {decisionCycles.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                  </select>
                  <p className="mt-2 text-xs text-ink-soft">Some purchases happen the same day, while others take days or weeks.</p>
                </label>
                <label className="block">
                  <span className="label-base">How is your offer priced in the market?</span>
                  <select value={form.pricePositioning} onChange={(event) => setForm((current) => ({ ...current, pricePositioning: event.target.value }))} className="input-base">
                    <option value="">Select price positioning</option>
                    {pricePositioning.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                  </select>
                  <p className="mt-2 text-xs text-ink-soft">Choose whether you compete more on affordability, the middle of the market, or premium value.</p>
                </label>
                <label className="block">
                  <span className="label-base">How much does a typical customer spend with you?</span>
                  <select value={form.averageCustomerSpendBand} onChange={(event) => setForm((current) => ({ ...current, averageCustomerSpendBand: event.target.value }))} className="input-base">
                    <option value="">Select average spend</option>
                    {averageCustomerSpendBands.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                  </select>
                  <p className="mt-2 text-xs text-ink-soft">A rough average is enough. This helps us understand the scale of sale you are aiming for.</p>
                </label>
                <label className="block">
                  <span className="label-base">What kind of growth are you aiming for?</span>
                  <select value={form.growthTarget} onChange={(event) => setForm((current) => ({ ...current, growthTarget: event.target.value }))} className="input-base">
                    <option value="">Select growth target</option>
                    {growthTargets.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                  </select>
                  <p className="mt-2 text-xs text-ink-soft">For example: more leads, more sales, more foot traffic, or a stronger market presence.</p>
                </label>
                <label className="block">
                  <span className="label-base">How soon do you need this campaign to start helping?</span>
                  <select value={form.urgencyLevel} onChange={(event) => setForm((current) => ({ ...current, urgencyLevel: event.target.value }))} className="input-base">
                    <option value="">Select urgency</option>
                    {urgencyLevels.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                  </select>
                  <p className="mt-2 text-xs text-ink-soft">Choose higher urgency if you are promoting something time-sensitive or need results soon.</p>
                </label>
                <label className="block">
                  <span className="label-base">How clearly can you describe your ideal customer?</span>
                  <select value={form.audienceClarity} onChange={(event) => setForm((current) => ({ ...current, audienceClarity: event.target.value }))} className="input-base">
                    <option value="">Select audience clarity</option>
                    {audienceClarity.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                  </select>
                  <p className="mt-2 text-xs text-ink-soft">If you are still figuring that out, that is okay. This helps us know how broad or focused to make the plan.</p>
                </label>
              </div>

              <label className="block">
                <span className="label-base">Anything the agent should know?</span>
                <textarea value={form.specialRequirements} onChange={(event) => setForm((current) => ({ ...current, specialRequirements: event.target.value }))} rows={5} className="input-base min-h-[150px] resize-y" placeholder="Optional. Add anything important, like launch dates, areas to avoid, seasonal timing, or special campaign needs." />
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
                || !canAdvanceFromStep
              }
              className="button-primary inline-flex items-center gap-2 px-6 py-3 disabled:cursor-not-allowed disabled:opacity-60"
            >
              <ClipboardList className="size-4" />
              {submitMutation.isPending ? 'Submitting...' : step === 3 ? 'Submit questionnaire' : 'Continue'}
            </button>
            {variant === 'hero' ? (
              <Link to="/start-campaign" className="button-secondary px-6 py-3">Open full page</Link>
            ) : (
              <button
                type="button"
                onClick={() => {
                  clearStoredQuestionnaireDraft();
                  setDraftRestored(false);
                  setErrors({});
                  setStep(1);
                  setForm({
                    fullName: '',
                    email: '',
                    phone: '',
                    businessName: '',
                    industry: '',
                    businessStage: defaultBrief.businessStage ?? '',
                    monthlyRevenueBand: defaultBrief.monthlyRevenueBand ?? '',
                    salesModel: defaultBrief.salesModel ?? '',
                    packageBandId: '',
                    campaignName: '',
                    objective: defaultBrief.objective,
                    geographyScope: defaultBrief.geographyScope,
                    primaryArea: '',
                    ageRange: '',
                    targetGender: defaultBrief.targetGender ?? '',
                    language: '',
                    preferredMediaTypes: defaultBrief.preferredMediaTypes,
                    customerType: defaultBrief.customerType ?? '',
                    buyingBehaviour: defaultBrief.buyingBehaviour ?? '',
                    decisionCycle: defaultBrief.decisionCycle ?? '',
                    pricePositioning: defaultBrief.pricePositioning ?? '',
                    averageCustomerSpendBand: defaultBrief.averageCustomerSpendBand ?? '',
                    growthTarget: defaultBrief.growthTarget ?? '',
                    urgencyLevel: defaultBrief.urgencyLevel ?? '',
                    audienceClarity: defaultBrief.audienceClarity ?? '',
                    valuePropositionFocus: defaultBrief.valuePropositionFocus ?? '',
                    specialRequirements: defaultBrief.specialRequirements ?? '',
                  });
                }}
                className="button-secondary px-6 py-3"
              >
                Clear saved draft
              </button>
            )}
          </div>
        </form>
      )}
    </div>
  );
}
