delete from media_outlet_pricing_package
where id = '4d7fd79e-74db-4e07-a6d1-7b8f1d2a4422'
   or media_outlet_id = 'c1d47f7a-5d7d-4a1f-9a6b-7f9a5e3f4b11';

delete from media_outlet_keyword
where media_outlet_id = 'c1d47f7a-5d7d-4a1f-9a6b-7f9a5e3f4b11';

delete from media_outlet_language
where media_outlet_id = 'c1d47f7a-5d7d-4a1f-9a6b-7f9a5e3f4b11';

delete from media_outlet_geography
where media_outlet_id = 'c1d47f7a-5d7d-4a1f-9a6b-7f9a5e3f4b11';

delete from media_outlet
where id = 'c1d47f7a-5d7d-4a1f-9a6b-7f9a5e3f4b11'
   or code = 'ooh_jhb_starter';
