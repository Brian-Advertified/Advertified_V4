import { z } from 'zod';

export type RecommendationDraftValidationMode = 'opportunity' | 'detailed';

export type RecommendationDraftValidationInput = {
  objective?: string;
  audience?: string;
  scope?: string;
  geography?: string;
  startDate?: string;
  endDate?: string;
  durationWeeks?: string;
  brief?: string;
  channels?: string[];
  salesModel?: string;
  customerType?: string;
  buyingBehaviour?: string;
  decisionCycle?: string;
  pricePositioning?: string;
  growthTarget?: string;
  urgencyLevel?: string;
  audienceClarity?: string;
};

export type RecommendationDraftField =
  | 'objective'
  | 'audience'
  | 'scope'
  | 'geography'
  | 'startDate'
  | 'endDate'
  | 'durationWeeks'
  | 'brief'
  | 'channels'
  | 'salesModel'
  | 'customerType'
  | 'buyingBehaviour'
  | 'decisionCycle'
  | 'pricePositioning'
  | 'growthTarget'
  | 'urgencyLevel'
  | 'audienceClarity';

export type RecommendationDraftValidationErrors = Partial<Record<RecommendationDraftField, string>>;

const requiredText = (fieldLabel: string) => z
  .string()
  .trim()
  .min(1, `${fieldLabel} is required.`);

const applyGeographyValidation = <TSchema extends z.ZodObject>(schema: TSchema) => schema.superRefine((value, context) => {
  const geography = 'geography' in value ? value.geography : undefined;
  const scope = 'scope' in value ? value.scope : undefined;
  const startDate = 'startDate' in value ? value.startDate : undefined;
  const endDate = 'endDate' in value ? value.endDate : undefined;
  const durationWeeks = 'durationWeeks' in value ? value.durationWeeks : undefined;

  if (scope !== 'national' && (typeof geography !== 'string' || geography.trim().length === 0)) {
    context.addIssue({
      code: z.ZodIssueCode.custom,
      path: ['geography'],
      message: scope === 'local' ? 'Main area is required.' : 'Main province is required.',
    });
  }

  if (
    typeof startDate === 'string'
    && typeof endDate === 'string'
    && startDate.trim().length > 0
    && endDate.trim().length > 0
    && new Date(endDate) < new Date(startDate)
  ) {
    context.addIssue({
      code: z.ZodIssueCode.custom,
      path: ['endDate'],
      message: 'End date cannot be earlier than the start date.',
    });
  }

  if (
    typeof durationWeeks === 'string'
    && durationWeeks.trim().length > 0
    && (!Number.isFinite(Number(durationWeeks)) || Number(durationWeeks) <= 0)
  ) {
    context.addIssue({
      code: z.ZodIssueCode.custom,
      path: ['durationWeeks'],
      message: 'Duration weeks must be greater than 0.',
    });
  }
});

const baseObjectSchema = z.object({
  objective: requiredText('Campaign objective'),
  audience: requiredText('Audience'),
  scope: requiredText('Coverage'),
  geography: z.string().optional(),
  startDate: z.string().optional(),
  endDate: z.string().optional(),
  durationWeeks: z.string().optional(),
  brief: requiredText('Client brief'),
  channels: z.array(z.string().trim().min(1)).min(1, 'Select at least one channel.'),
  salesModel: z.string().optional(),
  customerType: z.string().optional(),
  buyingBehaviour: z.string().optional(),
  decisionCycle: z.string().optional(),
  pricePositioning: z.string().optional(),
  growthTarget: z.string().optional(),
  urgencyLevel: z.string().optional(),
  audienceClarity: z.string().optional(),
});

const baseSchema = applyGeographyValidation(baseObjectSchema);

const detailedSchema = applyGeographyValidation(baseObjectSchema.extend({
  salesModel: requiredText('Sales model'),
  customerType: requiredText('Customer type'),
  buyingBehaviour: requiredText('Buying behaviour'),
  decisionCycle: requiredText('Decision cycle'),
  pricePositioning: requiredText('Price positioning'),
  growthTarget: requiredText('Growth target'),
  urgencyLevel: requiredText('Urgency'),
  audienceClarity: requiredText('Audience clarity'),
}));

export function validateRecommendationDraftForm(
  input: RecommendationDraftValidationInput,
  mode: RecommendationDraftValidationMode,
): {
  success: boolean;
  errors: RecommendationDraftValidationErrors;
  missingFields: string[];
} {
  const schema = mode === 'opportunity' ? baseSchema : detailedSchema;
  const result = schema.safeParse(input);

  if (result.success) {
    return {
      success: true,
      errors: {},
      missingFields: [],
    };
  }

  const errors: RecommendationDraftValidationErrors = {};
  const labels: Partial<Record<RecommendationDraftField, string>> = {
    objective: 'campaign objective',
    audience: 'audience',
    scope: 'coverage',
    geography: 'target geography',
    startDate: 'start date',
    endDate: 'end date',
    durationWeeks: 'duration weeks',
    brief: 'client brief',
    channels: 'channel selection',
    salesModel: 'sales model',
    customerType: 'customer type',
    buyingBehaviour: 'buying behaviour',
    decisionCycle: 'decision cycle',
    pricePositioning: 'price positioning',
    growthTarget: 'growth target',
    urgencyLevel: 'urgency',
    audienceClarity: 'audience clarity',
  };
  const missingFields: string[] = [];
  for (const issue of result.error.issues) {
    const field = issue.path[0];
    if (typeof field === 'string' && !(field in errors)) {
      const typedField = field as RecommendationDraftField;
      errors[typedField] = issue.message;
      missingFields.push(labels[typedField] ?? issue.message);
    }
  }

  return {
    success: false,
    errors,
    missingFields,
  };
}
