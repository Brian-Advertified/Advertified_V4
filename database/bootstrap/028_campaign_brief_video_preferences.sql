alter table campaign_briefs
    add column if not exists preferred_video_aspect_ratio varchar(10) null;

alter table campaign_briefs
    add column if not exists preferred_video_duration_seconds integer null;

