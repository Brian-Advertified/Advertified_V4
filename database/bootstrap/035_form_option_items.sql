CREATE TABLE IF NOT EXISTS form_option_items (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    option_set_key varchar(100) NOT NULL,
    value varchar(100) NOT NULL,
    label varchar(200) NOT NULL,
    sort_order integer NOT NULL DEFAULT 0,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_form_option_items_set_value UNIQUE (option_set_key, value)
);

CREATE INDEX IF NOT EXISTS ix_form_option_items_set_active_sort
    ON form_option_items (option_set_key, is_active, sort_order);

INSERT INTO form_option_items (option_set_key, value, label, sort_order)
VALUES
    ('business_types', 'PTY LTD', 'PTY LTD', 10),
    ('business_types', 'Sole proprietor', 'Sole proprietor', 20),
    ('business_types', 'Partnership', 'Partnership', 30),
    ('business_types', 'Non-profit', 'Non-profit', 40),
    ('business_types', 'Other', 'Other', 50),

    ('industries', 'Retail', 'Retail', 10),
    ('industries', 'Finance', 'Finance', 20),
    ('industries', 'Hospitality', 'Hospitality', 30),
    ('industries', 'Real estate', 'Real estate', 40),
    ('industries', 'Automotive', 'Automotive', 50),
    ('industries', 'Technology', 'Technology', 60),
    ('industries', 'Health', 'Health', 70),
    ('industries', 'Other', 'Other', 80),

    ('provinces', 'Gauteng', 'Gauteng', 10),
    ('provinces', 'Western Cape', 'Western Cape', 20),
    ('provinces', 'KwaZulu-Natal', 'KwaZulu-Natal', 30),
    ('provinces', 'Eastern Cape', 'Eastern Cape', 40),
    ('provinces', 'Free State', 'Free State', 50),
    ('provinces', 'Limpopo', 'Limpopo', 60),
    ('provinces', 'Mpumalanga', 'Mpumalanga', 70),
    ('provinces', 'North West', 'North West', 80),
    ('provinces', 'Northern Cape', 'Northern Cape', 90),

    ('revenue_bands', 'under_r1m', 'Under R1m', 10),
    ('revenue_bands', 'r1m_r5m', 'R1m - R5m', 20),
    ('revenue_bands', 'r5m_r20m', 'R5m - R20m', 30),
    ('revenue_bands', 'r20m_r100m', 'R20m - R100m', 40),
    ('revenue_bands', 'over_r100m', 'Over R100m', 50),

    ('business_stages', 'startup', 'Startup (0-1 year)', 10),
    ('business_stages', 'early_growth', 'Early growth (1-3 years)', 20),
    ('business_stages', 'established', 'Established (3-7 years)', 30),
    ('business_stages', 'mature', 'Mature (7+ years)', 40),

    ('monthly_revenue_bands', 'under_r50k', 'Under R50k', 10),
    ('monthly_revenue_bands', 'r50k_r200k', 'R50k - R200k', 20),
    ('monthly_revenue_bands', 'r200k_r1m', 'R200k - R1m', 30),
    ('monthly_revenue_bands', 'over_r1m', 'Over R1m', 40),

    ('sales_models', 'walk_ins', 'Walk-ins / physical traffic', 10),
    ('sales_models', 'online_sales', 'Online sales', 20),
    ('sales_models', 'direct_sales', 'Direct sales / reps', 30),
    ('sales_models', 'referral_based', 'Referral-based', 40),
    ('sales_models', 'hybrid', 'Hybrid', 50),

    ('customer_types', 'b2c', 'Individuals (B2C)', 10),
    ('customer_types', 'smb', 'Small businesses', 20),
    ('customer_types', 'corporate', 'Corporate / enterprise', 30),
    ('customer_types', 'government', 'Government / institutions', 40),

    ('buying_behaviours', 'price_sensitive', 'Price-sensitive', 10),
    ('buying_behaviours', 'quality_focused', 'Quality-focused', 20),
    ('buying_behaviours', 'convenience_driven', 'Convenience-driven', 30),
    ('buying_behaviours', 'brand_conscious', 'Brand-conscious', 40),
    ('buying_behaviours', 'urgency_driven', 'Urgency-driven', 50),

    ('decision_cycles', 'same_day', 'Immediate (same day)', 10),
    ('decision_cycles', '1_7_days', 'Short (1-7 days)', 20),
    ('decision_cycles', '1_4_weeks', 'Medium (1-4 weeks)', 30),
    ('decision_cycles', '1_6_months', 'Long (1-6 months+)', 40),

    ('growth_targets', 'maintain', 'Maintain current level', 10),
    ('growth_targets', '2x', '2x growth', 20),
    ('growth_targets', '3x', '3x growth', 30),
    ('growth_targets', '5x_plus', 'Aggressive scale (5x+)', 40),

    ('price_positioning', 'budget', 'Budget / low-cost', 10),
    ('price_positioning', 'mid_range', 'Mid-range', 20),
    ('price_positioning', 'premium', 'Premium', 30),
    ('price_positioning', 'luxury', 'Luxury', 40),

    ('average_customer_spend_bands', 'under_r500', 'Under R500', 10),
    ('average_customer_spend_bands', 'r500_r2000', 'R500 - R2,000', 20),
    ('average_customer_spend_bands', 'r2000_r10000', 'R2,000 - R10,000', 30),
    ('average_customer_spend_bands', 'r10000_plus', 'R10,000+', 40),

    ('urgency_levels', 'immediate', 'Immediate (start now)', 10),
    ('urgency_levels', 'within_1_month', 'Within 1 month', 20),
    ('urgency_levels', 'within_3_months', 'Within 3 months', 30),

    ('audience_clarity', 'very_clear', 'Very clear', 10),
    ('audience_clarity', 'somewhat_clear', 'Somewhat clear', 20),
    ('audience_clarity', 'unclear', 'Unclear', 30),

    ('value_proposition_focus', 'lowest_price', 'Lowest price', 10),
    ('value_proposition_focus', 'highest_quality', 'Highest quality', 20),
    ('value_proposition_focus', 'speed_convenience', 'Speed / convenience', 30),
    ('value_proposition_focus', 'unique_offer', 'Unique product / service', 40),
    ('value_proposition_focus', 'brand_reputation', 'Brand reputation', 50)
ON CONFLICT (option_set_key, value) DO UPDATE
SET label = EXCLUDED.label,
    sort_order = EXCLUDED.sort_order,
    is_active = true,
    updated_at = now();

ALTER TABLE campaign_briefs
    DROP COLUMN IF EXISTS current_customer_notes;
