import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import type { ReactNode } from 'react';
import {
  joinCommaList,
  optionalNumber,
  splitCommaList,
} from '../briefModel';
import type { CampaignBrief } from '../../../types/domain';
import { useSharedFormOptions } from '../../../lib/useSharedFormOptions';

const objectiveOptions = [
  { value: 'awareness', label: 'Brand awareness' },
  { value: 'leads', label: 'Lead generation' },
  { value: 'foot_traffic', label: 'Foot traffic' },
  { value: 'promotion', label: 'Promotion' },
  { value: 'launch', label: 'Product launch' },
  { value: 'brand_presence', label: 'Brand presence' },
] as const;

const geographyScopeOptions = [
  { value: 'local', label: 'Local' },
  { value: 'provincial', label: 'Provincial' },
  { value: 'national', label: 'National' },
] as const;

const videoAspectRatioOptions = [
  { value: '', label: 'Select video aspect ratio' },
  { value: '16:9', label: '16:9 (landscape)' },
  { value: '9:16', label: '9:16 (portrait)' },
  { value: '1:1', label: '1:1 (square)' },
  { value: '4:5', label: '4:5 (feed)' },
] as const;

const videoDurationOptions = [
  { value: '', label: 'Select video duration' },
  { value: '6', label: '6 seconds' },
  { value: '10', label: '10 seconds' },
  { value: '15', label: '15 seconds' },
  { value: '30', label: '30 seconds' },
  { value: '45', label: '45 seconds' },
  { value: '60', label: '60 seconds' },
] as const;

const ageBandOptions = [
  { value: '', label: 'Select age band' },
  { value: '18-24', label: '18-24' },
  { value: '25-34', label: '25-34' },
  { value: '35-44', label: '35-44' },
  { value: '45-54', label: '45-54' },
  { value: '55-100', label: '55+' },
] as const;

const lsmBandOptions = [
  { value: '', label: 'Select LSM band' },
  { value: '1-4', label: 'LSM 1-4 (entry)' },
  { value: '5-7', label: 'LSM 5-7 (mass middle)' },
  { value: '7-10', label: 'LSM 7-10 (upper middle)' },
  { value: '9-10', label: 'LSM 9-10 (premium)' },
] as const;

const briefSchema = z
  .object({
    objective: z.string().min(1, 'Objective is required.'),
    businessStage: z.string().optional(),
    monthlyRevenueBand: z.string().optional(),
    salesModel: z.string().optional(),
    geographyScope: z.string().min(1, 'Geography scope is required.'),
    startDate: z.string().optional(),
    endDate: z.string().optional(),
    durationWeeks: z.string().optional(),
    provinces: z.string().optional(),
    cities: z.string().optional(),
    suburbs: z.string().optional(),
    areas: z.string().optional(),
    targetAgeMin: z.string().optional(),
    targetAgeMax: z.string().optional(),
    targetGender: z.string().optional(),
    targetLanguages: z.string().optional(),
    targetLsmMin: z.string().optional(),
    targetLsmMax: z.string().optional(),
    targetInterests: z.string().optional(),
    targetAudienceNotes: z.string().optional(),
    customerType: z.string().optional(),
    buyingBehaviour: z.string().optional(),
    decisionCycle: z.string().optional(),
    pricePositioning: z.string().optional(),
    averageCustomerSpendBand: z.string().optional(),
    growthTarget: z.string().optional(),
    urgencyLevel: z.string().optional(),
    audienceClarity: z.string().optional(),
    valuePropositionFocus: z.string().optional(),
    preferredMediaTypes: z.string().optional(),
    excludedMediaTypes: z.string().optional(),
    mustHaveAreas: z.string().optional(),
    excludedAreas: z.string().optional(),
    creativeReady: z.boolean().optional(),
    creativeNotes: z.string().optional(),
    maxMediaItems: z.string().optional(),
    openToUpsell: z.boolean(),
    additionalBudget: z.string().optional(),
    specialRequirements: z.string().optional(),
    preferredVideoAspectRatio: z.string().optional(),
    preferredVideoDurationSeconds: z.string().optional(),
    targetAgeBand: z.string().optional(),
    targetLsmBand: z.string().optional(),
  })
  .refine((value) => !value.startDate || !value.endDate || new Date(value.endDate) >= new Date(value.startDate), {
    message: 'Campaign end date must be after the start date.',
    path: ['endDate'],
  });

type FormShape = z.infer<typeof briefSchema>;

export function CampaignBriefForm({
  initialValue,
  loading,
  onSave,
  onSubmitBrief,
}: {
  initialValue?: CampaignBrief;
  loading: boolean;
  onSave: (brief: CampaignBrief) => Promise<void>;
  onSubmitBrief: (brief: CampaignBrief) => Promise<void>;
}) {
  const {
    register,
    handleSubmit,
    setValue,
    watch,
    formState: { errors },
  } = useForm<FormShape>({
    resolver: zodResolver(briefSchema),
    defaultValues: {
      objective: initialValue?.objective ?? '',
      businessStage: initialValue?.businessStage ?? '',
      monthlyRevenueBand: initialValue?.monthlyRevenueBand ?? '',
      salesModel: initialValue?.salesModel ?? '',
      geographyScope: initialValue?.geographyScope ?? '',
      startDate: initialValue?.startDate,
      endDate: initialValue?.endDate,
      durationWeeks: initialValue?.durationWeeks?.toString(),
      provinces: joinCommaList(initialValue?.provinces),
      cities: joinCommaList(initialValue?.cities),
      suburbs: joinCommaList(initialValue?.suburbs),
      areas: joinCommaList(initialValue?.areas),
      targetAgeMin: initialValue?.targetAgeMin?.toString(),
      targetAgeMax: initialValue?.targetAgeMax?.toString(),
      targetGender: initialValue?.targetGender,
      targetLanguages: joinCommaList(initialValue?.targetLanguages),
      targetLsmMin: initialValue?.targetLsmMin?.toString(),
      targetLsmMax: initialValue?.targetLsmMax?.toString(),
      targetInterests: joinCommaList(initialValue?.targetInterests),
      targetAudienceNotes: initialValue?.targetAudienceNotes,
      customerType: initialValue?.customerType ?? '',
      buyingBehaviour: initialValue?.buyingBehaviour ?? '',
      decisionCycle: initialValue?.decisionCycle ?? '',
      pricePositioning: initialValue?.pricePositioning ?? '',
      averageCustomerSpendBand: initialValue?.averageCustomerSpendBand ?? '',
      growthTarget: initialValue?.growthTarget ?? '',
      urgencyLevel: initialValue?.urgencyLevel ?? '',
      audienceClarity: initialValue?.audienceClarity ?? '',
      valuePropositionFocus: initialValue?.valuePropositionFocus ?? '',
      preferredMediaTypes: joinCommaList(initialValue?.preferredMediaTypes),
      excludedMediaTypes: joinCommaList(initialValue?.excludedMediaTypes),
      mustHaveAreas: joinCommaList(initialValue?.mustHaveAreas),
      excludedAreas: joinCommaList(initialValue?.excludedAreas),
      creativeReady: initialValue?.creativeReady,
      creativeNotes: initialValue?.creativeNotes,
      maxMediaItems: initialValue?.maxMediaItems?.toString(),
      openToUpsell: initialValue?.openToUpsell ?? true,
      additionalBudget: initialValue?.additionalBudget?.toString(),
      specialRequirements: initialValue?.specialRequirements,
      preferredVideoAspectRatio: initialValue?.preferredVideoAspectRatio,
      preferredVideoDurationSeconds: initialValue?.preferredVideoDurationSeconds?.toString(),
      targetAgeBand: '',
      targetLsmBand: '',
    },
  });
  const formOptionsQuery = useSharedFormOptions();
  const geographyScope = watch('geographyScope');

  if (formOptionsQuery.isPending) {
    return <div className="panel px-6 py-6">Loading form options...</div>;
  }

  if (formOptionsQuery.isError || !formOptionsQuery.data) {
    return <div className="panel px-6 py-6">We could not load brief options right now. Refresh and try again.</div>;
  }

  const {
    audienceClarity,
    averageCustomerSpendBands,
    businessStages,
    buyingBehaviours,
    customerTypes,
    decisionCycles,
    growthTargets,
    monthlyRevenueBands,
    pricePositioning,
    salesModels,
    urgencyLevels,
    valuePropositionFocus,
  } = formOptionsQuery.data;

  function setAgeBand(value: string) {
    if (!value) {
      return;
    }

    const [min, max] = value.split('-');
    setValue('targetAgeMin', min);
    setValue('targetAgeMax', max);
  }

  function setLsmBand(value: string) {
    if (!value) {
      return;
    }

    const [min, max] = value.split('-');
    setValue('targetLsmMin', min);
    setValue('targetLsmMax', max);
  }

  function map(values: FormShape): CampaignBrief {
    return {
      objective: values.objective,
      businessStage: values.businessStage || undefined,
      monthlyRevenueBand: values.monthlyRevenueBand || undefined,
      salesModel: values.salesModel || undefined,
      geographyScope: values.geographyScope,
      startDate: values.startDate,
      endDate: values.endDate,
      durationWeeks: optionalNumber(values.durationWeeks),
      provinces: splitCommaList(values.provinces),
      cities: splitCommaList(values.cities),
      suburbs: splitCommaList(values.suburbs),
      areas: splitCommaList(values.areas),
      targetLocationLabel: initialValue?.targetLocationLabel,
      targetLocationCity: initialValue?.targetLocationCity,
      targetLocationProvince: initialValue?.targetLocationProvince,
      targetLatitude: initialValue?.targetLatitude,
      targetLongitude: initialValue?.targetLongitude,
      targetAgeMin: optionalNumber(values.targetAgeMin),
      targetAgeMax: optionalNumber(values.targetAgeMax),
      targetGender: values.targetGender,
      targetLanguages: splitCommaList(values.targetLanguages),
      targetLsmMin: optionalNumber(values.targetLsmMin),
      targetLsmMax: optionalNumber(values.targetLsmMax),
      targetInterests: splitCommaList(values.targetInterests),
      targetAudienceNotes: values.targetAudienceNotes?.trim() || undefined,
      customerType: values.customerType || undefined,
      buyingBehaviour: values.buyingBehaviour || undefined,
      decisionCycle: values.decisionCycle || undefined,
      pricePositioning: values.pricePositioning || undefined,
      averageCustomerSpendBand: values.averageCustomerSpendBand || undefined,
      growthTarget: values.growthTarget || undefined,
      urgencyLevel: values.urgencyLevel || undefined,
      audienceClarity: values.audienceClarity || undefined,
      valuePropositionFocus: values.valuePropositionFocus || undefined,
      preferredMediaTypes: splitCommaList(values.preferredMediaTypes),
      excludedMediaTypes: splitCommaList(values.excludedMediaTypes),
      mustHaveAreas: splitCommaList(values.mustHaveAreas),
      excludedAreas: splitCommaList(values.excludedAreas),
      creativeReady: values.creativeReady,
      creativeNotes: values.creativeNotes,
      maxMediaItems: optionalNumber(values.maxMediaItems),
      openToUpsell: values.openToUpsell,
      additionalBudget: optionalNumber(values.additionalBudget),
      specialRequirements: values.specialRequirements?.trim() || undefined,
      preferredVideoAspectRatio: values.preferredVideoAspectRatio || undefined,
      preferredVideoDurationSeconds: optionalNumber(values.preferredVideoDurationSeconds),
    };
  }

  return (
    <form className="space-y-6">
      <BriefSection title="Campaign setup" description="Frame the commercial objective, timing, and location in the simplest possible way.">
        <Grid>
          <Field label="Objective" error={errors.objective?.message}>
            <select {...register('objective')} className="input-base">
              <option value="">Select objective</option>
              {objectiveOptions.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </Field>
          <Field label="Business stage">
            <select {...register('businessStage')} className="input-base">
              <option value="">Select business stage</option>
              {businessStages.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </Field>
          <Field label="Monthly revenue">
            <select {...register('monthlyRevenueBand')} className="input-base">
              <option value="">Select monthly revenue</option>
              {monthlyRevenueBands.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </Field>
          <Field label="Sales model">
            <select {...register('salesModel')} className="input-base">
              <option value="">Select sales model</option>
              {salesModels.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </Field>
          <Field label="Geography scope" error={errors.geographyScope?.message}>
            <select
              {...register('geographyScope')}
              className="input-base"
              onChange={(event) => {
                register('geographyScope').onChange(event);
                const scopeValue = event.target.value;
                if (scopeValue === 'national') {
                  setValue('provinces', '');
                  setValue('cities', '');
                  setValue('suburbs', '');
                  setValue('areas', '');
                } else if (scopeValue === 'local') {
                  setValue('provinces', '');
                } else if (scopeValue === 'provincial') {
                  setValue('cities', '');
                  setValue('suburbs', '');
                  setValue('areas', '');
                }
              }}
            >
              <option value="">Select geography scope</option>
              {geographyScopeOptions.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </Field>
          <Field label="Start date" error={errors.startDate?.message}><input {...register('startDate')} type="date" className="input-base" /></Field>
          <Field label="End date" error={errors.endDate?.message}><input {...register('endDate')} type="date" className="input-base" /></Field>
          <Field label="Duration weeks" error={errors.durationWeeks?.message}><input {...register('durationWeeks')} type="number" className="input-base" /></Field>
          <Field label="Preferred video aspect ratio">
            <select {...register('preferredVideoAspectRatio')} className="input-base">
              {videoAspectRatioOptions.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </Field>
          <Field label="Preferred video duration">
            <select {...register('preferredVideoDurationSeconds')} className="input-base">
              {videoDurationOptions.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </Field>
          {geographyScope === 'provincial' ? (
            <Field label="Primary province" error={errors.provinces?.message}><input {...register('provinces')} className="input-base" placeholder="Gauteng" /></Field>
          ) : null}
          {geographyScope === 'local' ? (
            <Field label="Primary city"><input {...register('cities')} className="input-base" placeholder="Johannesburg" /></Field>
          ) : null}
          {geographyScope === 'national' ? (
            <Field label="Primary geography">
              <input value="Not required for national scope" className="input-base bg-slate-50 text-slate-500" disabled />
            </Field>
          ) : null}
        </Grid>
      </BriefSection>

      <BriefSection title="Audience" description="Tell us who matters most so the recommendation can stay focused.">
        <Grid>
          <Field label="Quick age band">
            <select
              {...register('targetAgeBand')}
              className="input-base"
              onChange={(event) => {
                register('targetAgeBand').onChange(event);
                setAgeBand(event.target.value);
              }}
            >
              {ageBandOptions.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </Field>
          <Field label="Quick LSM band">
            <select
              {...register('targetLsmBand')}
              className="input-base"
              onChange={(event) => {
                register('targetLsmBand').onChange(event);
                setLsmBand(event.target.value);
              }}
            >
              {lsmBandOptions.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </Field>
          <Field label="Target age min"><input {...register('targetAgeMin')} type="number" className="input-base" /></Field>
          <Field label="Target age max"><input {...register('targetAgeMax')} type="number" className="input-base" /></Field>
          <Field label="Target gender">
            <select {...register('targetGender')} className="input-base">
              <option value="">Select target gender</option>
              <option value="all">All</option>
              <option value="female">Female</option>
              <option value="male">Male</option>
              <option value="mixed">Mixed</option>
            </select>
          </Field>
          <Field label="Target languages"><input {...register('targetLanguages')} className="input-base" placeholder="English, Zulu" /></Field>
          <Field label="Target LSM min"><input {...register('targetLsmMin')} type="number" className="input-base" /></Field>
          <Field label="Target LSM max"><input {...register('targetLsmMax')} type="number" className="input-base" /></Field>
          <Field label="Target interests"><input {...register('targetInterests')} className="input-base" placeholder="Retail, family, commuters" /></Field>
          <Field label="Current customer type">
            <select {...register('customerType')} className="input-base">
              <option value="">Select customer type</option>
              {customerTypes.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </Field>
          <Field label="Buying behaviour">
            <select {...register('buyingBehaviour')} className="input-base">
              <option value="">Select buying behaviour</option>
              {buyingBehaviours.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </Field>
          <Field label="Decision cycle">
            <select {...register('decisionCycle')} className="input-base">
              <option value="">Select decision cycle</option>
              {decisionCycles.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </Field>
          <Field label="Growth target (6 months)">
            <select {...register('growthTarget')} className="input-base">
              <option value="">Select growth target</option>
              {growthTargets.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </Field>
          <Field label="Price positioning">
            <select {...register('pricePositioning')} className="input-base">
              <option value="">Select price positioning</option>
              {pricePositioning.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </Field>
          <Field label="Average customer spend">
            <select {...register('averageCustomerSpendBand')} className="input-base">
              <option value="">Select average spend</option>
              {averageCustomerSpendBands.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </Field>
          <Field label="Urgency level">
            <select {...register('urgencyLevel')} className="input-base">
              <option value="">Select urgency</option>
              {urgencyLevels.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </Field>
          <Field label="Audience clarity">
            <select {...register('audienceClarity')} className="input-base">
              <option value="">Select audience clarity</option>
              {audienceClarity.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </Field>
          <Field label="Value proposition focus">
            <select {...register('valuePropositionFocus')} className="input-base">
              <option value="">Select value proposition</option>
              {valuePropositionFocus.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </Field>
          <Field label="Audience notes"><textarea {...register('targetAudienceNotes')} className="input-base min-h-[120px]" placeholder="Any audience nuance we should keep in mind?" /></Field>
        </Grid>
      </BriefSection>

      <BriefSection title="Media preferences" description="Guide the recommendation without turning this into a technical planning tool.">
        <Grid>
          <Field label="Preferred media"><input {...register('preferredMediaTypes')} className="input-base" placeholder="radio, digital screens, billboard" /></Field>
          <Field label="Excluded media"><input {...register('excludedMediaTypes')} className="input-base" placeholder="tv, print" /></Field>
          <Field label="Must-have areas"><input {...register('mustHaveAreas')} className="input-base" placeholder="Mall entrances, taxi ranks..." /></Field>
          <Field label="Excluded areas"><input {...register('excludedAreas')} className="input-base" placeholder="Industrial zones..." /></Field>
          <Field label="Max media items"><input {...register('maxMediaItems')} type="number" className="input-base" /></Field>
          <Field label="Additional budget"><input {...register('additionalBudget')} type="number" className="input-base" placeholder="Optional upsell room" /></Field>
        </Grid>
      </BriefSection>

      <BriefSection title="Creative and final notes" description="Help the planning team understand what is ready now and what still needs support.">
        <Grid>
          <Field label="Creative ready?">
            <select {...register('creativeReady', { setValueAs: (value) => value === 'true' })} className="input-base">
              <option value="">Select</option>
              <option value="true">Yes</option>
              <option value="false">No</option>
            </select>
          </Field>
          <Field label="Open to upsell">
            <select {...register('openToUpsell', { setValueAs: (value) => value === 'true' })} className="input-base">
              <option value="true">Yes</option>
              <option value="false">No</option>
            </select>
          </Field>
          <Field label="Creative notes"><textarea {...register('creativeNotes')} className="input-base min-h-[120px]" /></Field>
          <Field label="Special requirements"><textarea {...register('specialRequirements')} className="input-base min-h-[120px]" /></Field>
        </Grid>
      </BriefSection>

      <div className="flex flex-col gap-3 sm:flex-row sm:justify-end">
        <button type="button" disabled={loading} onClick={handleSubmit(async (values: FormShape) => onSave(map(values)))} className="rounded-full border border-line px-5 py-3 text-sm font-semibold text-ink">
          Save draft
        </button>
        <button type="button" disabled={loading} onClick={handleSubmit(async (values: FormShape) => onSubmitBrief(map(values)))} className="rounded-full bg-ink px-5 py-3 text-sm font-semibold text-white">
          Submit brief
        </button>
      </div>
    </form>
  );
}

function BriefSection({ title, description, children }: { title: string; description: string; children: ReactNode }) {
  return (
    <div className="panel px-6 py-6">
      <h3 className="text-xl font-semibold text-ink">{title}</h3>
      <p className="mt-2 text-sm leading-7 text-ink-soft">{description}</p>
      <div className="mt-6">{children}</div>
    </div>
  );
}

function Grid({ children }: { children: ReactNode }) {
  return <div className="grid gap-5 md:grid-cols-2">{children}</div>;
}

function Field({ label, error, children }: { label: string; error?: string; children: ReactNode }) {
  return (
    <label className="block">
      <span className="label-base">{label}</span>
      {children}
      {error ? <p className="helper-text text-rose-600">{error}</p> : null}
    </label>
  );
}
