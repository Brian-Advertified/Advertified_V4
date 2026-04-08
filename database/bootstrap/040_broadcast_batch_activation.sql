with ranked_active_batches as (
    select
        id,
        row_number() over (
            partition by channel_family
            order by activated_at desc nulls last, created_at desc, id desc
        ) as row_number
    from inventory_import_batches
    where is_active = true
)
update inventory_import_batches batches
set
    is_active = false,
    status = case when status = 'active' then 'superseded' else status end
from ranked_active_batches ranked
where batches.id = ranked.id
  and ranked.row_number > 1;

create unique index if not exists ux_inventory_import_batches_channel_active
    on inventory_import_batches (channel_family)
    where is_active = true;

create index if not exists ix_media_outlet_media_type_import_batch
    on media_outlet (media_type, import_batch_id);
