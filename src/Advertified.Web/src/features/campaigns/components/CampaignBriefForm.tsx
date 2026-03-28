import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import type { ReactNode } from 'react';
import type { CampaignBrief } from '../../../types/domain';

const briefSchema = z
  .object({
    objective: z.string().min(1, 'Objective is required.'),
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
  })
  .refine((value) => !value.startDate || !value.endDate || new Date(value.endDate) >= new Date(value.startDate), {
    message: 'Campaign end date must be after the start date.',
    path: ['endDate'],
  });

type FormShape = z.infer<typeof briefSchema>;

function splitList(value?: string) {
  return value?.split(',').map((item) => item.trim()).filter(Boolean);
}

function joinList(value?: string[]) {
  return value?.join(', ');
}

function optionalNumber(value?: string) {
  if (!value) {
    return undefined;
  }

  const parsed = Number(value);
  return Number.isNaN(parsed) ? undefined : parsed;
}

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
    formState: { errors },
  } = useForm<FormShape>({
    resolver: zodResolver(briefSchema),
    defaultValues: {
      objective: initialValue?.objective ?? '',
      geographyScope: initialValue?.geographyScope ?? '',
      startDate: initialValue?.startDate,
      endDate: initialValue?.endDate,
      durationWeeks: initialValue?.durationWeeks?.toString(),
      provinces: joinList(initialValue?.provinces),
      cities: joinList(initialValue?.cities),
      suburbs: joinList(initialValue?.suburbs),
      areas: joinList(initialValue?.areas),
      targetAgeMin: initialValue?.targetAgeMin?.toString(),
      targetAgeMax: initialValue?.targetAgeMax?.toString(),
      targetGender: initialValue?.targetGender,
      targetLanguages: joinList(initialValue?.targetLanguages),
      targetLsmMin: initialValue?.targetLsmMin?.toString(),
      targetLsmMax: initialValue?.targetLsmMax?.toString(),
      targetInterests: joinList(initialValue?.targetInterests),
      targetAudienceNotes: initialValue?.targetAudienceNotes,
      preferredMediaTypes: joinList(initialValue?.preferredMediaTypes),
      excludedMediaTypes: joinList(initialValue?.excludedMediaTypes),
      mustHaveAreas: joinList(initialValue?.mustHaveAreas),
      excludedAreas: joinList(initialValue?.excludedAreas),
      creativeReady: initialValue?.creativeReady,
      creativeNotes: initialValue?.creativeNotes,
      maxMediaItems: initialValue?.maxMediaItems?.toString(),
      openToUpsell: initialValue?.openToUpsell ?? true,
      additionalBudget: initialValue?.additionalBudget?.toString(),
      specialRequirements: initialValue?.specialRequirements,
    },
  });

  function map(values: FormShape): CampaignBrief {
    return {
      objective: values.objective,
      geographyScope: values.geographyScope,
      startDate: values.startDate,
      endDate: values.endDate,
      durationWeeks: optionalNumber(values.durationWeeks),
      provinces: splitList(values.provinces),
      cities: splitList(values.cities),
      suburbs: splitList(values.suburbs),
      areas: splitList(values.areas),
      targetAgeMin: optionalNumber(values.targetAgeMin),
      targetAgeMax: optionalNumber(values.targetAgeMax),
      targetGender: values.targetGender,
      targetLanguages: splitList(values.targetLanguages),
      targetLsmMin: optionalNumber(values.targetLsmMin),
      targetLsmMax: optionalNumber(values.targetLsmMax),
      targetInterests: splitList(values.targetInterests),
      targetAudienceNotes: values.targetAudienceNotes,
      preferredMediaTypes: splitList(values.preferredMediaTypes),
      excludedMediaTypes: splitList(values.excludedMediaTypes),
      mustHaveAreas: splitList(values.mustHaveAreas),
      excludedAreas: splitList(values.excludedAreas),
      creativeReady: values.creativeReady,
      creativeNotes: values.creativeNotes,
      maxMediaItems: optionalNumber(values.maxMediaItems),
      openToUpsell: values.openToUpsell,
      additionalBudget: optionalNumber(values.additionalBudget),
      specialRequirements: values.specialRequirements,
    };
  }

  return (
    <form className="space-y-6">
      <BriefSection title="Campaign setup" description="Frame the commercial objective, timing, and location in the simplest possible way.">
        <Grid>
          <Field label="Objective" error={errors.objective?.message}><input {...register('objective')} className="input-base" placeholder="awareness, leads, foot_traffic..." /></Field>
          <Field label="Geography scope" error={errors.geographyScope?.message}><input {...register('geographyScope')} className="input-base" placeholder="local, regional, provincial, national" /></Field>
          <Field label="Start date" error={errors.startDate?.message}><input {...register('startDate')} type="date" className="input-base" /></Field>
          <Field label="End date" error={errors.endDate?.message}><input {...register('endDate')} type="date" className="input-base" /></Field>
          <Field label="Duration weeks" error={errors.durationWeeks?.message}><input {...register('durationWeeks')} type="number" className="input-base" /></Field>
          <Field label="Provinces" error={errors.provinces?.message}><input {...register('provinces')} className="input-base" placeholder="Western Cape, Gauteng" /></Field>
          <Field label="Cities"><input {...register('cities')} className="input-base" placeholder="Cape Town, Johannesburg" /></Field>
          <Field label="Areas / suburbs"><input {...register('areas')} className="input-base" placeholder="Sea Point, Sandton..." /></Field>
        </Grid>
      </BriefSection>

      <BriefSection title="Audience" description="Tell us who matters most so the recommendation can stay focused.">
        <Grid>
          <Field label="Target age min"><input {...register('targetAgeMin')} type="number" className="input-base" /></Field>
          <Field label="Target age max"><input {...register('targetAgeMax')} type="number" className="input-base" /></Field>
          <Field label="Target gender"><input {...register('targetGender')} className="input-base" placeholder="all, female, male..." /></Field>
          <Field label="Target languages"><input {...register('targetLanguages')} className="input-base" placeholder="English, Zulu" /></Field>
          <Field label="Target LSM min"><input {...register('targetLsmMin')} type="number" className="input-base" /></Field>
          <Field label="Target LSM max"><input {...register('targetLsmMax')} type="number" className="input-base" /></Field>
          <Field label="Target interests"><input {...register('targetInterests')} className="input-base" placeholder="Retail, family, commuters" /></Field>
          <Field label="Audience notes"><textarea {...register('targetAudienceNotes')} className="input-base min-h-[120px]" placeholder="Any audience nuance we should keep in mind?" /></Field>
        </Grid>
      </BriefSection>

      <BriefSection title="Media preferences" description="Guide the recommendation without turning this into a technical planning tool.">
        <Grid>
          <Field label="Preferred media"><input {...register('preferredMediaTypes')} className="input-base" placeholder="radio, digital, ooh" /></Field>
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
