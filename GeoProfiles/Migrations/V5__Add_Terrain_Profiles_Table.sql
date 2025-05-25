create table terrain_profiles
(
    id         uuid primary key     default uuid_generate_v4(),
    project_id uuid        not null references projects (id) on delete cascade,
    start_pt   geometry(point, 4326) not null,
    end_pt     geometry(point, 4326) not null,
    length_m   numeric     not null,
    png_data   bytea       not null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create index ix_terrain_profiles_project_id on terrain_profiles (project_id);
create index gist_terrain_profiles_geom on terrain_profiles using gist(start_pt, end_pt);

create trigger terrain_profiles_updated_at
    before update
    on terrain_profiles
    for each row
    execute procedure set_updated_at();