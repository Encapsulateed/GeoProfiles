create table isolines
(
    id         uuid primary key     default uuid_generate_v4(),
    project_id uuid        not null,
    level      int         not null check (level >= 0),
    geom       geometry(Polygon, 4326) not null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create index if not exists ix_isolines_project_id on isolines(project_id);
create index if not exists gist_isolines_geom on isolines using gist(geom);

create trigger isolines_updated_at
    before update
    on isolines
    for each row
    execute procedure set_updated_at();

alter table isolines
    add constraint fk_isolines_projects
        foreign key (project_id) references projects (id) on delete cascade;
