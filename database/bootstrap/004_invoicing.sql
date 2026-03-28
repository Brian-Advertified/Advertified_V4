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
    'Black Space PSG (Pty) Ltd t/a Black Space VSBLT',
    '2014/147638/07',
    '4210266484',
    E'08 Kikuyu Road\nSunninghill\nGauteng\n2191',
    'info@blackspacegroup.co.za',
    '0812549067',
    true
where not exists (select 1 from invoice_issuer_profiles where is_active = true);
