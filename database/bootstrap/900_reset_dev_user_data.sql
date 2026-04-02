begin;

-- Trigger marker: forced DEV user-data reset run.
do $$
declare
    table_name text;
    user_data_tables text[] := array[
        'notification_read_receipts',
        'campaign_messages',
        'campaign_conversations',
        'campaign_assets',
        'campaign_delivery_reports',
        'campaign_supplier_bookings',
        'campaign_pause_windows',
        'creative_scores',
        'campaign_creatives',
        'campaign_creative_systems',
        'campaign_brief_drafts',
        'campaign_briefs',
        'recommendation_items',
        'campaign_recommendations',
        'ai_ad_metrics',
        'ai_ad_variants',
        'ai_usage_logs',
        'ai_idempotency_records',
        'ai_creative_job_dead_letters',
        'ai_asset_jobs',
        'ai_creative_qa_results',
        'ai_creative_job_statuses',
        'invoices',
        'invoice_line_items',
        'payment_provider_requests',
        'payment_provider_webhooks',
        'consent_preferences',
        'change_audit_logs',
        'campaigns',
        'package_orders',
        'agent_area_assignments',
        'email_verification_tokens',
        'identity_profiles',
        'business_profiles',
        'user_accounts'
    ];
begin
    foreach table_name in array user_data_tables loop
        if to_regclass(table_name) is not null then
            execute format('truncate table %I restart identity cascade;', table_name);
        end if;
    end loop;
end $$;

commit;
