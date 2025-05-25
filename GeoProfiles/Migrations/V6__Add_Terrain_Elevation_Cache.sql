create table elevation_cache
(
    pt         geometry(point, 4326) primary key,
    elev_m     numeric     not null,
    updated_at timestamptz not null default now()
);

create index if not exists gist_elev_cache_pt on elevation_cache using gist(pt);