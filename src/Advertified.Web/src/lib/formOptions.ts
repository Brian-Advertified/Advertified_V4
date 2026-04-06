export const businessTypes = ['PTY LTD', 'Sole proprietor', 'Partnership', 'Non-profit', 'Other'] as const;

export const industries = ['Retail', 'Finance', 'Hospitality', 'Real estate', 'Automotive', 'Technology', 'Health', 'Other'] as const;

export const provinces = ['Gauteng', 'Western Cape', 'KwaZulu-Natal', 'Eastern Cape', 'Free State', 'Limpopo', 'Mpumalanga', 'North West', 'Northern Cape'] as const;

export const businessStageOptions = [
  { value: '', label: 'Select business stage' },
  { value: 'startup', label: 'Startup (0-1 year)' },
  { value: 'early_growth', label: 'Early growth (1-3 years)' },
  { value: 'established', label: 'Established (3-7 years)' },
  { value: 'mature', label: 'Mature (7+ years)' },
] as const;

export const monthlyRevenueBands = [
  { value: '', label: 'Select monthly revenue' },
  { value: 'under_r50k', label: 'Under R50k' },
  { value: 'r50k_r200k', label: 'R50k - R200k' },
  { value: 'r200k_r1m', label: 'R200k - R1m' },
  { value: 'over_r1m', label: 'Over R1m' },
] as const;

export const salesModelOptions = [
  { value: '', label: 'Select sales model' },
  { value: 'walk_ins', label: 'Walk-ins / physical traffic' },
  { value: 'online_sales', label: 'Online sales' },
  { value: 'direct_sales', label: 'Direct sales / reps' },
  { value: 'referral_based', label: 'Referral-based' },
  { value: 'hybrid', label: 'Hybrid' },
] as const;

export const revenueBands = [
  { value: 'under_r1m', label: 'Under R1m' },
  { value: 'r1m_r5m', label: 'R1m - R5m' },
  { value: 'r5m_r20m', label: 'R5m - R20m' },
  { value: 'r20m_r100m', label: 'R20m - R100m' },
  { value: 'over_r100m', label: 'Over R100m' },
] as const;

export const customerTypeOptions = [
  { value: '', label: 'Select customer type' },
  { value: 'b2c', label: 'Individuals (B2C)' },
  { value: 'smb', label: 'Small businesses' },
  { value: 'corporate', label: 'Corporate / enterprise' },
  { value: 'government', label: 'Government / institutions' },
] as const;

export const buyingBehaviourOptions = [
  { value: '', label: 'Select buying behaviour' },
  { value: 'price_sensitive', label: 'Price-sensitive' },
  { value: 'quality_focused', label: 'Quality-focused' },
  { value: 'convenience_driven', label: 'Convenience-driven' },
  { value: 'brand_conscious', label: 'Brand-conscious' },
  { value: 'urgency_driven', label: 'Urgency-driven' },
] as const;

export const decisionCycleOptions = [
  { value: '', label: 'Select decision cycle' },
  { value: 'same_day', label: 'Immediate (same day)' },
  { value: '1_7_days', label: 'Short (1-7 days)' },
  { value: '1_4_weeks', label: 'Medium (1-4 weeks)' },
  { value: '1_6_months', label: 'Long (1-6 months+)' },
] as const;

export const growthTargetOptions = [
  { value: '', label: 'Select growth target' },
  { value: 'maintain', label: 'Maintain current level' },
  { value: '2x', label: '2x growth' },
  { value: '3x', label: '3x growth' },
  { value: '5x_plus', label: 'Aggressive scale (5x+)' },
] as const;

export const pricePositioningOptions = [
  { value: '', label: 'Select price positioning' },
  { value: 'budget', label: 'Budget / low-cost' },
  { value: 'mid_range', label: 'Mid-range' },
  { value: 'premium', label: 'Premium' },
  { value: 'luxury', label: 'Luxury' },
] as const;

export const averageCustomerSpendOptions = [
  { value: '', label: 'Select average spend' },
  { value: 'under_r500', label: 'Under R500' },
  { value: 'r500_r2000', label: 'R500 - R2,000' },
  { value: 'r2000_r10000', label: 'R2,000 - R10,000' },
  { value: 'r10000_plus', label: 'R10,000+' },
] as const;

export const urgencyLevelOptions = [
  { value: '', label: 'Select urgency' },
  { value: 'immediate', label: 'Immediate (start now)' },
  { value: 'within_1_month', label: 'Within 1 month' },
  { value: 'within_3_months', label: 'Within 3 months' },
] as const;

export const audienceClarityOptions = [
  { value: '', label: 'Select audience clarity' },
  { value: 'very_clear', label: 'Very clear' },
  { value: 'somewhat_clear', label: 'Somewhat clear' },
  { value: 'unclear', label: 'Unclear' },
] as const;

export const valuePropositionFocusOptions = [
  { value: '', label: 'Select value proposition' },
  { value: 'lowest_price', label: 'Lowest price' },
  { value: 'highest_quality', label: 'Highest quality' },
  { value: 'speed_convenience', label: 'Speed / convenience' },
  { value: 'unique_offer', label: 'Unique product / service' },
  { value: 'brand_reputation', label: 'Brand reputation' },
] as const;
