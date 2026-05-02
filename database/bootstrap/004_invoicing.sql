create table if not exists invoice_issuer_profiles
(
    id uuid primary key default gen_random_uuid(),
    legal_name varchar(200) not null,
    registration_number varchar(50) not null,
    vat_number varchar(50) not null,
    address text not null,
    contact_email varchar(255) not null,
    contact_phone varchar(50) not null,
    logo_path text null,
    is_active boolean not null default true,
    created_at_utc timestamp without time zone not null default timezone('utc', now()),
    updated_at_utc timestamp without time zone not null default timezone('utc', now())
);

create unique index if not exists uq_invoice_issuer_profiles_active
    on invoice_issuer_profiles (is_active)
    where is_active = true;

create table if not exists legal_documents
(
    id uuid primary key default gen_random_uuid(),
    document_key varchar(120) not null,
    title varchar(200) not null,
    version_label varchar(50) not null,
    body_json jsonb not null,
    is_active boolean not null default true,
    created_at_utc timestamp without time zone not null default timezone('utc', now()),
    updated_at_utc timestamp without time zone not null default timezone('utc', now())
);

create unique index if not exists uq_legal_documents_document_key on legal_documents(document_key);

create table if not exists invoices
(
    id uuid primary key default gen_random_uuid(),
    package_order_id uuid not null references package_orders(id) on delete cascade,
    campaign_id uuid null references campaigns(id) on delete set null,
    user_id uuid not null references user_accounts(id) on delete restrict,
    company_id uuid null references business_profiles(id) on delete set null,
    invoice_number varchar(50) not null,
    provider varchar(50) not null,
    invoice_type varchar(50) not null,
    status varchar(50) not null,
    currency varchar(3) not null default 'ZAR',
    total_amount numeric(12,2) not null,
    campaign_name varchar(200) not null,
    package_name varchar(100) null,
    customer_name varchar(200) not null,
    customer_email varchar(255) not null,
    customer_address text not null,
    company_name varchar(200) not null,
    company_registration_number varchar(50) null,
    company_vat_number varchar(50) null,
    company_address text null,
    payment_reference varchar(200) null,
    storage_object_key text null,
    created_at_utc timestamp without time zone not null default timezone('utc', now()),
    due_at_utc timestamp without time zone null,
    paid_at_utc timestamp without time zone null
);

create unique index if not exists uq_invoices_package_order_id on invoices(package_order_id);
create unique index if not exists uq_invoices_invoice_number on invoices(invoice_number);
create index if not exists ix_invoices_user_id on invoices(user_id);
create index if not exists ix_invoices_status on invoices(status);
create index if not exists ix_invoices_provider on invoices(provider);

create table if not exists invoice_line_items
(
    id uuid primary key default gen_random_uuid(),
    invoice_id uuid not null references invoices(id) on delete cascade,
    line_type varchar(100) not null,
    description text not null,
    quantity numeric(12,2) not null,
    unit_amount numeric(12,2) not null,
    subtotal_amount numeric(12,2) not null,
    vat_amount numeric(12,2) not null,
    total_amount numeric(12,2) not null,
    sort_order integer not null default 0,
    created_at_utc timestamp without time zone not null default timezone('utc', now())
);

create index if not exists ix_invoice_line_items_invoice_id on invoice_line_items(invoice_id);

update invoice_issuer_profiles
set
    legal_name = 'Advertified (PTY) Ltd',
    registration_number = '2024/104944/07',
    vat_number = '4210266484',
    address = E'The Vineyard\nDevon Valley Road\nDevon Park\nStellenbosch\n7600',
    contact_email = 'ad@advertified.co.za',
    contact_phone = '0812549067',
    is_active = true,
    updated_at_utc = timezone('utc', now())
where registration_number = '2014/147638/07'
   or registration_number = '2024/104944/07'
   or legal_name = 'Black Space PSG (Pty) Ltd t/a Black Space VSBLT';

insert into invoice_issuer_profiles
(
    legal_name,
    registration_number,
    vat_number,
    address,
    contact_email,
    contact_phone,
    is_active
)
select
    'Advertified (Pty) Ltd',
    '2024/104944/07',
    '4210266484',
    E'The Vineyard\nDevon Valley Road\nDevon Park\nStellenbosch\n7600',
    'ad@advertified.co.za',
    '0812549067',
    true
where not exists (
    select 1
    from invoice_issuer_profiles
    where registration_number = '2024/104944/07'
);

update invoice_issuer_profiles
set
    is_active = false,
    updated_at_utc = timezone('utc', now())
where registration_number <> '2024/104944/07'
  and is_active = true;

insert into legal_documents
(
    document_key,
    title,
    version_label,
    body_json,
    is_active
)
select
    'terms-and-conditions',
    'Terms and Conditions',
    '2026-04-05',
    $$[
      {"title":"1. Agreement Formation","paragraphs":["These Terms and Conditions constitute a binding agreement between Advertified and the Client upon the earliest of written acceptance of a quotation or proposal, issuance of a purchase order or instruction, or payment of any invoice.","In the event of conflict, the following order of precedence applies: signed agreement, if any; approved proposal or insertion order; then these Terms and Conditions."]},
      {"title":"2. Payment Terms","paragraphs":["Payment is due within 7 (seven) days from invoice date unless otherwise agreed in writing.","Late payments incur interest at 2% per month, calculated daily.","Advertified reserves the right to suspend campaigns for accounts overdue by more than 7 days and cancel campaigns for accounts overdue by more than 14 days.","The Client is liable for all reasonable legal and collection costs incurred in recovering overdue amounts."]},
      {"title":"3. Booking and Media Placement","paragraphs":["All media placements are subject to availability and supplier confirmation.","No booking is secured until payment or valid proof of payment is received.","Advertified reserves the right to substitute equivalent media placements where necessary."]},
      {"title":"4. Cancellations and Amendments","paragraphs":["All cancellations must be submitted in writing.","Cancellation fees may be up to 50% more than 14 days before campaign start, and up to 100% less than 7 days before campaign start.","Post-confirmation changes may incur additional costs and remain subject to supplier approval."]},
      {"title":"5. Third-Party Media Suppliers","paragraphs":["Advertified acts solely as an intermediary. All media inventory is owned and operated by third-party suppliers.","The Client agrees that supplier terms apply in addition to these Terms, and that Advertified is not liable for supplier delays, errors, or non-performance.","In the event of supplier failure, Advertified's obligation is limited to rebooking equivalent media or issuing credit where applicable."]},
      {"title":"6. Campaign Execution","paragraphs":["Campaign timelines depend on receipt of payment, final creative approval, and supplier scheduling.","Delays caused by the Client do not entitle the Client to refunds."]},
      {"title":"7. Creative Content and Compliance","paragraphs":["The Client warrants that all content complies with South African law and meets Advertising Regulatory Board standards.","Advertified reserves the right to reject non-compliant material."]},
      {"title":"8. Intellectual Property","paragraphs":["The Client retains ownership of all creative assets supplied.","The Client grants Advertified a non-exclusive license to use campaign materials for execution and marketing purposes.","The Client indemnifies Advertified against any intellectual property infringement claims."]},
      {"title":"9. Data Protection (POPIA)","paragraphs":["Advertified processes personal information in accordance with the Protection of Personal Information Act.","The Client consents to the processing of data necessary for campaign execution and communication."]},
      {"title":"10. Proof of Performance","paragraphs":["Proof of campaign execution may include photos, logs, or supplier reports, depending on supplier capability.","Such proof constitutes sufficient evidence of delivery."]},
      {"title":"11. No Performance Guarantee","paragraphs":["Advertified does not guarantee sales outcomes, audience engagement, or return on investment.","Advertising inherently carries commercial risk."]},
      {"title":"12. Refund Policy","paragraphs":["Refunds are not standard and remain subject to supplier approval.","Where applicable, refunds will be issued as account credit by default, or a partial monetary refund at Advertified's discretion."]},
      {"title":"13. Limitation of Liability","paragraphs":["Advertified's total liability is limited to the value of fees paid by the Client.","Advertified is not liable for indirect or consequential losses, including loss of profit, revenue, or business opportunity."]},
      {"title":"14. Indemnity","paragraphs":["The Client indemnifies Advertified against all claims arising from illegal or non-compliant advertising content, intellectual property infringement, defamation, or regulatory breaches."]},
      {"title":"15. Force Majeure","paragraphs":["Advertified is not liable for delays or failures caused by events beyond its control, including natural disasters, government actions, or supplier disruptions."]},
      {"title":"16. Dispute Resolution","paragraphs":["Disputes must be submitted in writing within 5 business days.","The parties agree to attempt resolution in good faith before litigation."]},
      {"title":"17. Governing Law","paragraphs":["This agreement is governed by the laws of the Republic of South Africa.","Jurisdiction is the Gauteng High Court."]},
      {"title":"18. Non-Assignment","paragraphs":["The Client may not assign or transfer rights or obligations without prior written consent."]},
      {"title":"19. Entire Agreement","paragraphs":["These Terms constitute the entire agreement and supersede all prior discussions or representations."]},
      {"title":"20. Acceptance","paragraphs":["Payment or written confirmation constitutes full acceptance of these Terms and Conditions."]}
    ]$$::jsonb,
    true
where not exists (
    select 1
    from legal_documents
    where document_key = 'terms-and-conditions'
);

update legal_documents
set
    title = 'Terms and Conditions',
    version_label = '2026-04-05',
    body_json = $$[
      {"title":"1. Agreement Formation","paragraphs":["These Terms and Conditions constitute a binding agreement between Advertified and the Client upon the earliest of written acceptance of a quotation or proposal, issuance of a purchase order or instruction, or payment of any invoice.","In the event of conflict, the following order of precedence applies: signed agreement, if any; approved proposal or insertion order; then these Terms and Conditions."]},
      {"title":"2. Payment Terms","paragraphs":["Payment is due within 7 (seven) days from invoice date unless otherwise agreed in writing.","Late payments incur interest at 2% per month, calculated daily.","Advertified reserves the right to suspend campaigns for accounts overdue by more than 7 days and cancel campaigns for accounts overdue by more than 14 days.","The Client is liable for all reasonable legal and collection costs incurred in recovering overdue amounts."]},
      {"title":"3. Booking and Media Placement","paragraphs":["All media placements are subject to availability and supplier confirmation.","No booking is secured until payment or valid proof of payment is received.","Advertified reserves the right to substitute equivalent media placements where necessary."]},
      {"title":"4. Cancellations and Amendments","paragraphs":["All cancellations must be submitted in writing.","Cancellation fees may be up to 50% more than 14 days before campaign start, and up to 100% less than 7 days before campaign start.","Post-confirmation changes may incur additional costs and remain subject to supplier approval."]},
      {"title":"5. Third-Party Media Suppliers","paragraphs":["Advertified acts solely as an intermediary. All media inventory is owned and operated by third-party suppliers.","The Client agrees that supplier terms apply in addition to these Terms, and that Advertified is not liable for supplier delays, errors, or non-performance.","In the event of supplier failure, Advertified's obligation is limited to rebooking equivalent media or issuing credit where applicable."]},
      {"title":"6. Campaign Execution","paragraphs":["Campaign timelines depend on receipt of payment, final creative approval, and supplier scheduling.","Delays caused by the Client do not entitle the Client to refunds."]},
      {"title":"7. Creative Content and Compliance","paragraphs":["The Client warrants that all content complies with South African law and meets Advertising Regulatory Board standards.","Advertified reserves the right to reject non-compliant material."]},
      {"title":"8. Intellectual Property","paragraphs":["The Client retains ownership of all creative assets supplied.","The Client grants Advertified a non-exclusive license to use campaign materials for execution and marketing purposes.","The Client indemnifies Advertified against any intellectual property infringement claims."]},
      {"title":"9. Data Protection (POPIA)","paragraphs":["Advertified processes personal information in accordance with the Protection of Personal Information Act.","The Client consents to the processing of data necessary for campaign execution and communication."]},
      {"title":"10. Proof of Performance","paragraphs":["Proof of campaign execution may include photos, logs, or supplier reports, depending on supplier capability.","Such proof constitutes sufficient evidence of delivery."]},
      {"title":"11. No Performance Guarantee","paragraphs":["Advertified does not guarantee sales outcomes, audience engagement, or return on investment.","Advertising inherently carries commercial risk."]},
      {"title":"12. Refund Policy","paragraphs":["Refunds are not standard and remain subject to supplier approval.","Where applicable, refunds will be issued as account credit by default, or a partial monetary refund at Advertified's discretion."]},
      {"title":"13. Limitation of Liability","paragraphs":["Advertified's total liability is limited to the value of fees paid by the Client.","Advertified is not liable for indirect or consequential losses, including loss of profit, revenue, or business opportunity."]},
      {"title":"14. Indemnity","paragraphs":["The Client indemnifies Advertified against all claims arising from illegal or non-compliant advertising content, intellectual property infringement, defamation, or regulatory breaches."]},
      {"title":"15. Force Majeure","paragraphs":["Advertified is not liable for delays or failures caused by events beyond its control, including natural disasters, government actions, or supplier disruptions."]},
      {"title":"16. Dispute Resolution","paragraphs":["Disputes must be submitted in writing within 5 business days.","The parties agree to attempt resolution in good faith before litigation."]},
      {"title":"17. Governing Law","paragraphs":["This agreement is governed by the laws of the Republic of South Africa.","Jurisdiction is the Gauteng High Court."]},
      {"title":"18. Non-Assignment","paragraphs":["The Client may not assign or transfer rights or obligations without prior written consent."]},
      {"title":"19. Entire Agreement","paragraphs":["These Terms constitute the entire agreement and supersede all prior discussions or representations."]},
      {"title":"20. Acceptance","paragraphs":["Payment or written confirmation constitutes full acceptance of these Terms and Conditions."]}
    ]$$::jsonb,
    is_active = true,
    updated_at_utc = timezone('utc', now())
where document_key = 'terms-and-conditions';
