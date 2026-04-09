export type AutoBriefConfidence = 'detected' | 'strongly_inferred' | 'weakly_inferred' | 'no_evidence';

export type AutoBriefFieldKey =
  | 'objective'
  | 'audience'
  | 'scope'
  | 'geography'
  | 'tone'
  | 'salesModel'
  | 'customerType'
  | 'buyingBehaviour'
  | 'decisionCycle'
  | 'urgencyLevel'
  | 'language'
  | 'targetInterests'
  | 'targetGender'
  | 'ageRange';

export type AutoBriefField = {
  value: string;
  confidence: AutoBriefConfidence;
  reason?: string;
};

export type AutoBriefPayload = {
  fields?: Partial<Record<AutoBriefFieldKey, AutoBriefField>>;
  channels?: {
    values: string[];
    confidence: AutoBriefConfidence;
    reason?: string;
  };
  uncertainFields?: string[];
  generatedAtUtc?: string;
};
