alter table campaign_briefs
    add column if not exists business_stage varchar(50) null,
    add column if not exists monthly_revenue_band varchar(50) null,
    add column if not exists sales_model varchar(50) null,
    add column if not exists customer_type varchar(50) null,
    add column if not exists current_customer_notes text null,
    add column if not exists buying_behaviour varchar(50) null,
    add column if not exists decision_cycle varchar(50) null,
    add column if not exists price_positioning varchar(50) null,
    add column if not exists average_customer_spend_band varchar(50) null,
    add column if not exists growth_target varchar(50) null,
    add column if not exists urgency_level varchar(50) null,
    add column if not exists audience_clarity varchar(50) null,
    add column if not exists value_proposition_focus varchar(50) null;
